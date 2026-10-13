using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// RoguelikeSystem의 흐름 코루틴. 기획서 7장 상태 표의 "다음 상태"를 그대로 이어 붙였다.
// 한 번에 하나의 흐름만 돈다(StartFlow가 이전 흐름을 끊는다). 게임오버·포기는 진행 중인 흐름을 끊고 새 흐름을 시작한다.
public partial class RoguelikeSystem
{
    // // 로비

    /// <summary>
    /// 로비로 들어간다(기획서 4-1 진입 연출). 결과 화면에서 오면(_bFromResult) 런 상태를 전부 비우고 캐릭터를 되살린다.
    ///   1~2. 디스토션 → 암전: 메인메뉴에서 오면 메인메뉴(UIView_MainMenu)가 이미 걸고 검은 화면인 채 씬이 넘어온다.
    ///        결과 화면에서 오면 여기서 결과 화면 위에 건다.
    ///   (암전 아래) 런 상태 정리, 1티어 지형 생성. 화면 안은 물만 깔린 바다로 되돌려 둔다.
    ///   암전이 걷힌다 - 물 타일은 깔린 채로 보인다.
    ///   3~5. 바닥 타일 파도 → 배경(해안선) 퍼즐 조립 → N초 안에 완성.
    ///   6. 플레이어 낙하.  7. 조작 가능.
    /// </summary>
    private IEnumerator EnterLobbyRoutine(bool _bFromResult)
    {
        SetPhase(ERunPhase.ReturningToLobby);
        DisableControl();

        // Lobby가 될 때까지 ESC를 막는다. 이 구간의 메인메뉴 요청은 처리할 곳이 없어 무시되는데(GoToMainMenuRequested),
        // ESC 쪽은 그 전에 메뉴를 닫고 이동 잠금을 풀어 버려 연출 중에 캐릭터가 움직인다.
        inputManager.SetESCKeyLock(LobbyEntryESCLockOwner, true);

        RoguelikeScreenTransition screen = RoguelikeScreenTransition.Instance;
        if (_bFromResult)
        {
            // 결과 화면에서 확인을 누르면 결과 화면에서 바로 디스토션이 걸린다(메인메뉴를 거치지 않는다). 이펙트 담당의
            // ResultToLobby 연출은 디스토션과 함께 돈다.
            bool bGateDone = false;
            StartCoroutine(PlayProductionThen(new RoguelikeProductionRequest
            {
                type = ERoguelikeProduction.ResultToLobby,
                characterTransform = null != character ? character.transform : null,
            }, () => bGateDone = true));

            yield return screen.DistortToBlack(screenTransitionSettings);
            while (false == bGateDone) yield return null;

            // 메인메뉴에서 들어올 때처럼(Initialize) 검은 화면 아래에서 HUD를 내려 두고, 조작이 풀릴 때 올린다(PopupUIUpSignal).
            signalHub.Publish(new PopupUIDownSignal());
        }
        else
        {
            // 메인메뉴가 검은 화면을 덮은 채 넘어온다. 다른 경로로 들어와 덮여 있지 않아도 지형을 까는 모습은 보이지 않게 덮는다.
            screen.CoverBlack();
        }

        signalHub.Publish(new RoguelikeEntryScreenCoveredSignal());
        float coveredAt = Time.realtimeSinceStartup;

        ResetRunState();
        RestoreCharacterAfterRun();

        // 화면 안은 물만 깔린 바다로 되돌려 둔다(바닥·해안선은 파도로 나타난다).
        yield return BuildFieldRoutine(0, true);

        // 캐릭터는 낙하 전까지 화면 밖(섬 위쪽)에 둔다 - 카메라는 섬 중앙에 고정이라 따라가지 않는다.
        PrepareCharacterForField();
        Vector3 startPos = inDungeonObjectManager.GetPlayerStartPos();
        if (null != character) unitLogicManager.SetCharacterPos(startPos + characterHiddenOffset);

        TierFieldData lobbyField = GetTierField(0);
        signalHub.Publish(new DecalreDungeonTypeSignal(lobbyField.mapType, lobbyField.forestType));

        if (false == _bFromResult)
        {
            PrepareSceneUnderBlack();
        }

        // 지역 BGM은 화면이 드러나는 순간부터. 결과 화면에서 돌아오면 1티어 곡으로 돌아간다.
        PlayFieldBgm(lobbyField.mapType);

        // 검은 화면 아래에서 HUD 내림 모션(본편 연출) 같은 정리가 끝날 만큼은 덮어 둔다.
        while (Time.realtimeSinceStartup - coveredAt < screenTransitionSettings.minBlackHoldSeconds) yield return null;

        // 암전이 걷힌다. 물 타일은 깔린 채로 보인다.
        signalHub.Publish(new RoguelikeEntryScreenRevealStartedSignal());
        yield return screen.FadeFromBlack(screenTransitionSettings.fadeInSeconds);

        // 3~5단계: 바닥 타일 파도 → 배경 퍼즐 조립(N초 이내 완성). 이펙트 담당의 LobbyEntry 연출은 파도와 함께 돈다.
        tileWaveCoroutine = StartCoroutine(PlayTileWaveRoutine());

        yield return productionGate.Play(new RoguelikeProductionRequest
        {
            type = ERoguelikeProduction.LobbyEntry,
            bFromResult = _bFromResult,
            characterTransform = null != character ? character.transform : null,
            mapType = lobbyField.mapType,
        });

        while (tileWave.IsPlaying) yield return null;

        // 6단계: 로비 물건을 놓고 하늘에서 플레이어가 똑 떨어진다.
        PlaceLobbyObjects(startPos);
        yield return CharacterDropRoutine(startPos);

        // 7단계: 조작 가능.
        signalHub.Publish(new PopupUIUpSignal());
        inputManager.SetESCKeyLock(LobbyEntryESCLockOwner, false);
        EnableControl();
        SetPhase(ERunPhase.Lobby);

        flowCoroutine = null;
    }

