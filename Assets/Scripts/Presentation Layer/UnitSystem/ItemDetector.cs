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
    /// 삽입 정렬을 직접 돌린다. 리스트가 반경 안의 아이템 수(보통 수십 개 이하)라 충분히 빠르고,
    /// List.Sort(Comparison)과 달리 비교자 래퍼를 할당하지 않는다 - 이 정렬은 아이템 감지 틱마다
    /// (초당 5회) 돈다. 안정 정렬이라 우선순위가 같은 아이템끼리는 스캔 순서가 그대로 유지된다.
    /// </summary>
    public static void SortByPickupPriority(List<IStaticCollidable> _results)
    {
        TreeType speciesFloor = FindSpeciesImprovementFloor(_results);

        CountVisibleLogs(_results, speciesFloor);

        for (int i = 1; i < _results.Count; i++)
        {
            IStaticCollidable current = _results[i];
            long currentPriority = GetPickupPriority(current, speciesFloor);

            int j = i - 1;
            while (j >= 0 && GetPickupPriority(_results[j], speciesFloor) < currentPriority)
            {
                _results[j + 1] = _results[j];
                j--;
            }

            _results[j + 1] = current;
        }
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
        if (!(_collidable is LogItem logItem)) return long.MaxValue;

        TreeType treeType = EffectiveTreeType(logItem, _speciesFloor);

        int index = VisibleIndex(treeType, logItem.logState);
        int visibleCount = (index >= 0 && index < visibleCounts.Length) ? visibleCounts[index] : 1;

        return LogValue.GetGroupValue(treeType, logItem.logState, visibleCount);
    }
}
