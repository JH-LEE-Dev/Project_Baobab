using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// RoguelikeSystem의 흐름 코루틴. 기획서 7장 상태 표의 "다음 상태"를 그대로 이어 붙였다.
// 한 번에 하나의 흐름만 돈다(StartFlow가 이전 흐름을 끊는다). 게임오버·포기는 진행 중인 흐름을 끊고 새 흐름을 시작한다.
public partial class RoguelikeSystem
{
    // // 로비

    /// <summary>로비로 들어간다. 결과 화면에서 오면(_bFromResult) 런 상태를 전부 비우고 캐릭터를 되살린다.</summary>
    private IEnumerator EnterLobbyRoutine(bool _bFromResult)
    {
        SetPhase(ERunPhase.ReturningToLobby);
        DisableControl();

        // Lobby가 될 때까지 ESC를 막는다. 이 구간의 메인메뉴 요청은 처리할 곳이 없어 무시되는데(GoToMainMenuRequested),
        // ESC 쪽은 그 전에 메뉴를 닫고 이동 잠금을 풀어 버려 연출 중에 캐릭터가 움직인다.
        inputManager.SetESCKeyLock(LobbyEntryESCLockOwner, true);

        if (_bFromResult)
        {
            yield return productionGate.Play(new RoguelikeProductionRequest
            {
                type = ERoguelikeProduction.ResultToLobby,
                characterTransform = null != character ? character.transform : null,
            });
        }

        ResetRunState();
        RestoreCharacterAfterRun();

        yield return BuildFieldRoutine(0);

        PrepareCharacterForField();
        PlaceLobbyObjects(inDungeonObjectManager.GetPlayerStartPos());

        TierFieldData lobbyField = GetTierField(0);
        signalHub.Publish(new DecalreDungeonTypeSignal(lobbyField.mapType, lobbyField.forestType));

        yield return productionGate.Play(new RoguelikeProductionRequest
        {
            type = ERoguelikeProduction.LobbyEntry,
            bFromResult = _bFromResult,
            characterTransform = null != character ? character.transform : null,
            mapType = lobbyField.mapType,
        });

        signalHub.Publish(new PopupUIUpSignal());
        inputManager.SetESCKeyLock(LobbyEntryESCLockOwner, false);
        EnableControl();
        SetPhase(ERunPhase.Lobby);

        flowCoroutine = null;
    }

    /// <summary>로비에서 메인메뉴로(ESC → 메인메뉴). 본편 던전의 메인메뉴 이탈과 같은 신호를 쓴다.</summary>
    private IEnumerator ExitToMainMenuRoutine()
    {
        bExitingToMainMenu = true;
        DisableControl();
        RemoveLobbyObjects();

        Sound.FadeOutBGM(exitToMainMenuSeconds);
        signalHub.Publish(new StartSkyProductionSignal(true));
        signalHub.Publish(new PopupUIDownSignal());
        GoToMainMenuCurtainRevealEvent?.Invoke();

        yield return new WaitForSecondsRealtime(exitToMainMenuSeconds);

        signalHub.Publish(new GoToMainMenuSignal());
    }

    // // 런

    private IEnumerator RunRoutine()
    {
        RemoveLobbyObjects();
        DisableControl();
        SetPhase(ERunPhase.RunStarting);

        ResetRunState();
        BeginRunState();

        yield return productionGate.Play(new RoguelikeProductionRequest
        {
            type = ERoguelikeProduction.LobbyToRun,
            characterTransform = null != character ? character.transform : null,
        });

        runWaveNumber = 1;
        while (true)
        {
            WaveSpec spec = WaveSchedule.Get(balance, runWaveNumber);
            currentWave = spec;

            // 언리밋은 카드가 나오지 않는다(기획서 3-5).
            if (false == spec.bUnlimit)
            {
                yield return CardSelectRoutine(1 == runWaveNumber);
            }

            // 지금 깔린 지형의 티어와 다르면 전환한다. spec.bTierChanged(직전 웨이브 대비)만 보면
            // 웨이브 번호가 건너뛰는 경우(디버그 점프, 밸런스표 변경)에 지형이 옛 티어로 남는다.
            if (spec.tierIndex != currentFieldTierIndex)
            {
                yield return TierTransitionRoutine(spec);
            }

            yield return WaveCombatRoutine(spec);
            yield return WaveClearRoutine(spec);

            if (spec.bUnlimit) lastClearedUnlimitWave = spec.unlimitIndex;
            else lastClearedWave = spec.waveNumber;

            runWaveNumber++;
        }
    }