    /// <summary>
    /// 메인메뉴에서 막 들어온 경우. 본편 메인메뉴 → 마을 진입의 하강 연출이 하던 일 중 화면과 무관한 것을 한다:
    /// 씬 로드 때 0으로 내려간 3D 효과음 볼륨을 화면이 드러나는 동안 되살린다. 메인메뉴 커튼은 메인메뉴가 암전 아래에서
    /// 이미 걷어 두었다(돌아갈 때 GoToMainMenuCurtainRevealEvent가 같은 인스턴스를 다시 내린다).
    /// </summary>
    private void PrepareSceneUnderBlack()
    {
        Sound.RampProduction3DVolume(1f, Mathf.Max(0.01f, screenTransitionSettings.fadeInSeconds));
    }

    /// <summary>연출 게이트를 재생하고 끝나면 _onComplete를 부른다(다른 연출과 동시에 돌릴 때).</summary>
    private IEnumerator PlayProductionThen(RoguelikeProductionRequest _request, System.Action _onComplete)
    {
        yield return productionGate.Play(_request);
        _onComplete?.Invoke();
    }

    /// <summary>
    /// 로비에서 메인메뉴로(ESC → 메인메뉴). 본편 마을 → 메인메뉴 이탈(TownSystem.GoToMainMenuRequested →
    /// TownProductionManager.StartGoToMainMenu)과 같은 순서다: BGM 페이드아웃 → 메인메뉴 커튼 → 구름 → HUD 내림 →
    /// 카메라 상승(상승 효과음). 상승이 끝나면 메인메뉴로 넘어간다.
    /// </summary>
    private IEnumerator ExitToMainMenuRoutine()
    {
        bExitingToMainMenu = true;
        DisableControl();
        inputManager.PauseESCKey(true); // 본편과 같이 메인메뉴 도착(BootStrap.SetupMainMenuScene)까지 막는다
        RemoveLobbyObjects();

        float ascendSeconds = null != skyCameraProductionManager ? skyCameraProductionManager.MoveDuration : exitToMainMenuSeconds;
        Sound.FadeOutBGM(ascendSeconds);
        bFieldBgmPlaying = false;

        GoToMainMenuCurtainRevealEvent?.Invoke();
        signalHub.Publish(new StartSkyProductionSignal(true));
        signalHub.Publish(new PopupUIDownSignal());

        if (null != skyCameraProductionManager && null != cameraAnchor)
        {
            bool bAscended = false;
            skyCameraProductionManager.PlayAscendOutForRun(cameraAnchor, () => bAscended = true);

            float elapsed = 0f;
            float limit = Mathf.Max(exitToMainMenuSeconds, ascendSeconds + 2f);
            while (false == bAscended && elapsed < limit)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // 완료를 못 받고 넘어간다. 남은 일회성 콜백이 나중의 상승 완료를 가로채지 않게 거둔다.
            if (false == bAscended) skyCameraProductionManager.CancelAscendOutForRun();
        }
        else
        {
            yield return new WaitForSecondsRealtime(exitToMainMenuSeconds);
        }

        signalHub.Publish(new GoToMainMenuSignal());
    }

