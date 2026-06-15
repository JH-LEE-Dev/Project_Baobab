using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class HUD_NavigationSubField : MonoBehaviour
{
    // 이벤트
    public event Action subRegionSelectedEvent;

    // 외부 의존성
    [Header("UI Elements")]
    [SerializeField] private GameObject subRegionPrefab;
    [SerializeField] private RectTransform subRegionContainer;
    [SerializeField] private float subRegionAppearDelayGap = 0.1f;

    [Header("Random Layout Config")]
    [SerializeField] private float minDistanceMultiplier = 2.5f;

    // 내부 의존성
    private readonly List<HUD_NavigationSubRegion> spawnedSubRegions = new List<HUD_NavigationSubRegion>(maxSubRegionCount);
    private Action<int> onSubRegionSelectedCallback;
    private int currentSelectedNumber = -1;
    private HUD_Vehicle vehicle;

    public bool IsInputBlocked => vehicle != null && vehicle.IsUnlockingProductionActive;
    private bool isInitialized = false;
    private TweenCallback onSubRegionDisappearCompleteCallback;
    private int disappearCompletedCount = 0;
    private int disappearActiveCount = 0;
    private Action allDisappearCompleteCallback;

    // 캐싱된 상수 및 리터럴 값
    private const int maxSubRegionCount = 3;
    private const int maxSafetyAttempts = 10;
    private const int maxOverlapAttempts = 50;
    private const float defaultSubRegionSize = 100f;
    private const float distanceDecayRate = 0.8f;
    private static readonly Vector2 centerAnchorAndPivot = new Vector2(0.5f, 0.5f);


    // 퍼블릭 초기화 및 제어 메서드

    public void Initialize(HUD_Vehicle _vehicle)
    {
        if (true == isInitialized)
            return;

        vehicle = _vehicle;
        currentSelectedNumber = -1;
        onSubRegionSelectedCallback = OnSubRegionSelected;
        onSubRegionDisappearCompleteCallback = OnSubRegionDisappearComplete;

        for (int i = 0; i < spawnedSubRegions.Count; i++)
        {
            if (null != spawnedSubRegions[i])
            {
                spawnedSubRegions[i].ResetAnimation();
                spawnedSubRegions[i].gameObject.SetActive(false);
            }
        }

        isInitialized = true;
    }

    public void SetSubRegions(MapType _mapType, List<ForestEnvironmentInfo> _forestDatas)
    {
        if (false == isInitialized)
            Initialize(null);

        currentSelectedNumber = -1;

        if (null == subRegionPrefab || null == subRegionContainer || null == _forestDatas)
            return;

        int dataCount = _forestDatas.Count;

        // 항상 최대 3개의 SubRegion 버튼을 보장하여 생성 및 풀링
        int safetyCounter = 0;
        while (maxSubRegionCount > spawnedSubRegions.Count && maxSafetyAttempts > safetyCounter)
        {
            safetyCounter++;
            GameObject obj = Instantiate(subRegionPrefab, subRegionContainer);
            if (null == obj)
                break;

            HUD_NavigationSubRegion sub = obj.GetComponent<HUD_NavigationSubRegion>();
            if (null != sub)
                spawnedSubRegions.Add(sub);
            else
            {
                Destroy(obj);
                break;
            }
        }

        for (int i = 0; i < spawnedSubRegions.Count; i++)
        {
            if (null == spawnedSubRegions[i])
                continue;

            if (maxSubRegionCount > i && dataCount > i)
            {
                spawnedSubRegions[i].PlayOpenAnimation();
                spawnedSubRegions[i].Setup(_forestDatas[i], i + 1, null, null, onSubRegionSelectedCallback, this);
                spawnedSubRegions[i].PlayAppearAnimation(i * subRegionAppearDelayGap);
            }
            else
                spawnedSubRegions[i].PlayCloseAnimation();
        }

        RepositionSubRegions(_mapType, dataCount);
    }

    private void RepositionSubRegions(MapType _mapType, int _activeCount)
    {
        if (null == subRegionContainer || 0 >= _activeCount)
            return;

        int count = _activeCount;
        if (maxSubRegionCount < count)
            count = maxSubRegionCount;

        float containerWidth = subRegionContainer.rect.width;
        float containerHeight = subRegionContainer.rect.height;

        float subWidth = defaultSubRegionSize;
        float subHeight = defaultSubRegionSize;

        for (int i = 0; i < spawnedSubRegions.Count; i++)
        {
            if (null != spawnedSubRegions[i])
            {
                RectTransform subRect = spawnedSubRegions[i].GetRectTransform();
                if (null != subRect)
                {
                    subWidth = subRect.rect.width;
                    subHeight = subRect.rect.height;
                    break;
                }
            }
        }

        float minX = -containerWidth / 2f + subWidth / 2f;
        float maxX = containerWidth / 2f - subWidth / 2f;
        float minY = -containerHeight / 2f + subHeight / 2f;
        float maxY = containerHeight / 2f - subHeight / 2f;

        if (minX > maxX)
        {
            float temp = minX;
            minX = maxX;
            maxX = temp;
        }
        if (minY > maxY)
        {
            float temp = minY;
            minY = maxY;
            maxY = temp;
        }

        // 격리 최소 거리 기준을 대폭 강화
        float baseMinDistance = Mathf.Max(subWidth, subHeight) * minDistanceMultiplier;

        // 전역 난수 흐름 오염 방지 및 결정론적 배치 고정을 위한 시드 처리
        UnityEngine.Random.State prevState = UnityEngine.Random.state;
        UnityEngine.Random.InitState((int)_mapType);

        // X축 3분할 영역 가로폭 계산
        float totalWidth = maxX - minX;
        float sectionWidth = totalWidth / 3f;

        Span<Vector2> positions = stackalloc Vector2[maxSubRegionCount];
        int posIndex = 0;

        for (int i = 0; i < spawnedSubRegions.Count; i++)
        {
            if (null == spawnedSubRegions[i] || i >= count)
                continue;

            if (count <= posIndex)
                break;

            RectTransform subRect = spawnedSubRegions[i].GetRectTransform();
            if (null == subRect)
                continue;

            subRect.anchorMin = centerAnchorAndPivot;
            subRect.anchorMax = centerAnchorAndPivot;
            subRect.pivot = centerAnchorAndPivot;

            // 해당 인덱스(posIndex)에 따른 X축 영역 슬라이싱
            float minRangeX = minX + sectionWidth * posIndex;
            float maxRangeX = minRangeX + sectionWidth;

            Vector2 targetPos = Vector2.zero;
            bool found = false;
            float currentMinDistance = baseMinDistance;

            for (int attempt = 0; maxOverlapAttempts > attempt; attempt++)
            {
                // 분할 영역 내에서 난수 추첨
                float randX = UnityEngine.Random.Range(minRangeX, maxRangeX);
                float randY = UnityEngine.Random.Range(minY, maxY);
                Vector2 candidate = new Vector2(randX, randY);

                bool isOverlap = false;
                for (int j = 0; j < posIndex; j++)
                {
                    if (currentMinDistance > Vector2.Distance(candidate, positions[j]))
                    {
                        isOverlap = true;
                        break;
                    }
                }

                if (false == isOverlap)
                {
                    targetPos = candidate;
                    found = true;
                    break;
                }

                if (0 < attempt && 0 == attempt % 10)
                    currentMinDistance *= distanceDecayRate;
            }

            if (false == found)
                targetPos = new Vector2(UnityEngine.Random.Range(minRangeX, maxRangeX), UnityEngine.Random.Range(minY, maxY));

            positions[posIndex] = targetPos;
            subRect.anchoredPosition = targetPos;
            posIndex++;
        }

        // 난수 상태 복구
        UnityEngine.Random.state = prevState;
    }

    public HUD_NavigationSubRegion GetSubRegionInstance(ForestType _forestType)
    {
        for (int i = 0; i < spawnedSubRegions.Count; i++)
        {
            if (null != spawnedSubRegions[i] && spawnedSubRegions[i].GetForestType() == _forestType)
                return spawnedSubRegions[i];
        }
        return null;
    }

    public ForestType GetSelectedForestType()
    {
        int index = currentSelectedNumber - 1;
        if (null == spawnedSubRegions || 0 > index || spawnedSubRegions.Count <= index)
            return ForestType.None;

        if (null == spawnedSubRegions[index])
            return ForestType.None;

        return spawnedSubRegions[index].GetForestType();
    }

    public ForestEnvironmentInfo GetSelectedForestInfo()
    {
        int index = currentSelectedNumber - 1;
        if (null == spawnedSubRegions || 0 > index || spawnedSubRegions.Count <= index)
            return default;

        if (null == spawnedSubRegions[index])
            return default;

        return spawnedSubRegions[index].GetForestInfo();
    }

    public void ResetSelection()
    {
        currentSelectedNumber = -1;

        for (int i = 0; i < spawnedSubRegions.Count; i++)
        {
            if (null != spawnedSubRegions[i])
            {
                spawnedSubRegions[i].SetSelect(false);
                spawnedSubRegions[i].ResetAnimation();
                spawnedSubRegions[i].PlayCloseAnimation();
            }
        }

        subRegionSelectedEvent?.Invoke();
    }

    public void PlayDisappearAnimations(Action _onComplete)
    {
        int activeCount = 0;
        for (int i = 0; i < spawnedSubRegions.Count; i++)
        {
            if (null != spawnedSubRegions[i] && true == spawnedSubRegions[i].gameObject.activeSelf)
                activeCount++;
        }

        if (0 == activeCount)
        {
            _onComplete?.Invoke();
            return;
        }

        disappearCompletedCount = 0;
        disappearActiveCount = activeCount;
        allDisappearCompleteCallback = _onComplete;

        int delayIndex = 0;

        for (int i = spawnedSubRegions.Count - 1; i >= 0; i--)
        {
            HUD_NavigationSubRegion sub = spawnedSubRegions[i];
            if (null == sub || false == sub.gameObject.activeSelf)
                continue;

            float delay = delayIndex * subRegionAppearDelayGap;
            delayIndex++;

            sub.PlayDisappearAnimation(delay, onSubRegionDisappearCompleteCallback);
        }
    }

    private void OnSubRegionDisappearComplete()
    {
        disappearCompletedCount++;
        if (disappearActiveCount == disappearCompletedCount)
        {
            for (int j = 0; j < spawnedSubRegions.Count; j++)
                if (null != spawnedSubRegions[j])
                    spawnedSubRegions[j].PlayCloseAnimation();

            allDisappearCompleteCallback?.Invoke();
            allDisappearCompleteCallback = null;
        }
    }


    private void OnSubRegionSelected(int _number)
    {
        currentSelectedNumber = _number;

        if (null != spawnedSubRegions)
        {
            for (int i = 0; i < spawnedSubRegions.Count; i++)
            {
                if (null != spawnedSubRegions[i])
                    spawnedSubRegions[i].SetSelect(spawnedSubRegions[i].GetNumber() == _number);
            }
        }

        subRegionSelectedEvent?.Invoke();
    }


    // 유니티 이벤트 함수 (Awake, Start, OnDestroy 등 최하단 배치)

    private void OnDisable()
    {
        ResetSelection();
    }

    private void OnDestroy()
    {
    }
}
