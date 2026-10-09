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

    // "지금 눈에 보이는 만큼"(LogVisibleCounts)을 화면 전체에서 세는지. 플레이어의 감지기만 켠다.
    // 흡입 반경(0.35 × 배율)은 워낙 작아 좋은 원목 위에 서 있어도 1~3개만 들어오는데, 그걸로 교체 상한을
    // 잡으면 20개짜리 스택은 영영 후보가 못 된다. 화면 안의 개수로 세면 유저가 보는 것과 판정이 같아진다.
    // 발동 조건(흡입 반경에서 실제로 거절됨)은 그대로다 - 그래서 비운 칸은 발밑의 그 원목이 채운다.
    private readonly bool bTrackVisibleCounts;
    private readonly List<IStaticCollidable> screenResults = new List<IStaticCollidable>(64);

    // 정렬 작업 버퍼. 원목마다 우선순위를 한 번만 계산해 sortKeys에 담고, Array.Sort(keys, items)로
    // 두 배열을 함께 정렬한 뒤 리스트에 되돌린다. 비교마다 우선순위를 다시 계산하던 삽입 정렬은
    // 반경 안 원목이 수십 개일 때는 괜찮았지만 n²이라, 흡입 반경을 키우는 특성이 생기면 감지 틱마다
    // 수만 번의 가치 조회가 도는 구조였다. 지금은 원목당 계산 1회 + O(n log n) 비교이고, 버퍼는
    // 모자랄 때만 두 배로 키우므로 정상 플레이 중에는 할당이 없다.
    private const int SORT_BUFFER_INITIAL = 64;
    private static PickupSortKey[] sortKeys = new PickupSortKey[SORT_BUFFER_INITIAL];
    private static IStaticCollidable[] sortItems = new IStaticCollidable[SORT_BUFFER_INITIAL];

    /// <summary>
    /// 흡입 선점 정렬 키. 원목이 아닌 아이템이 맨 앞, 원목끼리는 LogPickupOrder(보석 등급 → 총가치 → 개당 가치),
    /// 그래도 같으면 스캔 순서. 교체 요청(InventoryManager.RequestLogSwap)도 같은 LogPickupOrder로 "들어올
    /// 원목"을 고르므로 안내와 선점이 어긋나지 않는다.
    ///
    /// 스캔 순서는 안정 정렬 흉내다. Array.Sort는 안정 정렬이 아니라서 같은 무리 안에서 어느 원목이 먼저
    /// 흡입되는지가 틱마다 달라져 보일 수 있다. 예전엔 값들을 long 하나에 비트로 눌러 담았는데 총가치(최대 6e10)와
    /// 개당 가치(최대 6e8)를 둘 다 실으면 63비트를 넘어 구조체로 바꿨다. IComparable&lt;T&gt;를 구현한 구조체라
    /// Array.Sort가 박싱 없이 비교한다.
    /// </summary>
    private struct PickupSortKey : IComparable<PickupSortKey>
    {
        public bool bNonLog;
        public LogState logState;
        public long groupValue;
        public long unitValue;
        public int scanIndex;

        public int CompareTo(PickupSortKey _other)
        {
            if (bNonLog != _other.bNonLog) return bNonLog ? -1 : 1;

            int order = LogPickupOrder.Compare(logState, groupValue, unitValue,
                _other.logState, _other.groupValue, _other.unitValue);
            if (order != 0) return order;

            return scanIndex.CompareTo(_other.scanIndex);
        }
    }

    public ItemDetector(Transform _sensorTransform, LayerMask _itemLayer, bool _bTrackVisibleCounts = false)
    {
        sensorTransform = _sensorTransform;
        itemLayer = _itemLayer;
        bTrackVisibleCounts = _bTrackVisibleCounts;
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

    /// <summary>
    /// 지금 바닥에 놓여 있고 먹을 수 있는 원목 중 해당 종류(수종은 개량이 끝난 값으로 비교)에서 <b>가장 가까운 것</b>을
    /// 찾는다. 인벤토리 교체(Tab)가 비운 칸에 "안내된 그 종류"를 즉시 끌어오기 위해 쓴다 - 걷는 중이면 제안을 띄운
    /// 원목이 흡입 반경을 막 벗어났을 수 있으므로 반경이 아니라 _radius(화면 크기)까지 본다. 없으면 null.
    /// 주기 스캔의 결과(results)는 건드리지 않는다.
    /// </summary>
    public LogItem FindNearestAcquirableLog(float _radius, TreeType _treeType, LogState _logState)
    {
        if (CollisionSystem.Instance == null) return null;

        Vector2 center = sensorTransform.position;
        CollisionSystem.Instance.GetCollidablesInRadius(center, _radius, itemLayer.value, screenResults);

        TreeType speciesFloor = FindSpeciesImprovementFloor(screenResults);
        LogItem nearest = null;
        float nearestSqr = float.MaxValue;

        for (int i = 0; i < screenResults.Count; i++)
        {
            if (!(screenResults[i] is LogItem logItem)) continue;
            if (!logItem.CanBeAcquired || logItem.MoveState != ItemMoveState.Dropped) continue;
            if (logItem.logState != _logState) continue;
            if (EffectiveTreeType(logItem, speciesFloor) != _treeType) continue;

            float sqr = ((Vector2)logItem.transform.position - center).sqrMagnitude;
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = logItem;
            }
        }

        screenResults.Clear();
        return nearest;
    }

    /// <summary>주기가 찼으면 스캔을 수행하고 true를 반환한다. 아직이면 results를 건드리지 않고 false.</summary>
    private bool Scan(float _deltaTime, float _interval, float _radius)
    {
        if (CollisionSystem.Instance == null) return false;

        timer += _deltaTime;
        if (timer < _interval) return false;
        timer = 0f;

        using var _profile = PickupProfilerMarkers.DetectScan.Auto();

        if (!bTrackVisibleCounts)
        {
            CollisionSystem.Instance.GetCollidablesInRadius(sensorTransform.position, _radius, itemLayer.value, results);
            return true;
        }

        // 화면 개수를 셀 때는 흡입 반경 질의와 화면 질의를 한 번의 격자 순회로 같이 한다. 흡입 반경이 화면만큼
        // 커지면 두 질의가 거의 같은 셀을 두 번 훑기 때문이다. 결과(내용·순서)는 따로 질의한 것과 같다
        // (CollisionSystem.GetCollidablesInTwoRadii 참고). 카메라를 못 찾으면 흡입 반경 결과로 센다(예전 동작).
        float screenRadius = CameraBoundsUtil.GetReferenceHalfDiagonal();
        Camera cam = CameraFinder.Instance != null ? CameraFinder.Instance.PPMainCamera : null;
        bool bUseScreen = screenRadius > 0f && cam != null;

        if (bUseScreen)
        {
            CollisionSystem.Instance.GetCollidablesInTwoRadii(sensorTransform.position, _radius, results,
                cam.transform.position, screenRadius, screenResults, itemLayer.value);
        }
        else
        {
            CollisionSystem.Instance.GetCollidablesInRadius(sensorTransform.position, _radius, itemLayer.value, results);
        }

        CountVisibleLogsOnScreen(bUseScreen ? screenResults : results);

        return true;
    }

    /// <summary>
    /// 화면(기준 해상도 반대각선 원, 카메라 중심) 안의 원목을 (수종, 등급)별로 세어 LogVisibleCounts에 채운다.
    /// 같은 틱의 흡입 정렬과 교체 요청이 이 표를 읽는다. 카메라를 못 찾으면 흡입 반경 결과로 센다(예전 동작).
    /// 공중에 떠 있는 원목도 센다. 수종은 개량이 끝난 값으로 묶는다.
    /// </summary>
    private void CountVisibleLogsOnScreen(List<IStaticCollidable> _source)
    {
        TreeType speciesFloor = FindSpeciesImprovementFloor(_source);

        LogVisibleCounts.Clear();

        for (int i = 0; i < _source.Count; i++)
        {
            if (!(_source[i] is LogItem logItem)) continue;

            // "들어올 수 있는" 원목만 센다.
            // - 습득 불가(교체·DropAllItem이 흘리는 연출 원목, 정리 중인 원목)는 제외. 연출 원목은 풀에서 꺼낸 진짜
            //   LogItem이라 충돌 시스템에 등록되지만 날아가다 사라진다. 세면 소나무 3개를 버리는 순간 소나무가 3개
            //   "더 보이는" 셈이 되어 버린 수종의 총가치가 1초 동안 부풀어 선점과 상한이 같이 틀어진다.
            // - 이미 자리가 정해진 원목도 제외: 캐릭터로 빨려 들어가는 중(Sucking), 상자로 전송 중(Transferring·
            //   ContainerTransferring). 가방 [자작 3/5]에 자작 3개가 떨어지면 2개는 기존 슬롯으로 흡입되고 1개만 거절되는데,
            //   흡입 중인 2개까지 세면 "자작 3개 들어옴"으로 상한이 3배가 되어 소나무 5개(60)를 버리고 자작 1개(40)를
            //   얻는 손해 제안이 성립한다. 바닥에 놓인 것과 나무에서 떨어지는 중인 것만 "들어올 개수"다.
            if (!logItem.CanBeAcquired) continue;

            ItemMoveState moveState = logItem.MoveState;
            if (moveState != ItemMoveState.Dropped && moveState != ItemMoveState.Launching) continue;

            LogVisibleCounts.Add(EffectiveTreeType(logItem, speciesFloor), logItem.logState);
        }

        // 화면 목록은 개수만 세면 볼일이 끝난다. 풀로 돌아간 아이템을 붙들고 있지 않게 비운다.
        screenResults.Clear();
    }

    /// <summary>
    /// 한 번의 스캔 결과를 <b>비싼 원목이 앞에 오도록</b> 재정렬한다.
    ///
    /// 인벤토리 슬롯 예약(IInventoryChecker.CanAcquired)은 먼저 물어본 원목이 먼저 자리를 차지하는
    /// 선착순이고, 스캔 결과의 순서는 CollisionSystem이 훑은 순서라 사실상 무작위였다. 그래서 빈 슬롯이
    /// 하나뿐인데 반경 안에 수종이 다른 원목이 섞여 있으면, 마지막 한 칸을 참나무가 가져가고 흑요목이
    /// 튕겨나가는 일이 생겼다. 흡입을 걸기 전에 여기서 순서를 잡아주면 값비싼 쪽이 그 칸을 선점한다.
    ///
    /// 순서는 LogPickupOrder 한 곳에서 정한다: <b>보석 등급이 먼저</b>, 그 다음 <b>"지금 보이는 만큼의 가치"</b>
    /// = 개당 가치 × min(화면 안에 보이는 개수, 슬롯 최대 중첩)(LogValue.GetGroupValue). 개수는 흡입 반경이
    /// 아니라 <b>화면 전체</b>에서 센다(CountVisibleLogsOnScreen). 일반 원목끼리 개당 가치만 보면 자작 <b>한 개</b>
    /// (40)가 마지막 칸을 잠그고 뒤따르는 소나무 15개(12 × 5칸 = 60)를 통째로 튕겨낸다. 칸의 가치는 그 칸을
    /// 채울 수 있는 만큼이지 원목 하나의 가치가 아니다. 교체 시스템의 상한도 같은 식을 쓰므로, 선점과 교체가
    /// 같은 값을 보고 움직인다. 보석만은 예외로 가치와 무관하게 먼저 들인다(이유는 LogPickupOrder 참고).
    ///
    /// 개당 가치는 기본 가치 × 등급 배율의 실제 값이다(LogValue). 수종 순서만으로 비교하면 황금
    /// 소나무(60)를 일반 자작(40) 아래로 잘못 두게 된다.
    /// 수종은 "수종 개량"이 끝난 뒤의 값으로 본다(FillPickupSortKey 참고).
    ///
    /// 보석 등급 원목은 총가치와 무관하게 일반 원목보다 먼저다(LogPickupOrder 참고). 총가치가 같으면
    /// <b>개당 가치가 높은 쪽</b>이 앞선다.
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
        // 개수 표(LogVisibleCounts)는 Scan이 화면 전체를 세어 이미 채워 뒀다. 여기서는 읽기만 한다.
        int count = _results.Count;
        if (count < 2) return;

        TreeType speciesFloor = FindSpeciesImprovementFloor(_results);

        EnsureSortBuffers(count);

        // 원목당 한 번만 계산한다. 비교 순서(비원목 → 보석 → 총가치 → 개당 → 스캔 순서)는 키 구조체가 안다.
        for (int i = 0; i < count; i++)
        {
            IStaticCollidable item = _results[i];
            sortItems[i] = item;
            FillPickupSortKey(item, speciesFloor, i, ref sortKeys[i]);
        }

        Array.Sort(sortKeys, sortItems, 0, count);

        for (int i = 0; i < count; i++)
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

        sortKeys = new PickupSortKey[newSize];
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
        // "첫 원목"이 아니라 "None이 아닌 첫 값". 교체·DropAllItem이 흘리는 연출 원목은 공급자가 없어 None을 돌려주는데
        // 캐릭터 위치에서 출발해 목록 첫 원소가 되기 쉽다. 개량이 꺼져 있으면 전부 None이라 끝까지 훑지만 비용은 없다.
        for (int i = 0; i < _results.Count; i++)
        {
            if (!(_results[i] is LogItem logItem)) continue;

            TreeType floor = logItem.SpeciesImprovementFloor;
            if (floor != TreeType.None) return floor;
        }

        return TreeType.None;
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
    /// 정렬 키를 채운다. 순서 자체는 PickupSortKey.CompareTo(→ LogPickupOrder)가 정한다.
    ///
    /// 원목이 아닌 아이템(원석 등)은 원목 슬롯을 두고 경쟁하지 않으므로 최우선으로 두어 앞쪽에
    /// 모아둔다. 원목끼리의 순서만 바뀌고 나머지는 하던 대로 처리된다.
    /// </summary>
    private static void FillPickupSortKey(IStaticCollidable _collidable, TreeType _speciesFloor, int _scanIndex,
        ref PickupSortKey _key)
    {
        _key.scanIndex = _scanIndex;

        if (!(_collidable is LogItem logItem))
        {
            _key.bNonLog = true;
            _key.logState = LogState.Normal;
            _key.groupValue = 0;
            _key.unitValue = 0;
            return;
        }

        TreeType treeType = EffectiveTreeType(logItem, _speciesFloor);

        _key.bNonLog = false;
        _key.logState = logItem.logState;
        _key.unitValue = LogValue.GetUnitValue(treeType, logItem.logState);
        _key.groupValue = LogValue.GetGroupValue(treeType, logItem.logState, LogVisibleCounts.Get(treeType, logItem.logState));
    }
}