    private IEnumerator CardSelectRoutine(bool _bFirstPick)
    {
        SetPhase(ERunPhase.CardSelect);
        DisableControl();

        bCardPresentationDone = false;
        discardedOfferIndices.Clear();
        shop.Open(_bFirstPick);
        signalHub.Publish(new RoguelikeCardSelectionOpenedSignal());

        // 구매 또는 건너뛰기로 닫힐 때까지. 그동안 시간은 흐르지 않는다(필드는 비어 있고 판정도 멈춰 있다).
        while (shop.IsOpen)
        {
            yield return null;
        }

        // 카드 UI의 구매·소멸 연출이 끝나기를 기다린다.
        // 상한은 게임 시간으로 센다. ESC·보유 카드 일시정지(timeScale 0) 동안 흐르면 메뉴가 떠 있는 채로
        // 강제 진행되어 EnableControl이 메뉴 아래에서 입력 모드를 Gameplay로 바꿔 버린다.
        float elapsed = 0f;
        while (false == bCardPresentationDone && elapsed < maxCardPresentationWaitSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (false == bCardPresentationDone)
        {
            Debug.LogWarning("[RoguelikeSystem] 카드 UI가 연출 완료를 알리지 않아 다음 단계로 강제 진행합니다.");

            // 연출이 끝나지 않은 카드 화면이 전투 위에 남지 않게 여기서 닫는다.
            uiCoordinator.ForceCloseCardSelection();
        }
    }

    private IEnumerator TierTransitionRoutine(WaveSpec _spec)
    {
        SetPhase(ERunPhase.TierTransition);
        DisableControl();

        yield return BuildFieldRoutine(_spec.tierIndex);
        PrepareCharacterForField();

        TierFieldData field = GetTierField(_spec.tierIndex);
        signalHub.Publish(new DecalreDungeonTypeSignal(field.mapType, field.forestType));
        signalHub.Publish(new RoguelikeTierChangedSignal(_spec.tierIndex, field.mapType));

        yield return productionGate.Play(new RoguelikeProductionRequest
        {
            type = ERoguelikeProduction.TierTransition,
            tierIndex = _spec.tierIndex,
            mapType = field.mapType,
            characterTransform = null != character ? character.transform : null,
        });
    }

    private IEnumerator WaveCombatRoutine(WaveSpec _spec)
    {
        waveKills = 0;
        treeRules.SetTreeHpMultiplier(_spec.treeHpMultiplier);
        ConfigurePressure();

        float spawnRate = Mathf.Max(0.1f, 1f + modifiers.Get(ERunEffect.TreeSpawnRatePercent) / 100f);
        waveStrategy?.BeginWave(inDungeonObjectManager, _spec.spawnInterval / spawnRate, balance.SpawnCap);

        SetPhase(ERunPhase.WaveCombat);
        signalHub.Publish(new RoguelikeWaveStartedSignal(_spec));
        signalHub.Publish(new RoguelikeWaveProgressSignal(0, _spec.killTarget));
        EnableControl();
        skillController?.BeginCombat();

        EPressureState prevState = pressure.State;

        // 목표 처치 수를 채우면 OnTreeKilled가 WaveClear로 넘긴다. 게임오버·포기는 이 흐름 자체를 끊는다.
        while (ERunPhase.WaveCombat == phase)
        {
            int hits = pressure.Tick(Time.deltaTime, FieldTreeCount);

            if (pressure.State != prevState)
            {
                prevState = pressure.State;
                signalHub.Publish(new RoguelikePressureStateChangedSignal(prevState));
            }

            if (hits > 0 && null != character)
            {
                float damage = _spec.staminaDamage * hits;
                signalHub.Publish(new RoguelikePressureDamagedSignal(damage));
                character.TakeEnvironmentalStaminaDamage(damage);
            }

            yield return null;
        }

        // 별·연막·표식·돌진은 웨이브와 함께 끝난다.
        skillController?.EndCombat();

        pressure.Reset();
        signalHub.Publish(new RoguelikePressureStateChangedSignal(pressure.State));
    }

    private IEnumerator WaveClearRoutine(WaveSpec _spec)
    {
        SetPhase(ERunPhase.WaveClear);
        waveStrategy?.StopSpawning();
        signalHub.Publish(new RoguelikeWaveClearedSignal(_spec));

        // 30웨이브: 마지막 나무 줌인·슬로우 → 줌아웃이 먼저, 그 뒤 나무 정리(기획서 3-5 "런 클리어 연출").
        if (IsFinalWave(in _spec))
        {
            yield return RunClearRoutine();
            SetPhase(ERunPhase.WaveClear);
        }

        // 묘목 회수 연출과 코인 흡수는 동시에 진행한다(기획서 3-3 골드 획득 4단계).
        bool bCoinsDone = null == coinController || null == character;
        if (false == bCoinsDone)
        {
            StartCoroutine(AbsorbCoinsRoutine(() => bCoinsDone = true));
        }

        yield return productionGate.Play(new RoguelikeProductionRequest
        {
            type = ERoguelikeProduction.WaveClear,
            characterTransform = null != character ? character.transform : null,
        });

        while (false == bCoinsDone)
        {
            yield return null;
        }

        inDungeonObjectManager.ClearTreesForRun();

        // 스태미너는 자동 회복되지 않는다. 회복은 카드로만(기획서 3-2 "회복").
        float recover = modifiers.Get(ERunEffect.StaminaRecoverPerWave);
        if (recover > 0f) character?.RecoverStaminaForRun(recover);
    }

    private IEnumerator AbsorbCoinsRoutine(System.Action _onComplete)
    {
        Transform target = null != character && null != character.centerTransform ? character.centerTransform : character.transform;
        yield return coinController.AbsorbAll(target, wallet.Add);
        _onComplete?.Invoke();
    }

    private IEnumerator RunClearRoutine()
    {
        SetPhase(ERunPhase.RunClear);

        bCleared = true;
        clearTimeSeconds = playTimeSeconds;
        lastClearedWave = balance.clearWave;

        // 클리어 직후 바로 저장(기획서 3-7). 언리밋에서 게임이 꺼져도 클리어 랭킹은 남는다.
        RunRecordEntry clearSnapshot = BuildRecordEntry(ERunEndReason.GameOver, false);

        // 30웨이브 코인은 아직 바닥에 있다(흡수는 이 연출 뒤 WaveClearRoutine에서). 목표를 채운 웨이브의 코인은
        // 포기해도 지갑에 들어가므로(StopRunActivity) 지금 바닥 값까지 이 판의 획득으로 센다.
        if (null != coinController) clearSnapshot.coinsCollected += coinController.GroundValue;

        // 언리밋이 끝날 때의 SubmitRunEnd는 클리어 랭킹을 다시 넣지 않아 순위를 돌려주지 않는다. 여기서 받아 둔다.
        lastClearRank = RunRecordBook.SubmitClear(records, clearSnapshot);
        RoguelikeRecordRepository.Save(records);

        signalHub.Publish(new RoguelikeRunClearedSignal());

        yield return productionGate.Play(new RoguelikeProductionRequest
        {
            type = ERoguelikeProduction.RunClear,
            position = finalTreePosition,
            characterTransform = null != character ? character.transform : null,
        });
    }

    // // 종료

    private IEnumerator GameOverRoutine()
    {
        // 쓰러지는 애니메이션(본편과 같음)이 끝나기를 기다린다. UnitSystem이 1.5초 뒤 알려 준다.
        float elapsed = 0f;
        while (false == bCharacterDownCompleted && elapsed < maxCharacterDownWaitSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        yield return FinishRunRoutine(ERunEndReason.GameOver);
    }

    /// <summary>결과를 기록하고(결과 화면 직전) 결과 화면을 띄운 뒤, 확인을 누르면 로비로 간다. 기획서 3-7 "종료 흐름".</summary>
    private IEnumerator FinishRunRoutine(ERunEndReason _reason)
    {
        SetPhase(ERunPhase.Result);
        inputManager.PauseESCKey(true);

        RunRecordEntry entry = BuildRecordEntry(_reason, true);
        RunRecordBook.SubmitRunEnd(records, entry, _reason, out int clearRank, out lastUnlimitRank);

        // 클리어한 판의 클리어 순위는 클리어 순간(RunClearRoutine)에 받았다. SubmitRunEnd는 그 판에 -1을 돌려준다.
        if (false == entry.bCleared) lastClearRank = clearRank;
        RoguelikeRecordRepository.Save(records);
        lastRunRecord = entry;

        bRunActive = false;
        signalHub.Publish(new RoguelikeRunEndedSignal(_reason));

        bResultConfirmed = false;
        signalHub.Publish(new RoguelikeResultReadySignal());

        while (false == bResultConfirmed)
        {
            yield return null;
        }

        yield return EnterLobbyRoutine(true);
    }

    // // 필드·캐릭터

    /// <summary>_tierIndex 티어의 지형을 새로 깔고 나무 매니저를 준비한다. 필드의 나무는 전부 사라진다.</summary>
    private IEnumerator BuildFieldRoutine(int _tierIndex)
    {
        TierFieldData field = GetTierField(_tierIndex);
        currentFieldTierIndex = _tierIndex;

        waveStrategy?.StopSpawning();
        inDungeonObjectManager.ClearTreesForRun();

        List<Vector3> grassPositions = null;
        // 섬은 해상도와 무관한 월드 크기다(기준 화면에서 한 번 계산). 플레이어 시작 칸을 중심으로 한 완벽한 타원.
        Vector2 cell = environmentSystem.TileCellSize;
        // 타원 판정은 칸의 중심으로 하지만 화면에 보이는 섬은 가장자리 칸의 마름모 끝(반 칸)까지다.
        // 그 반 칸을 빼 두어야 화면 가장자리에 보이는 물이 islandEdgeWaterTiles 칸이 된다.
        float islandRadiusX = Mathf.Max(1f, islandReferenceViewSize.x * 0.5f - (islandEdgeWaterTiles + 0.5f) * cell.x);
        float islandRadiusY = Mathf.Max(1f, islandReferenceViewSize.y * 0.5f - (islandEdgeWaterTiles + 0.5f) * cell.y);

        environmentSystem.GenerateRoguelikeField(field.forestType, field.mapType, centerSafeZoneRadius, islandRadiusX, islandRadiusY,
            _positions => grassPositions = _positions);

        Vector3 center = inDungeonObjectManager.GetPlayerStartPos();
        waveStrategy?.ConfigureField(center, islandRadiusX, islandRadiusY);
        skillController?.SetField(center, islandRadiusX, islandRadiusY);
        inDungeonObjectManager.SetExternalTreeGenerationStrategy(waveStrategy, field.mapType);

        if (null == grassPositions)
        {
            Debug.LogError("[RoguelikeSystem] 필드 맵 생성 결과를 받지 못했습니다.");
            yield break;
        }

        bool bReady = false;
        inDungeonObjectManager.ReadyTrees(grassPositions, () => bReady = true);

        while (false == bReady)
        {
            yield return null;
        }

        // 전략이 지연 개방 칸까지 이미 후보에 넣었다(RoguelikeWaveTreeStrategySO.SpawnInitialTrees).
        inDungeonObjectManager.CancelDelayedGrassOpenForRun();
    }

    /// <summary>캐릭터를 필드 시작 지점에 세우고 카메라를 붙인다.</summary>
    private void PrepareCharacterForField()
    {
        if (null == character) return;

        Vector3 startPos = inDungeonObjectManager.GetPlayerStartPos();
        unitLogicManager.SetCharacterPos(startPos);
        CameraMoveController.Instance?.SetupCamera();

        // 섬은 한 화면에 맞춰 만든 크기라 카메라는 캐릭터를 따라가지 않고 섬 중앙에 고정한다.
        // 시작 위치는 칸 중심보다 반 칸(세로) 위에 잡히므로(TileMapGenerator.GetWorldPos) 섬의 기하 중심으로 내린다.
        LockCameraToIsland(startPos - new Vector3(0f, environmentSystem.TileCellSize.y * 0.5f, 0f));
    }

    /// <summary>가상 카메라의 추적 대상을 섬 중앙의 고정 지점으로 바꾸고 그 자리로 바로 옮긴다(보간 없이).</summary>
    private void LockCameraToIsland(Vector3 _center)
    {
        if (null == cameraAnchor)
        {
            cameraAnchor = new GameObject("RoguelikeCameraAnchor").transform;
            cameraAnchor.SetParent(transform, false);
        }

        cameraAnchor.position = _center;

        Unity.Cinemachine.CinemachineCamera virtualCamera = FindAnyObjectByType<Unity.Cinemachine.CinemachineCamera>();
        if (null == virtualCamera) return;

        virtualCamera.Follow = cameraAnchor;
        virtualCamera.LookAt = cameraAnchor;
        // 직전 추적 대상(캐릭터) 위치에서 미끄러져 오지 않도록 이번 프레임에 바로 붙인다.
        virtualCamera.PreviousStateIsValid = false;
    }

    /// <summary>
    /// 결과 화면 이후 캐릭터를 런 시작 전 상태로 되돌린다. 쓰러졌다면 일으키고, 쓰러질 때 걸린 입력 잠금을 푼다
    /// (Character.StaminaIsEmpty: 이동·ESC·상호작용 잠금. 상호작용은 카운터라 정확히 한 번만 푼다).
    /// </summary>
    private void RestoreCharacterAfterRun()
    {
        if (null == character) return;

        bool bWasDead = character.bDead;
        if (bWasDead)
        {
            unitLogicManager.ResetCharacterStatus();
            inputManager.PauseInteractKey(false);
        }

        // 던전 입장과 같은 초기화(쓰러짐 해제·드론·부메랑·공격 컴포넌트). 이어서 스태미너를 채운다.
        unitLogicManager.SetWhereIsCharacter(true);
        unitLogicManager.SetCharacterStaminaState(false, 0f, 0f);
        unitLogicManager.CharacterSleep();

        // 결과 화면이 건 ESC 잠금(FinishRunRoutine)을 푼다. 로비 입장 구간은 LobbyEntryESCLockOwner가 따로 막고 있다.
        inputManager.PauseESCKey(false);
    }

    /// <summary>판 단위 상태를 전부 비운다. 캐릭터 스탯도 이 판이 더한 만큼 되돌린다.</summary>
    private void ResetRunState()
    {
        bRunActive = false;
        bRunEnding = false;
        bCleared = false;

        waveStrategy?.StopSpawning();
        coinController?.DiscardAll();
        pressure.Reset();

        effectApplier.RemoveAll();
        build.Reset();
        wallet.Reset();
        shop.ResetRun(System.Environment.TickCount);

        currentWave = default;
        waveKills = 0;
        playTimeSeconds = 0f;
        clearTimeSeconds = 0f;
        treesFelled = 0;
        lastClearedWave = 0;
        lastClearedUnlimitWave = 0;
        lastClearRank = -1;
        lastUnlimitRank = -1;
        goldRemainder = 0f;
        discardedOfferIndices.Clear();
    }

    /// <summary>새 런 시작(골드 0, 보유 카드 없음, 리롤 비용 초기화). 스태미너를 밸런스 최대치로 채운다.</summary>
    private void BeginRunState()
    {
        effectApplier.SetBaseMaxStamina(balance.maxStamina);
        effectApplier.Apply(modifiers);

        unitLogicManager.SetCharacterStaminaState(false, 0f, 0f);
        unitLogicManager.CharacterSleep();

        bRunActive = true;
        signalHub.Publish(new RoguelikeRunStartedSignal());
        signalHub.Publish(new RoguelikeModifiersChangedSignal());
    }

    private void ConfigurePressure()
    {
        int cap = balance.SpawnCap;
        int threshold = Mathf.Clamp(balance.threshold + Mathf.RoundToInt(modifiers.Get(ERunEffect.ThresholdAdd)), Mathf.Max(1, balance.minThreshold), cap);
        float grace = Mathf.Clamp(balance.graceTime + modifiers.Get(ERunEffect.GraceTimeAdd), 0f, balance.maxGraceTime);
        float cooldown = Mathf.Clamp(balance.damageCooldown + modifiers.Get(ERunEffect.DamageCooldownAdd), 0.1f, balance.maxDamageCooldown);

        pressure.Configure(threshold, grace, cooldown);
        pressure.Reset();
    }

    /// <summary>조작 허용: 이동·조준·공격·ESC. 임계치 피해를 받을 수 있게 스태미너 감소 경로도 연다(시간 감소량은 0).</summary>
    private void EnableControl()
    {
        if (null == character) return;

        // 입력 모드·이동 잠금은 모달 UI와 함께 관리한다. 카드 선택 같은 전체 화면 UI가 아직 떠 있으면
        // 그 UI가 닫힐 때 Gameplay로 넘어간다(UIView_RoguelikeModalBase / RoguelikeModalInput).
        RoguelikeModalInput.SetGameplayControl(true, inputManager);
        inputManager.PauseESCKey(false);

        unitLogicManager.EnableCharacterAim();
        unitLogicManager.ActivateCharacter();
        character.SetStaminaDecrease(true);
    }

    /// <summary>조작 잠금: 이동과 공격을 막는다. 연출·카드 선택·결과 동안.</summary>
    private void DisableControl()
    {
        RoguelikeModalInput.SetGameplayControl(false, inputManager);
        character?.PauseCharacter(true);
    }

    // // 기록

    /// <summary>
    /// 지금 판의 기록을 만든다. _bRunEnd가 false면 30웨이브 클리어 순간의 스냅샷이다.
    /// 도달 웨이브: 게임오버는 싸우던 웨이브, 포기는 마지막으로 클리어한 웨이브(기획서 3-7).
    /// </summary>
    private RunRecordEntry BuildRecordEntry(ERunEndReason _reason, bool _bRunEnd)
    {
        bool bAbandoned = _bRunEnd && ERunEndReason.Abandoned == _reason;

        RunRecordEntry entry = new RunRecordEntry
        {
            recordedAtUtcTicks = System.DateTime.UtcNow.Ticks,
            bCleared = bCleared,
            bAbandoned = bAbandoned,
            playTimeSeconds = playTimeSeconds,
            clearTimeSeconds = bCleared ? clearTimeSeconds : 0f,
            treesFelled = treesFelled,
            coinsCollected = wallet.TotalEarned,
        };

        if (bCleared)
        {
            entry.reachedWave = balance.clearWave;

            if (_bRunEnd)
            {
                bool bFightingUnlimit = currentWave.bUnlimit;
                entry.reachedUnlimitWave = bAbandoned || false == bFightingUnlimit ? lastClearedUnlimitWave : currentWave.unlimitIndex;
            }
        }
        else
        {
            entry.reachedWave = bAbandoned ? lastClearedWave : Mathf.Max(1, currentWave.waveNumber);
        }

        IReadOnlyList<CardDefinitionSO> owned = build.OwnedCards;
        entry.cards = new List<RunRecordCard>(owned.Count);
        for (int i = 0; i < owned.Count; i++)
        {
            if (null == owned[i]) continue;
            entry.cards.Add(new RunRecordCard(owned[i].cardId, build.GetLevel(owned[i])));
        }

        return entry;
    }
}
