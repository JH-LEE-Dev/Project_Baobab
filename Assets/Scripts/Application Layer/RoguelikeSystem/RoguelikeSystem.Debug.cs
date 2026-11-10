#if UNITY_EDITOR
using UnityEngine;

// 에디터 전용 디버그 API. 빌드에는 포함되지 않는다(DebugSwitchBuildGuard 원칙).
// RoguelikeDebugWindow(Tools/Roguelike/Debug Window)가 이 메서드들을 부른다.
// UI가 아직 없을 때 카드 선택·결과 확인을 대신하고, 웨이브를 건너뛰어 후반 상황을 빠르게 만든다.
public partial class RoguelikeSystem
{
    // // 지형 전환 연출 확인(F6)

    // 웨이브 전투 중에 이 키를 누르면 지금 웨이브를 클리어하고 다음 티어의 첫 웨이브로 건너뛴다. 카드 선택 뒤 런 흐름 그대로
    // 티어 전환 연출(TierTransition)이 재생된다. 본편 디버그 키(F1 시네마틱, F4 수동 저장, F9 벤치마크, ` 사운드)와 겹치지 않는 키를 쓴다.
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
    /// 오프로드를 지금 바로 한 번 내보낸다. _count가 0 이하면 지금 빌드의 대수(I~II 1대, III~IV 2대, V~VI 4대, MAX 6대)를 쓴다.
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
    /// 웨이브 전투 중에 지금 웨이브를 클리어하고 다음 티어의 첫 웨이브로 건너뛴다(마지막 티어에서는 하지 않는다).
    /// 다음 웨이브의 카드 선택이 끝나면 런 도중의 전환과 같은 순서(지형 생성 → 시그널 → 타일 파도 · TierTransition 연출)로 지형이 바뀐다.
    /// </summary>
    public bool DebugPlayTierTransition()
    {
        if (ERunPhase.WaveCombat != phase || null == balance || null == balance.tiers || RoguelikeModalInput.HasOpenModal)
        {
            Debug.LogWarning($"[RoguelikeSystem] 지형 전환 연출 디버그는 웨이브 전투 중 화면이 열려 있지 않을 때만 됩니다. (현재 {phase})");
            return false;
        }

        int nextTier = currentWave.tierIndex + 1;
        if (nextTier >= balance.tiers.Count)
        {
            Debug.LogWarning("[RoguelikeSystem] 이미 마지막 티어입니다. 지형 전환 연출 디버그는 다음 티어가 있을 때만 됩니다.");
            return false;
        }

        int nextTierStartWave = Mathf.Max(1, balance.GetTier(nextTier).startWave);
        Debug.Log($"[RoguelikeSystem] 디버그: 티어 {currentWave.tierIndex + 1} → {nextTier + 1} 지형 전환 (웨이브 {nextTierStartWave}로 건너뜀)");

        DebugSetNextWave(nextTierStartWave);
        DebugCompleteWave();
        return true;
    }

    public void DebugBuyOffer(int _offerIndex) => CardBuyRequested(new RoguelikeCardBuyRequestedSignal(_offerIndex));
    public void DebugReroll() => CardRerollRequested(new RoguelikeCardRerollRequestedSignal());

    /// <summary>결과 화면의 버튼과 같은 요청을 보낸다(다시 도전 / 메인메뉴). 결과 화면이 떠 있을 때만 받는다.</summary>
    public void DebugResultChoice(ERoguelikeResultChoice _choice) => signalHub.Publish(new RoguelikeResultConfirmedSignal(_choice));

    /// <summary>제시와 무관하게 카드를 한 단계 얻는다.</summary>
    public void DebugGrantCard(CardDefinitionSO _card)
    {
        if (null == _card || false == shop.GrantDirect(_card)) return;

        effectApplier.Apply(modifiers);
        signalHub.Publish(new RoguelikeModifiersChangedSignal());
    }

    /// <summary>필드의 나무 _count그루를 즉시 쓰러뜨린다(처치 수에 그대로 반영된다).</summary>
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
