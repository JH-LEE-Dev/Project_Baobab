using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 일정 주기로 자신의 위치를 중심으로 반경 내 IStaticCollidable(아이템 등)을 스캔하는 재사용 헬퍼.
/// Character/LumberjackNPC처럼 "찾은 아이템으로 무엇을 할지"는 서로 다르지만
/// "얼마나 자주, 어떤 반경으로 스캔할지"는 동일한 패턴을 공유하는 대상들이 사용한다.
/// </summary>
public class ItemDetector
{
    private readonly Transform sensorTransform;
    private readonly LayerMask itemLayer;
    private readonly List<IStaticCollidable> results = new List<IStaticCollidable>(16);
    private float timer;

    // 한 번의 스캔에서 (수종, 등급)별로 몇 개가 보이는지 세는 표. 13 × 6 = 78칸.
    // 정렬이 감지 틱마다(초당 5회) 돌므로 매번 새로 잡지 않고 정적으로 두고 지워 쓴다.
    private const int LOG_STATE_COUNT = 6;
    private static readonly int[] visibleCounts = new int[(int)TreeType.Max * LOG_STATE_COUNT];

    // 정렬 작업 버퍼. 원목마다 우선순위를 한 번만 계산해 sortKeys에 담고, Array.Sort(keys, items)로
    // 두 배열을 함께 정렬한 뒤 리스트에 되돌린다. 비교마다 우선순위를 다시 계산하던 삽입 정렬은
    // 반경 안 원목이 수십 개일 때는 괜찮았지만 n²이라, 흡입 반경을 키우는 특성이 생기면 감지 틱마다
    // 수만 번의 가치 조회가 도는 구조였다. 지금은 원목당 계산 1회 + O(n log n) 비교이고, 버퍼는
    // 모자랄 때만 두 배로 키우므로 정상 플레이 중에는 할당이 없다.
    private const int SORT_BUFFER_INITIAL = 64;
    private static long[] sortKeys = new long[SORT_BUFFER_INITIAL];
    private static IStaticCollidable[] sortItems = new IStaticCollidable[SORT_BUFFER_INITIAL];

    // 정렬 키에 원래 순서를 함께 실어 안정 정렬을 흉내낸다. Array.Sort는 안정 정렬이 아니라서 우선순위가
    // 같은 원목끼리 스캔 순서가 뒤섞일 수 있는데, 그러면 같은 무리 안에서 어느 원목이 먼저 흡입되는지가
    // 틱마다 달라져 보인다. key = -(우선순위 × 2^16) + 인덱스 로 두면 오름차순 정렬이 곧 "우선순위
    // 내림차순, 같으면 스캔 순서 유지"가 된다. 한 스캔에 2^16개를 넘길 일은 없다.
    private const int SORT_INDEX_BITS = 16;
    private const int SORT_MAX_ITEMS = 1 << SORT_INDEX_BITS;

    // 원목이 아닌 아이템(원석 등)의 우선순위. 어떤 원목 무리보다 커야 하고(최우선), 2^16을 곱해도
    // long을 넘지 않아야 한다. 흑요목 프리즘(6억) × 슬롯 용량 100개여도 6e10 < 2^40이다.
    private const long NON_LOG_PRIORITY = 1L << 40;

    public ItemDetector(Transform _sensorTransform, LayerMask _itemLayer)
    {
        sensorTransform = _sensorTransform;
        itemLayer = _itemLayer;
    }

    /// <summary>
    /// 매 프레임(혹은 FixedUpdate)마다 호출하세요. _interval마다 실제로 스캔을 수행해
    /// 발견된 각 콜라이더블을 _onFound에 전달합니다.
    /// </summary>
    public void Tick(float _deltaTime, float _interval, float _radius, Action<IStaticCollidable> _onFound)
    {
        if (!Scan(_deltaTime, _interval, _radius)) return;

        for (int i = 0; i < results.Count; i++)
        {
            _onFound(results[i]);
        }
    }

    /// <summary>
    /// 발견된 목록을 통째로 넘기는 오버로드. "무엇을 찾았는가"뿐 아니라 "어떤 순서로 처리할지"까지
    /// 호출 측이 정해야 할 때 쓴다(예: 인벤토리 슬롯을 고가 원목부터 예약하게 하는 경우).
    ///
    /// 넘기는 리스트는 다음 스캔에서 그대로 재사용되므로, 콜백 안에서만 쓰고 밖으로 들고 나가면 안 된다.
    /// 콜백 안에서 순서를 바꾸는 것(정렬)은 괜찮다.
    /// </summary>
    public void Tick(float _deltaTime, float _interval, float _radius, Action<List<IStaticCollidable>> _onFound)
    {
        if (!Scan(_deltaTime, _interval, _radius)) return;

        _onFound(results);
    }

