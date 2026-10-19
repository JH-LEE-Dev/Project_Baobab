#if UNITY_EDITOR
using System.Collections;
using UnityEngine;

// 에디터 전용 디버그 API. 빌드에는 포함되지 않는다(DebugSwitchBuildGuard 원칙).
// RoguelikeDebugWindow(Tools/Roguelike/Debug Window)가 이 메서드들을 부른다.
// UI가 아직 없을 때 카드 선택·결과 확인을 대신하고, 웨이브를 건너뛰어 후반 상황을 빠르게 만든다.
public partial class RoguelikeSystem
{
    // // 지형 전환 연출 확인(F6)

    // 로비에서 이 키를 누르면 다음 티어 지형으로 바꾸며 티어 전환 연출(TierTransition)을 재생한다.
    // 본편 디버그 키(F1 시네마틱, F4 수동 저장, F9 벤치마크, ` 사운드)와 겹치지 않는 키를 쓴다.
    private const UnityEngine.InputSystem.Key DebugTierTransitionKey = UnityEngine.InputSystem.Key.F6;

    // // 오프로드 즉시 발동(F7)

    // 웨이브 전투 중에 이 키를 누르면 오프로드를 지금 빌드의 대수로 바로 한 번 내보낸다(카드가 없어도 된다).
    private const UnityEngine.InputSystem.Key DebugOffroadKey = UnityEngine.InputSystem.Key.F7;

    partial void DebugTick()
    {
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (null == keyboard) return;

        if (keyboard[DebugTierTransitionKey].wasPressedThisFrame) DebugPlayTierTransition();
        if (keyboard[DebugOffroadKey].wasPressedThisFrame) DebugFireOffroad(0);
    }

