using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 켜져 있는 TreeStarMarkAnimator 전부를 한 번의 Update에서 진행시킨다(표식마다 Update를 받는 호출 비용을 없앤다).
/// 각자 Update를 받던 때와 같은 프레임에 같은 값으로 진행되도록 유니티의 Update 등록 규칙을 그대로 따른다.
/// - 유니티는 Update 단계를 시작할 때만 새로 켜진 컴포넌트를 목록에 넣는다. 그래서 Update 단계 전에 켜지면 그 프레임부터,
///   Update 단계 중이나 그 뒤(코루틴, LateUpdate)에 켜지면 다음 프레임부터 Update를 받는다.
/// - 드라이버는 모든 스크립트보다 먼저(실행 순서 최하위) Update를 돌고, 그 시작에서만 대기 목록을 합친다. 따라서 위 규칙과 같다.
/// - 꺼지면 바로 목록에서 빠진다(유니티도 꺼진 컴포넌트의 Update는 부르지 않는다).
/// - 표식끼리는 서로 영향이 없어 도는 순서는 결과와 무관하다. Time.deltaTime은 한 프레임 안에서 같다.
/// </summary>
[DefaultExecutionOrder(-32100)]
public sealed class TreeStarMarkAnimatorDriver : MonoBehaviour
{
    private static TreeStarMarkAnimatorDriver instance;
    private static readonly List<TreeStarMarkAnimator> active = new List<TreeStarMarkAnimator>(1024);
    private static readonly List<TreeStarMarkAnimator> pending = new List<TreeStarMarkAnimator>(256);

    internal static void Register(TreeStarMarkAnimator _animator)
    {
        if (0 <= _animator.driverIndex) return;

        EnsureInstance();
        _animator.bDriverPending = true;
        _animator.driverIndex = pending.Count;
        pending.Add(_animator);
    }

    internal static void Unregister(TreeStarMarkAnimator _animator)
    {
        int index = _animator.driverIndex;
        if (0 > index) return;

        List<TreeStarMarkAnimator> list = true == _animator.bDriverPending ? pending : active;
        int last = list.Count - 1;
        TreeStarMarkAnimator moved = list[last];
        list[index] = moved;
        moved.driverIndex = index;
        list.RemoveAt(last);

        _animator.driverIndex = -1;
        _animator.bDriverPending = false;
    }

    private static void EnsureInstance()
    {
        if (null != instance) return;

        GameObject go = new GameObject("[TreeStarMarkAnimatorDriver]");
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        instance = go.AddComponent<TreeStarMarkAnimatorDriver>();
    }

    private void Update()
    {
        // Update 단계 시작: 지난 Update 이후 켜진 표식을 합친다.
        for (int i = 0; i < pending.Count; i++)
        {
            TreeStarMarkAnimator animator = pending[i];
            animator.bDriverPending = false;
            animator.driverIndex = active.Count;
            active.Add(animator);
        }
        pending.Clear();

        float deltaTime = Time.deltaTime;
        for (int i = 0; i < active.Count; i++)
        {
            active[i].Tick(deltaTime);
        }
    }

    private void OnDestroy()
    {
        if (this != instance) return;

        instance = null;
        ResetLists();
    }

    private static void ResetLists()
    {
        for (int i = 0; i < active.Count; i++)
        {
            if (null == active[i]) continue;
            active[i].driverIndex = -1;
            active[i].bDriverPending = false;
        }

        for (int i = 0; i < pending.Count; i++)
        {
            if (null == pending[i]) continue;
            pending[i].driverIndex = -1;
            pending[i].bDriverPending = false;
        }

        active.Clear();
        pending.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        active.Clear();
        pending.Clear();
    }
}
