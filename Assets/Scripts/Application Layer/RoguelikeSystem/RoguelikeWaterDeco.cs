using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>섬 둘레 물 장식 수치.</summary>
[Serializable]
public sealed class RoguelikeWaterDecoSettings
{
    [Tooltip("화면 안 깊은 물(둘레 8칸에 육지가 없는 물) 칸 중 장식을 띄우는 비율(0~1). 본편은 바다 0.01, 섬 안 웅덩이 0.1.")]
    [Range(0f, 1f)] public float density = 0.08f;
    [Tooltip("장식 둘레로 이 칸 수(가로·세로 칸 차이) 안에는 다른 장식을 놓지 않는다. 0이면 바로 옆 칸에도 놓는다.")]
    public int minGapCells = 2;
    [Tooltip("파도가 지나갈 때 새 장식이 물 위로 톡 솟아 제 크기가 되는 시간(초). 살짝 넘쳤다가 돌아온다.")]
    public float appearSeconds = 0.4f;
    [Tooltip("티어 전환에서 직전 장식이 물속으로 오므라들며 사라지는 시간(초).")]
    public float disappearSeconds = 0.25f;
}

/// <summary>
/// 섬 둘레 물의 장식. 맵 종류마다 본편 물에 뜨는 장식(연잎·물결·용암 거품 등, StageTileData의 물 장식 프리팹)을 그대로 쓴다.
/// 타원 섬 생성은 장식을 만들지 않고(TileMapGenerator.bSpawnDeco), 본편 분포(바다 0.01)로는 섬 둘레 좁은 물에 거의 걸리지 않아
/// 화면 안 깊은 물에만 따로 띄운다. 본편 장식 생성기(AnimatedObjGenerator)는 쓰지 않는다 - 맵을 까는 순간 장식을 걷어 버려
/// 티어 전환 파도가 닿기 전에 직전 티어의 장식이 먼저 사라진다.
///
/// 파도가 새 지형으로 바꾼 선(RoguelikeTileWave.TryGetSwapFront)이 장식 칸에 닿으면 직전 장식은 오므라들어 사라지고 새 장식은 톡 솟아난다.
/// 진입(바다가 처음부터 새 물이고 화면이 암전 아래)은 직전 장식을 바로 걷고 새 장식만 파도를 따라 솟는다.
/// 파도 칸 이벤트에 묶지 않는다 - 물 타일이 같은 맵 사이(1·2스테이지)는 깊은 물 칸이 파도 대상에서 빠지고, 진입은 섬 둘레 칸만 파도가 돈다.
/// 파도가 장식 칸까지 가기 전에 끝나면 같은 속도로 선을 이어 가며 바꾼다.
/// </summary>
public sealed class RoguelikeWaterDeco
{
    private struct ActiveDeco
    {
        public DecoSpritePatternAnimator prefab;
        public DecoSpritePatternAnimator instance;
    }

    // 파도 선이 닿으면 바꿀 칸 하나. 걷을 장식(old)과 띄울 장식(prefab) 중 하나만 있을 수도 있다.
    private struct PendingSwap
    {
        public Vector3Int cell;
        public float distance;
        public bool bHasOld;
        public ActiveDeco old;
        public DecoSpritePatternAnimator prefab;
    }

    private struct DecoAnim
    {
        public ActiveDeco deco;
        public float elapsed;
        public bool bOut;
    }

    private EnvironmentSystem environmentSystem;
    private RoguelikeWaterDecoSettings settings;
    private Transform root;

    private readonly Dictionary<Vector3Int, ActiveDeco> active = new Dictionary<Vector3Int, ActiveDeco>(32);
    private readonly Dictionary<Vector3Int, DecoSpritePatternAnimator> incoming = new Dictionary<Vector3Int, DecoSpritePatternAnimator>(32);
    // 파도 거리 순. pendingNext부터 아직 바뀌지 않았다.
    private readonly List<PendingSwap> pending = new List<PendingSwap>(64);
    private int pendingNext;
    private readonly List<DecoAnim> anims = new List<DecoAnim>(64);
    private readonly Dictionary<DecoSpritePatternAnimator, Stack<DecoSpritePatternAnimator>> pools = new Dictionary<DecoSpritePatternAnimator, Stack<DecoSpritePatternAnimator>>(8);
    private readonly List<Vector3Int> candidates = new List<Vector3Int>(512);
    private readonly List<Vector3Int> picked = new List<Vector3Int>(64);

