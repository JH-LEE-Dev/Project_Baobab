using UnityEngine;

/// <summary>
/// 제재소가 증설되어 그 자리에 가공 라인(커터 등)이 들어서면 치워져야 하는 마을 나무에 붙인다.
/// 활성 가공 라인 수가 removeAtLineCount 이상이면 TownObjectManager가 이 나무를 끄고 갱신 목록에서 뺀다.
/// (라인 수가 다시 줄어들면 되살린다)
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TreeObj))]
public class TownTreeRemovedByLineExpansion : MonoBehaviour
{
    [SerializeField, Min(2)] private int removeAtLineCount = 3;

    public bool ShouldBeRemoved(int _activeLineCount)
    {
        return _activeLineCount >= removeAtLineCount;
    }
}