    /// <summary>주기가 찼으면 스캔을 수행하고 true를 반환한다. 아직이면 results를 건드리지 않고 false.</summary>
    private bool Scan(float _deltaTime, float _interval, float _radius)
    {
        if (CollisionSystem.Instance == null) return false;

        timer += _deltaTime;
        if (timer < _interval) return false;
        timer = 0f;

        CollisionSystem.Instance.GetCollidablesInRadius(sensorTransform.position, _radius, itemLayer.value, results);
        return true;
    }

    /// <summary>
    /// 한 번의 스캔 결과를 <b>비싼 원목이 앞에 오도록</b> 재정렬한다.
    ///
    /// 인벤토리 슬롯 예약(IInventoryChecker.CanAcquired)은 먼저 물어본 원목이 먼저 자리를 차지하는
    /// 선착순이고, 스캔 결과의 순서는 CollisionSystem이 훑은 순서라 사실상 무작위였다. 그래서 빈 슬롯이
    /// 하나뿐인데 반경 안에 수종이 다른 원목이 섞여 있으면, 마지막 한 칸을 참나무가 가져가고 흑요목이
    /// 튕겨나가는 일이 생겼다. 흡입을 걸기 전에 여기서 순서를 잡아주면 값비싼 쪽이 그 칸을 선점한다.
    ///
    /// <b>기준은 "지금 보이는 만큼의 가치"</b> = 개당 가치 × min(보이는 개수, 슬롯 최대 중첩)이다
    /// (LogValue.GetGroupValue). 개당 가치만 보면 보석 <b>한 개</b>(황금 소나무 60)가 마지막 칸을
    /// 잠그고, 뒤따르는 일반 자작 15개(600)를 통째로 튕겨낸다. 칸의 가치는 그 칸을 채울 수 있는
    /// 만큼이지 원목 하나의 가치가 아니다. 교체 시스템의 상한도 같은 식을 쓰므로, 선점과 교체가
    /// 같은 값을 보고 움직인다.
    ///
    /// 개당 가치는 기본 가치 × 등급 배율의 실제 값이다(LogValue). 수종 순서만으로 비교하면 황금
    /// 소나무(60)를 일반 자작(40) 아래로 잘못 두게 된다.
    /// 수종은 "수종 개량"이 끝난 뒤의 값으로 본다(GetPickupPriority 참고).
    ///
    /// 순서를 잘못 예측해도 예약이 어긋나지는 않는다. 공간 판정(CanAcquired)은 개량이 끝난 실제
    /// 수종으로 이뤄지므로, 이 정렬은 "누가 먼저 물어보는가"만 정할 뿐이다.
    ///
    /// 원목마다 우선순위를 한 번만 계산해 정적 버퍼에 담고 Array.Sort(keys, items)로 정렬한다.
    /// List.Sort(Comparison)과 달리 비교자 래퍼를 할당하지 않고, 버퍼는 모자랄 때만 키우므로 이 정렬이
    /// 아이템 감지 틱마다(초당 5회) 돌아도 정상 플레이 중 할당이 없다. 키에 스캔 순서를 함께 실어
    /// 우선순위가 같은 아이템끼리는 순서가 그대로 유지된다(안정 정렬과 같은 결과).
    /// </summary>
    public static void SortByPickupPriority(List<IStaticCollidable> _results)
    {
        int count = _results.Count;
        if (count < 2) return;

        TreeType speciesFloor = FindSpeciesImprovementFloor(_results);

        CountVisibleLogs(_results, speciesFloor);

        EnsureSortBuffers(count);

        // 원목당 한 번만 계산한다. 키가 작을수록 앞에 오도록 부호를 뒤집고, 같은 우선순위끼리는
        // 스캔 순서(인덱스)가 낮은 쪽이 앞에 오도록 인덱스를 더한다.
        int keyed = Mathf.Min(count, SORT_MAX_ITEMS);
        for (int i = 0; i < keyed; i++)
        {
            IStaticCollidable item = _results[i];
            sortItems[i] = item;
            sortKeys[i] = -(GetPickupPriority(item, speciesFloor) << SORT_INDEX_BITS) + i;
        }

        Array.Sort(sortKeys, sortItems, 0, keyed);

        for (int i = 0; i < keyed; i++)
        {
            _results[i] = sortItems[i];
            sortItems[i] = null;   // 풀로 돌아간 아이템을 정적 버퍼가 붙들고 있지 않게 한다
        }
    }