    /// <summary>
    /// 지형의 지역 BGM을 튼다(본편 InDungeonSystem.PlayDungeonBGM과 같은 대지역 → 곡 대응). 같은 곡이 이미 흐르면
    /// 다시 틀지 않는다(처음부터 다시 시작하지 않게).
    /// </summary>
    private void PlayFieldBgm(MapType _mapType)
    {
        SoundID bgm;
        switch (_mapType)
        {
            case MapType.WideGreenForest: bgm = SoundID.Stage1BGM; break;
            case MapType.FluffySporeForest: bgm = SoundID.Stage2BGM; break;
            case MapType.StarrootForest: bgm = SoundID.Stage3BGM; break;
            case MapType.MagmaForest: bgm = SoundID.Stage4BGM; break;
            default: return;
        }

        if (bFieldBgmPlaying && currentFieldBgm == bgm) return;

        bFieldBgmPlaying = true;
        currentFieldBgm = bgm;
        Sound.PlayBGM(bgm);
    }

    /// <summary>
    /// 플레이어 낙하(기획서 4-1 6단계 "하늘에서 플레이어가 똑 하고 떨어진다"). 시작 칸 위 화면 밖에서 시작 칸으로 떨어진다.
    /// 카메라는 섬 중앙 고정이라 따라가지 않는다. 떨어지는 동안은 바닥 그림자를 끄고 착지할 때 켠다.
    /// 착지 먼지·효과음은 RoguelikeCharacterLandedSignal에 이펙트·사운드 담당이 붙인다.
    /// </summary>
    private IEnumerator CharacterDropRoutine(Vector3 _landPos)
    {
        if (null == character) yield break;

        Transform target = character.transform;
        Vector3 from = _landPos + new Vector3(0f, Mathf.Max(0f, characterDropHeight), 0f);
        float duration = Mathf.Max(0.01f, characterDropSeconds);

        // 본편 메인메뉴 → 던전 입장처럼 공격 인디케이터·조준을 꺼 둔다. 착지 때 인디케이터만 아래를 향해 켜고, 조준은 EnableControl이 켠다.
        character.DisableAttackComponent();
        character.DisableShadow();
        target.position = from;
        signalHub.Publish(new RoguelikeCharacterDropStartedSignal(from, _landPos, duration));

        float elapsed = 0f;
        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            target.position = Vector3.LerpUnclamped(from, _landPos, t * t);
        }

        unitLogicManager.SetCharacterPos(_landPos);
        character.EnableShadow();