    private readonly RoguelikeWaveFront front = new RoguelikeWaveFront();

    private static readonly Comparison<PendingSwap> pendingComparison = (_a, _b) => _a.distance.CompareTo(_b.distance);

    public void Initialize(EnvironmentSystem _environmentSystem, RoguelikeWaterDecoSettings _settings, Transform _parent)
    {
        environmentSystem = _environmentSystem;
        settings = _settings ?? new RoguelikeWaterDecoSettings();

        if (null == root)
        {
            root = new GameObject("RoguelikeWaterDeco").transform;
            root.SetParent(_parent, false);
        }
    }

    /// <summary>
    /// 맵을 깔고 파도 시작 상태를 준비한 직후(RoguelikeTileWave.PrepareAfterGenerate 뒤)에 부른다. _viewRect(월드) 안 깊은 물에서 새 장식 자리를 고르고,
    /// Tick이 파도 선을 따라 바꾼다. _bEntry(진입 - 바다가 처음부터 새 물이고 화면이 암전 아래)면 직전 장식은 바로 걷는다.
    /// </summary>
    public void Prepare(Rect _viewRect, RoguelikeTileWave _wave, bool _bEntry)
    {
        Flush();

        PickCells(_viewRect);

        if (_bEntry)
        {
            foreach (KeyValuePair<Vector3Int, ActiveDeco> pair in active) Despawn(pair.Value);
            active.Clear();
        }

        // 걷을 장식과 띄울 장식을 칸마다 묶어 파도 거리 순으로 줄 세운다.
        foreach (KeyValuePair<Vector3Int, ActiveDeco> pair in active)
        {
            PendingSwap swap = new PendingSwap { cell = pair.Key, bHasOld = true, old = pair.Value };
            if (incoming.TryGetValue(pair.Key, out DecoSpritePatternAnimator prefab))
            {
                swap.prefab = prefab;
                incoming.Remove(pair.Key);
            }
            swap.distance = _wave.GetWaveDistance(environmentSystem.GetTileCellCenterWorldForRun(pair.Key));
            pending.Add(swap);
        }
        active.Clear();

        foreach (KeyValuePair<Vector3Int, DecoSpritePatternAnimator> pair in incoming)
        {
            pending.Add(new PendingSwap
            {
                cell = pair.Key,
                prefab = pair.Value,
                distance = _wave.GetWaveDistance(environmentSystem.GetTileCellCenterWorldForRun(pair.Key)),
            });
        }
        incoming.Clear();

        pending.Sort(pendingComparison);
        pendingNext = 0;
        front.Reset();
    }

    /// <summary>매 프레임(게임 시간). 파도 선이 닿은 칸을 바꾸고 솟아남·오므라듦을 진행한다.</summary>
    public void Tick(RoguelikeTileWave _wave, float _deltaTime)
    {
        if (pendingNext < pending.Count) AdvancePending(_wave, _deltaTime);
        if (anims.Count > 0) AdvanceAnims(_deltaTime);
    }

    /// <summary>흐름이 끊기거나 맵을 다시 깔기 전. 남은 교체와 연출을 바로 끝낸다.</summary>
    public void Flush()
    {
        for (int i = pendingNext; i < pending.Count; i++)
        {
            PendingSwap swap = pending[i];
            if (swap.bHasOld) Despawn(swap.old);
            if (null != swap.prefab) Spawn(swap.cell, swap.prefab, false);
        }
        pending.Clear();
        pendingNext = 0;

        for (int i = 0; i < anims.Count; i++)
        {
            DecoAnim anim = anims[i];
            if (anim.bOut) Despawn(anim.deco);
            else if (null != anim.deco.instance) anim.deco.instance.transform.localScale = anim.deco.prefab.transform.localScale;
        }
        anims.Clear();
    }

    // // 내부

    private void AdvancePending(RoguelikeTileWave _wave, float _deltaTime)
    {
        if (false == front.Advance(_wave, _deltaTime)) return;

        while (pendingNext < pending.Count && pending[pendingNext].distance <= front.Distance)
        {
            PendingSwap swap = pending[pendingNext];
            pendingNext++;

            if (swap.bHasOld) anims.Add(new DecoAnim { deco = swap.old, bOut = true });
            if (null != swap.prefab) Spawn(swap.cell, swap.prefab, true);
        }

        if (pendingNext >= pending.Count)
        {
            pending.Clear();
            pendingNext = 0;
        }
    }

