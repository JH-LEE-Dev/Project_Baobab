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
///
/// 표식의 프레임 타이머는 켜질 때 0에서 시작해 매 프레임 같은 deltaTime을 더하고 frameRate로만 넘긴다. 그래서 같은 Update에서
/// 합쳐진 같은 frameRate의 표식들은 타이머가 늘 같은 값이고 넘기는 프레임 수도 같다. 이런 표식들을 한 묶음(Bucket)으로 두고
/// 타이머를 묶음에서 한 번만 같은 식으로 계산한 뒤, 넘길 프레임이 있을 때만 각 표식의 그림을 넘긴다.
/// </summary>
[DefaultExecutionOrder(-32100)]
public sealed class TreeStarMarkAnimatorDriver : MonoBehaviour
{
    /// <summary>같은 Update에서 합쳐진 같은 frameRate의 표식들. 타이머는 각 표식이 따로 셀 때의 frameTimer와 같은 값이다.</summary>
    internal sealed class Bucket
    {
        public float frameTimer;
        public float frameRate;
        public int index = -1;
        public readonly List<TreeStarMarkAnimator> members = new List<TreeStarMarkAnimator>(64);
    }

    private static TreeStarMarkAnimatorDriver instance;
    private static readonly List<Bucket> buckets = new List<Bucket>(16);
    private static readonly Stack<Bucket> freeBuckets = new Stack<Bucket>();
    private static readonly List<TreeStarMarkAnimator> pending = new List<TreeStarMarkAnimator>(256);
    // 이번 Update에서 만든 묶음(합치는 동안만 쓴다).
    private static readonly List<Bucket> mergeBuckets = new List<Bucket>(4);

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

        Bucket bucket = _animator.driverBucket;
        List<TreeStarMarkAnimator> list = true == _animator.bDriverPending ? pending : bucket.members;
        int last = list.Count - 1;
        TreeStarMarkAnimator moved = list[last];
        list[index] = moved;
        moved.driverIndex = index;
        list.RemoveAt(last);

        if (false == _animator.bDriverPending && 0 == bucket.members.Count) ReleaseBucket(bucket);

        _animator.driverIndex = -1;
        _animator.bDriverPending = false;
        _animator.driverBucket = null;
    }

    private static void ReleaseBucket(Bucket _bucket)
    {
        int last = buckets.Count - 1;
        Bucket moved = buckets[last];
        buckets[_bucket.index] = moved;
        moved.index = _bucket.index;
        buckets.RemoveAt(last);

        _bucket.index = -1;
        freeBuckets.Push(_bucket);
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
        // Update 단계 시작: 지난 Update 이후 켜진 표식을 합친다. 켜질 때 타이머가 0이 되므로 새 묶음도 0에서 시작한다.
        if (0 < pending.Count)
        {
            for (int i = 0; i < pending.Count; i++)
            {
                TreeStarMarkAnimator animator = pending[i];
                Bucket bucket = FindMergeBucket(animator.FrameRate);
                animator.bDriverPending = false;
                animator.driverBucket = bucket;
                animator.driverIndex = bucket.members.Count;
                bucket.members.Add(animator);
            }
            pending.Clear();
            mergeBuckets.Clear();
        }

        float deltaTime = Time.deltaTime;
        for (int b = 0; b < buckets.Count; b++)
        {
            Bucket bucket = buckets[b];

            // 원래 TreeStarMarkAnimator의 Update 본문과 같은 식의 타이머 계산
            bucket.frameTimer += deltaTime;
            float frameDuration = 1f / Mathf.Max(0.01f, bucket.frameRate);

            int advance = 0;
            while (bucket.frameTimer >= frameDuration)
            {
                bucket.frameTimer -= frameDuration;
                advance++;
            }

            if (0 == advance) continue;

            List<TreeStarMarkAnimator> members = bucket.members;
            for (int i = 0; i < members.Count; i++)
            {
                members[i].Advance(advance);
            }
        }
    }

    private static Bucket FindMergeBucket(float _frameRate)
    {
        for (int i = 0; i < mergeBuckets.Count; i++)
        {
            if (mergeBuckets[i].frameRate == _frameRate) return mergeBuckets[i];
        }

        Bucket bucket = 0 < freeBuckets.Count ? freeBuckets.Pop() : new Bucket();
        bucket.frameTimer = 0f;
        bucket.frameRate = _frameRate;
        bucket.index = buckets.Count;
        buckets.Add(bucket);
        mergeBuckets.Add(bucket);
        return bucket;
    }

    private void OnDestroy()
    {
        if (this != instance) return;

        instance = null;
        ResetLists();
    }

    private static void ResetLists()
    {
        for (int b = 0; b < buckets.Count; b++)
        {
            List<TreeStarMarkAnimator> members = buckets[b].members;
            for (int i = 0; i < members.Count; i++)
            {
                if (null == members[i]) continue;
                ResetAnimator(members[i]);
            }
        }

        for (int i = 0; i < pending.Count; i++)
        {
            if (null == pending[i]) continue;
            ResetAnimator(pending[i]);
        }

        ClearAll();
    }

    private static void ResetAnimator(TreeStarMarkAnimator _animator)
    {
        _animator.driverIndex = -1;
        _animator.bDriverPending = false;
        _animator.driverBucket = null;
    }

    private static void ClearAll()
    {
        for (int b = 0; b < buckets.Count; b++)
        {
            buckets[b].members.Clear();
            buckets[b].index = -1;
        }
        buckets.Clear();
        freeBuckets.Clear();
        pending.Clear();
        mergeBuckets.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        ClearAll();
    }
}