        // 착지하면 공격 인디케이터가 캐릭터 정면(아래)을 향해 나타난다. 마우스를 따라가는 건 조작이 풀릴 때(EnableControl)부터다.
        character.ShowAttackIndicatorFacingDownForRun();
        signalHub.Publish(new RoguelikeCharacterLandedSignal(_landPos));
    }

    /// <summary>바닥 타일 파도를 재생하고 시작·완료 신호를 보낸다(BuildFieldRoutine이 시작 상태를 깔아 둔 뒤).</summary>
    private IEnumerator PlayTileWaveRoutine()
    {
        bool bEntryWave = tileWave.IsEntryWave;
        signalHub.Publish(new RoguelikeTileWaveStartedSignal(bEntryWave));
        yield return tileWave.Play();
        signalHub.Publish(new RoguelikeTileWaveCompletedSignal(bEntryWave));
        tileWaveCoroutine = null;
    }

    /// <summary>흐름이 끊길 때(포기 등). 도중이던 파도를 멈추고 남은 칸을 바로 새 지형으로 채운다.</summary>
    private void StopTileWave()
    {
        bool bWasPlaying = null != tileWaveCoroutine;
        if (bWasPlaying)
        {
            StopCoroutine(tileWaveCoroutine);
            tileWaveCoroutine = null;
        }

        tileWave.FinishImmediately();

        // 시작 신호를 받은 쪽(이펙트 담당)이 끝 신호를 기다리지 않게 짝을 맞춘다. 파도는 방금 새 지형으로 다 채워졌다.
        if (bWasPlaying) signalHub.Publish(new RoguelikeTileWaveCompletedSignal(tileWave.IsEntryWave));
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

        yield return BuildFieldRoutine(_spec.tierIndex, false);
        LockFieldCamera();

        TierFieldData field = GetTierField(_spec.tierIndex);
        signalHub.Publish(new DecalreDungeonTypeSignal(field.mapType, field.forestType));
        signalHub.Publish(new RoguelikeTierChangedSignal(_spec.tierIndex, field.mapType));

        // 지형이 바뀌면 그 숲의 지역 BGM으로 바꾼다(본편 숲마다 다른 BGM과 같다).
        PlayFieldBgm(field.mapType);

        // 지형 전환 = 바닥 타일 파도(기획서 3-4, 진입과 공용). 이펙트 담당의 TierTransition 연출은 파도와 함께 돈다.
        tileWaveCoroutine = StartCoroutine(PlayTileWaveRoutine());

        yield return productionGate.Play(new RoguelikeProductionRequest
        {
            type = ERoguelikeProduction.TierTransition,
            tierIndex = _spec.tierIndex,
            mapType = field.mapType,
            characterTransform = null != character ? character.transform : null,
        });

        while (tileWave.IsPlaying) yield return null;
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

        // 별·연막·표식·돌진은 웨이브와 함께 끝난다. 스텔라+드론의 별은 노바 때까지 남는다(EndCombat).
        skillController?.EndCombat(true);

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
            RevertFieldTreesToSapling();
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

        // 묘목이 다 사라진 뒤에 회수한다(회수하면 그 자리에서 바로 없어진다).
        while (Time.time < saplingVanishEndTime) yield return null;

        inDungeonObjectManager.ClearTreesForRun();
        // 본편 나무 정리가 땅 별·점선 그림까지 거둔다. 노바 때까지 남는 별(스텔라+드론)의 그림을 다시 그린다.
        skillController?.RestoreKeptStarVisuals();

        // 스태미너는 자동 회복되지 않는다. 회복은 카드로만(기획서 3-2 "회복").
        float recover = modifiers.Get(ERunEffect.StaminaRecoverPerWave);
        if (recover > 0f) character?.RecoverStaminaForRun(recover);
    }

    /// <summary>
    /// 바닥 코인을 코인 HUD 쪽으로 빨아들인다(기획서 3-3 골드 획득 5단계 "코인 HUD 쪽으로 날아가는 애니메이션과 함께 보유 골드가 오른다").
    /// 코인은 월드 물체라 HUD 화면 지점을 월드로 옮긴 고정점으로 날아간다. 흡수 동안 매 프레임 다시 맞춘다(해상도 변경 대비).
    /// </summary>
    private IEnumerator AbsorbCoinsRoutine(System.Action _onComplete)
    {
        if (null == coinHudTarget)
        {
            coinHudTarget = new GameObject("RoguelikeCoinHudTarget").transform;
            coinHudTarget.SetParent(transform, false);
        }

        UpdateCoinHudTarget();

        bool bDone = false;
        StartCoroutine(AbsorbAllThen(() => bDone = true));
        while (false == bDone)
        {
            UpdateCoinHudTarget();
            yield return null;
        }

        _onComplete?.Invoke();
    }

    private IEnumerator AbsorbAllThen(System.Action _onComplete)
    {
        yield return coinController.AbsorbAll(coinHudTarget, wallet.Add);
        _onComplete?.Invoke();
    }

    private void UpdateCoinHudTarget()
    {
        Camera cam = Camera.main;
        if (null == cam || null == coinHudTarget) return;

        Vector2 screen;
        if (false == uiCoordinator.TryGetCoinHudScreenPoint(out screen))
        {
            screen = new Vector2(coinHudFallbackViewport.x * Screen.width, coinHudFallbackViewport.y * Screen.height);
        }

        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Mathf.Abs(cam.transform.position.z)));
        world.z = 0f;
        coinHudTarget.position = world;
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

        // 마지막 나무 줌인 + 슬로우 부서짐 → 줌아웃(기획서 3-5). 이펙트 담당의 RunClear 연출은 함께 돈다.
        DisableControl();
        bool bGateDone = false;
        StartCoroutine(PlayProductionThen(new RoguelikeProductionRequest
        {
            type = ERoguelikeProduction.RunClear,
            position = finalTreePosition,
            characterTransform = null != character ? character.transform : null,
        }, () => bGateDone = true));

        yield return RunClearCinematicRoutine();
        while (false == bGateDone) yield return null;
    }

    /// <summary>
    /// 마지막 나무로 빠르게 다가가 확대하고(줌인) 시간을 늦춰 그 나무가 천천히 부서지는 모습을 보여 준 뒤 섬 전체로 돌아간다(줌아웃).
    /// 확대는 본편 카메라 줌(CameraMoveController.ZoomCamera - 픽셀 퍼펙트 보정 처리 포함)을 쓰고, 다가가기는 카메라 고정점을 옮긴다.
    /// 줌 트윈은 게임 시간으로 흐르므로 슬로우 구간 길이에 시간 배율을 곱해 넘긴다. 연출 동안 ESC를 막는다(일시정지가 시간 배율을 덮어쓴다).
    /// </summary>
    private IEnumerator RunClearCinematicRoutine()
    {
        if (null == cameraAnchor) yield break;

        inputManager.SetESCKeyLock(RunClearESCLockOwner, true);

        Vector3 islandCenter = cameraAnchor.position;
        Vector3 target = new Vector3(finalTreePosition.x, finalTreePosition.y, islandCenter.z);
        float slow = Mathf.Clamp(runClearSlowTimeScale, 0.05f, 1f);
        float zoomIn = Mathf.Max(0.01f, runClearZoomInSeconds);
        float hold = Mathf.Max(0f, runClearSlowSeconds);
        float zoomOut = Mathf.Max(0.01f, runClearZoomOutSeconds);

        bRunClearSlowMo = true;
        Time.timeScale = slow;
        CameraMoveController.Instance?.ZoomCamera(Mathf.Max(1f, runClearZoomMultiplier), zoomIn * slow, hold * slow, zoomOut);

        float elapsed = 0f;
        while (elapsed < zoomIn + hold)
        {
            elapsed += Time.unscaledDeltaTime;
            cameraAnchor.position = Vector3.Lerp(islandCenter, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / zoomIn)));
            yield return null;
        }

        // 줌아웃은 제 속도로.
        Time.timeScale = 1f;
        bRunClearSlowMo = false;

        elapsed = 0f;
        while (elapsed < zoomOut)
        {
            elapsed += Time.unscaledDeltaTime;
            cameraAnchor.position = Vector3.Lerp(target, islandCenter, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / zoomOut)));
            yield return null;
        }

        cameraAnchor.position = islandCenter;
        EndRunClearCinematic();
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

    /// <summary>
    /// _tierIndex 티어의 지형을 새로 깔고 나무 매니저를 준비한다. 필드의 나무는 전부 사라진다.
    /// 새 지형은 바로 보이지 않고 파도 시작 상태로 되돌려 두며, 호출한 쪽이 PlayTileWaveRoutine으로 파도를 재생해 드러낸다.
    /// _bEntryWave면 로비 진입(물만 깔린 바다에서 육지가 깔린다), 아니면 티어 전환(직전 지형이 칸마다 새 지형으로 바뀐다).
    /// </summary>
    private IEnumerator BuildFieldRoutine(int _tierIndex, bool _bEntryWave)
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

        // 지형 전환 파도가 칸마다 직전 지형에서 새 지형으로 바꿀 수 있게, 깔기 전에 화면 안 타일을 떠 둔다.
        // 돌던 파도가 있으면 코루틴까지 멈춘다(끝내기만 하면 남은 루프가 새 지형 위에 직전 파도의 칸을 다시 깐다).
        StopTileWave();
        if (false == _bEntryWave) tileWave.CaptureBefore();

        environmentSystem.GenerateRoguelikeField(field.forestType, field.mapType, centerSafeZoneRadius, islandRadiusX, islandRadiusY,
            _positions => grassPositions = _positions);

        // 노바 때까지 남는 별(스텔라+드론)은 새 지형의 육지 위에 있는 것만 남기고, 본편 나무 정리가 거둔 그림을 다시 그린다.
        if (null != skillController)
        {
            ITilemapDataProvider tiles = environmentSystem.tilemapDataProvider;
            skillController.DropKeptStarsOffLand(_pos => null == tiles || tiles.IsWalkable(tiles.WorldToCell(_pos)));
            skillController.RestoreKeptStarVisuals();
        }

        Vector3 center = inDungeonObjectManager.GetPlayerStartPos();

        // 같은 프레임에 시작 상태로 되돌린다(새 지형이 한 프레임도 통째로 보이지 않게). 섬 중심은 시작 칸보다 반 칸 아래다.
        Vector3 islandCenter = center - new Vector3(0f, cell.y * 0.5f, 0f);
        tileWave.PrepareAfterGenerate(islandCenter, islandReferenceViewSize * 0.5f, _bEntryWave);
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

        unitLogicManager.SetCharacterPos(inDungeonObjectManager.GetPlayerStartPos());
        LockFieldCamera();
    }

    /// <summary>
    /// 카메라만 섬 중앙에 고정한다(캐릭터는 그 자리 그대로). 티어 전환이 쓴다 - 기획서 3-4에는 캐릭터를 옮기라는 말이 없고
    /// 섬 모양이 티어마다 같아 서 있던 칸이 그대로 육지다. 지형만 파도로 바뀌고 캐릭터는 제자리에 있다.
    /// </summary>
    private void LockFieldCamera()
    {
        CameraMoveController.Instance?.SetupCamera();

        // 섬은 한 화면에 맞춰 만든 크기라 카메라는 캐릭터를 따라가지 않고 섬 중앙에 고정한다.
        // 시작 위치는 칸 중심보다 반 칸(세로) 위에 잡히므로(TileMapGenerator.GetWorldPos) 섬의 기하 중심으로 내린다.
        LockCameraToIsland(inDungeonObjectManager.GetPlayerStartPos() - new Vector3(0f, environmentSystem.TileCellSize.y * 0.5f, 0f));
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
        // 착지 때 먼저 띄운 인디케이터는 다시 페이드하지 않는다(깜빡임 방지). 꺼져 있으면 평소처럼 켠다.
        character.ActivateCharacter(true);
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
