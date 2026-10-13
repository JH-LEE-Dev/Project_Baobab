#if UNITY_EDITOR
using UnityEngine;

// 에디터 전용 디버그 API. 빌드에는 포함되지 않는다(DebugSwitchBuildGuard 원칙).
// RoguelikeDebugWindow(Tools/Roguelike/Debug Window)가 이 메서드들을 부른다.
// UI가 아직 없을 때 카드 선택·결과 확인을 대신하고, 웨이브를 건너뛰어 후반 상황을 빠르게 만든다.
public partial class RoguelikeSystem
{
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