    /// <summary>정렬 버퍼가 모자라면 두 배씩 키운다. 정상 플레이에서는 초기 크기 안에서 끝나 할당이 없다.</summary>
    private static void EnsureSortBuffers(int _count)
    {
        if (sortKeys.Length >= _count) return;

        int newSize = sortKeys.Length;
        while (newSize < _count) newSize *= 2;

        sortKeys = new long[newSize];
        sortItems = new IStaticCollidable[newSize];
    }

    /// <summary>
    /// "수종 개량" 특성이 이번 흡입에서 원목들을 끌어올릴 바닥 수종을 한 번만 알아온다.
    ///
    /// 바닥 수종은 지역마다 하나로 정해지고, 던전의 모든 원목이 같은 공급자(LogItemController)를
    /// 공유하므로 원목 하나에만 물어보면 된다.
    /// </summary>
    private static TreeType FindSpeciesImprovementFloor(List<IStaticCollidable> _results)
    {
        for (int i = 0; i < _results.Count; i++)
        {
            if (_results[i] is LogItem logItem) return logItem.SpeciesImprovementFloor;
        }

        return TreeType.None;
    }

    /// <summary>이번 스캔에 (수종, 등급)별로 원목이 몇 개 보이는지 센다. 수종은 개량이 끝난 값으로 묶는다.</summary>
    private static void CountVisibleLogs(List<IStaticCollidable> _results, TreeType _speciesFloor)
    {
        Array.Clear(visibleCounts, 0, visibleCounts.Length);

        for (int i = 0; i < _results.Count; i++)
        {
            if (!(_results[i] is LogItem logItem)) continue;

            int index = VisibleIndex(EffectiveTreeType(logItem, _speciesFloor), logItem.logState);
            if (index >= 0 && index < visibleCounts.Length) visibleCounts[index]++;
        }
    }

    private static int VisibleIndex(TreeType _treeType, LogState _logState)
    {
        return (int)_treeType * LOG_STATE_COUNT + (int)_logState;
    }

    /// <summary>
    /// "수종 개량"은 흡입 직전에 원목을 바닥 수종까지 끌어올린다(LogItem.CheckAcquireCondition이
    /// CanAcquired보다 먼저 부른다). 그래서 바닥에 보이는 수종이 아니라 실제로 담길 수종으로
    /// 줄을 세워야 한다. 개량이 꺼져 있으면 _speciesFloor가 None이라 아무 영향이 없다.
    ///
    /// 이걸 빼먹으면, 개량으로 어차피 같은 수종이 될 원목들을 원래 수종 순서로 줄 세우게 된다.
    /// 그러면 등급이 뒤집힌다 - 바닥 수종이 자작나무일 때 Normal 소나무와 Perfect 참나무는 둘 다
    /// 자작나무가 되는데, 원래 수종만 보면 소나무가 앞서므로 20배 싼 쪽이 마지막 칸을 가져간다.
    /// </summary>
    private static TreeType EffectiveTreeType(LogItem _logItem, TreeType _speciesFloor)
    {
        return _speciesFloor > _logItem.treeType ? _speciesFloor : _logItem.treeType;
    }

    /// <summary>
    /// 정렬 기준값. 클수록 먼저 흡입을 건다.
    ///
    /// 원목이 아닌 아이템(원석 등)은 원목 슬롯을 두고 경쟁하지 않으므로 최우선으로 두어 앞쪽에
    /// 모아둔다. 원목끼리의 순서만 바뀌고 나머지는 하던 대로 처리된다.
    /// </summary>
    private static long GetPickupPriority(IStaticCollidable _collidable, TreeType _speciesFloor)
    {
        // long.MaxValue를 쓰면 정렬 키를 만들 때(<< 16) 넘친다. 어떤 원목 무리보다 크면서 시프트에 안전한 값을 쓴다.
        if (!(_collidable is LogItem logItem)) return NON_LOG_PRIORITY;

        TreeType treeType = EffectiveTreeType(logItem, _speciesFloor);

        int index = VisibleIndex(treeType, logItem.logState);
        int visibleCount = (index >= 0 && index < visibleCounts.Length) ? visibleCounts[index] : 1;

        return LogValue.GetGroupValue(treeType, logItem.logState, visibleCount);
    }
}