    /// <summary>
    /// 오프로드를 지금 바로 한 번 내보낸다. _count가 0 이하면 지금 빌드의 대수(I~II 1대, III~IV 2대, V 3대)를 쓴다.
    /// 오프로드 카드를 갖고 있지 않아도 되며, 피해·돌진 폭은 지금 빌드 값을 쓴다. 웨이브 전투 중에만 된다.
    /// </summary>
    public bool DebugFireOffroad(int _count)
    {
        if (ERunPhase.WaveCombat != phase || null == skillController || false == skillController.DebugFireOffroad(_count))
        {
            Debug.LogWarning($"[RoguelikeSystem] 오프로드 디버그 발동은 웨이브 전투 중에만 됩니다. (현재 {phase})");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 로비에서 다음 티어(1 → 2 → 3 → 4 → 1 순환)의 지형을 깔며 티어 전환 연출을 재생한다. 끝나면 그 지형의 로비로 돌아온다.
    /// 런 도중의 전환과 같은 순서(지형 생성 → 캐릭터 배치 → 시그널 → 연출 대기)를 그대로 탄다.
    /// 그 지형에서 게임을 시작하면 1웨이브가 1티어라 런 쪽에서 평소처럼 다시 전환된다.
    /// </summary>
    public bool DebugPlayTierTransition()
    {
        if (ERunPhase.Lobby != phase || bExitingToMainMenu || RoguelikeModalInput.HasOpenModal)
        {
            Debug.LogWarning($"[RoguelikeSystem] 지형 전환 연출 디버그는 로비에서 화면이 열려 있지 않을 때만 됩니다. (현재 {phase})");
            return false;
        }

        int tierCount = null != tierFields && tierFields.Count > 0 ? tierFields.Count : 1;
        int nextTier = (currentFieldTierIndex + 1) % tierCount;

        Debug.Log($"[RoguelikeSystem] 디버그: 티어 {currentFieldTierIndex + 1} → {nextTier + 1} 지형 전환 연출");
        StartFlow(DebugTierTransitionRoutine(nextTier));
        return true;
    }

    private IEnumerator DebugTierTransitionRoutine(int _tierIndex)
    {
        // 로비 입장과 같은 보호: 로비 물건을 치우고 조작·ESC를 막는다(이 구간의 메인메뉴 요청은 처리할 곳이 없다).
        SetPhase(ERunPhase.ReturningToLobby);
        DisableControl();
        inputManager.SetESCKeyLock(LobbyEntryESCLockOwner, true);
        RemoveLobbyObjects();

        yield return BuildFieldRoutine(_tierIndex, false);
        LockFieldCamera();

        TierFieldData field = GetTierField(_tierIndex);
        signalHub.Publish(new DecalreDungeonTypeSignal(field.mapType, field.forestType));
        signalHub.Publish(new RoguelikeTierChangedSignal(_tierIndex, field.mapType));
        PlayFieldBgm(field.mapType);

        // 런 도중 전환과 같다: 바닥 타일 파도와 TierTransition 연출을 함께 돌린다.
        tileWaveCoroutine = StartCoroutine(PlayTileWaveRoutine());

        yield return productionGate.Play(new RoguelikeProductionRequest
        {
            type = ERoguelikeProduction.TierTransition,
            tierIndex = _tierIndex,
            mapType = field.mapType,
            characterTransform = null != character ? character.transform : null,
        });

        while (tileWave.IsPlaying) yield return null;

        PlaceLobbyObjects(inDungeonObjectManager.GetPlayerStartPos());
        inputManager.SetESCKeyLock(LobbyEntryESCLockOwner, false);
        EnableControl();
        SetPhase(ERunPhase.Lobby);

        flowCoroutine = null;
    }

    public void DebugBuyOffer(int _offerIndex) => CardBuyRequested(new RoguelikeCardBuyRequestedSignal(_offerIndex));
    public void DebugSkip() => CardSkipRequested(new RoguelikeCardSkipRequestedSignal());
    public void DebugReroll() => CardRerollRequested(new RoguelikeCardRerollRequestedSignal());
    public void DebugConfirmResult() => signalHub.Publish(new RoguelikeResultConfirmedSignal());
    public void DebugLobbyAction(ERoguelikeLobbyAction _action) => OnLobbyAction(_action);

    public void DebugAddGold(long _amount)
    {
        wallet.Add(_amount);
    }

    /// <summary>제시와 무관하게 카드를 한 단계 얻는다(골드 차감 없음).</summary>
    public void DebugGrantCard(CardDefinitionSO _card)
    {
        if (null == _card || false == shop.GrantDirect(_card)) return;

        effectApplier.Apply(modifiers);
        signalHub.Publish(new RoguelikeModifiersChangedSignal());
    }

    /// <summary>필드의 나무 _count그루를 즉시 쓰러뜨린다(처치 수·골드에 그대로 반영된다).</summary>
    public void DebugKillTrees(int _count)
    {
        var trees = inDungeonObjectManager.ActiveTrees;
        for (int i = trees.Count - 1; i >= 0 && _count > 0; i--)
        {
            if (i >= trees.Count) continue;

            TreeObj tree = trees[i];
            if (null == tree || false == tree.bCanApplyDamage) continue;

            tree.TakeDamage(float.MaxValue * 0.5f);
            _count--;
        }
    }

    /// <summary>현재 웨이브를 바로 클리어한다(남은 목표 수만큼 처치한 것으로 친다).</summary>
    public void DebugCompleteWave()
    {
        if (ERunPhase.WaveCombat != phase) return;

        int remaining = Mathf.Max(0, currentWave.killTarget - waveKills);
        for (int i = 0; i < remaining && ERunPhase.WaveCombat == phase; i++)
        {
            OnTreeKilled(null, null != character ? character.transform.position : Vector3.zero);
        }
    }

    /// <summary>다음으로 진행할 웨이브 번호를 바꾼다. 현재 웨이브가 끝난 뒤 그 웨이브부터 이어진다.</summary>
    public void DebugSetNextWave(int _waveNumber)
    {
        runWaveNumber = Mathf.Max(1, _waveNumber) - 1;
    }

    public void DebugDamageStamina(float _amount)
    {
        character?.TakeEnvironmentalStaminaDamage(_amount);
    }
}
#endif