    private void AdvanceAnims(float _deltaTime)
    {
        for (int i = anims.Count - 1; i >= 0; i--)
        {
            DecoAnim anim = anims[i];
            anim.elapsed += _deltaTime;

            float seconds = Mathf.Max(0.01f, anim.bOut ? settings.disappearSeconds : settings.appearSeconds);
            float t = Mathf.Clamp01(anim.elapsed / seconds);
            Vector3 baseScale = anim.deco.prefab.transform.localScale;

            if (t >= 1f)
            {
                if (anim.bOut) Despawn(anim.deco);
                else if (null != anim.deco.instance) anim.deco.instance.transform.localScale = baseScale;

                anims.RemoveAt(i);
                continue;
            }

            if (null != anim.deco.instance)
            {
                anim.deco.instance.transform.localScale = baseScale * (anim.bOut ? DisappearScale(t) : AppearScale(t));
            }
            anims[i] = anim;
        }
    }

    /// <summary>0에서 솟아 살짝 넘쳤다가(약 1.1배) 제 크기로 돌아온다(ease-out-back).</summary>
    private static float AppearScale(float _t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float u = _t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    /// <summary>처음엔 천천히, 끝으로 갈수록 빠르게 오므라든다(물속으로 가라앉듯).</summary>
    private static float DisappearScale(float _t)
    {
        return 1f - _t * _t;
    }

    private void PickCells(Rect _viewRect)
    {
        incoming.Clear();

        List<DecoSpritePatternAnimator> prefabs = environmentSystem.WaterDecoPrefabsForRun;
        if (null == prefabs || 0 == prefabs.Count || settings.density <= 0f) return;

        environmentSystem.GetDeepWaterCellsForRun(_viewRect, candidates);
        int target = Mathf.RoundToInt(candidates.Count * Mathf.Clamp01(settings.density));
        if (target <= 0) return;

        int gap = Mathf.Max(0, settings.minGapCells);
        picked.Clear();

        // 부분 Fisher-Yates로 무작위 순서로 훑으며 이미 고른 자리와 너무 가까운 칸은 건너뛴다.
        int n = candidates.Count;
        for (int k = 0; k < n && picked.Count < target; k++)
        {
            int r = k + UnityEngine.Random.Range(0, n - k);
            Vector3Int cell = candidates[r];
            candidates[r] = candidates[k];
            candidates[k] = cell;

            if (IsNearPicked(cell, gap)) continue;

            DecoSpritePatternAnimator prefab = prefabs[UnityEngine.Random.Range(0, prefabs.Count)];
            if (null == prefab) continue;

            picked.Add(cell);
            incoming[cell] = prefab;
        }
    }

    private bool IsNearPicked(Vector3Int _cell, int _gap)
    {
        if (_gap <= 0) return false;

        for (int i = 0; i < picked.Count; i++)
        {
            Vector3Int other = picked[i];
            if (Mathf.Abs(other.x - _cell.x) <= _gap && Mathf.Abs(other.y - _cell.y) <= _gap) return true;
        }
        return false;
    }

    private void Spawn(Vector3Int _cell, DecoSpritePatternAnimator _prefab, bool _bAnimate)
    {
        Vector3 pos = environmentSystem.GetWaterDecoWorldPosForRun(_cell);

        DecoSpritePatternAnimator instance;
        if (pools.TryGetValue(_prefab, out Stack<DecoSpritePatternAnimator> pool) && pool.Count > 0)
        {
            instance = pool.Pop();
            instance.transform.position = pos;
            instance.gameObject.SetActive(true);
        }
        else
        {
            instance = UnityEngine.Object.Instantiate(_prefab, pos, Quaternion.identity, root);
        }

        instance.transform.localScale = _bAnimate ? Vector3.zero : _prefab.transform.localScale;
        instance.SetSortingOrder();

        ActiveDeco deco = new ActiveDeco { prefab = _prefab, instance = instance };
        active[_cell] = deco;
        if (_bAnimate) anims.Add(new DecoAnim { deco = deco, bOut = false });
    }

    private void Despawn(ActiveDeco _deco)
    {
        if (null == _deco.instance) return;

        _deco.instance.gameObject.SetActive(false);

        if (false == pools.TryGetValue(_deco.prefab, out Stack<DecoSpritePatternAnimator> pool))
        {
            pool = new Stack<DecoSpritePatternAnimator>(8);
            pools.Add(_deco.prefab, pool);
        }
        pool.Push(_deco.instance);
    }
}
