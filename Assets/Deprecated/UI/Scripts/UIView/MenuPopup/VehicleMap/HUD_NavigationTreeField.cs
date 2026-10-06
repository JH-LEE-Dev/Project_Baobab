using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class HUD_NavigationTreeField : MonoBehaviour
{
    // 이벤트
    public event Action<TreeType> treeSelectedEvent;

    // 외부 의존성
    [SerializeField] private GameObject treePropPrefab;
    [SerializeField] private RectTransform treeContainer;
    [SerializeField] private float treeAppearDelayGap = 0.1f;
    [SerializeField] private float treeDisappearDelayGap = 0.05f;

    // 내부 의존성
    private readonly HUD_NavigationTreeProp[] spawnedTreeProps = new HUD_NavigationTreeProp[maxTreePropCount];
    private Action<TreeType> onTreeSelectedCallback;
    private bool isInitialized = false;
    private TweenCallback onTreeDisappearCompleteCallback;
    private int disappearCompletedCount = 0;
    private int disappearActiveCount = 0;
    private Action allDisappearCompleteCallback;
    private LocalizationManager localizationManager;

    // 캐싱된 상수 및 리터럴 값
    private const int maxTreePropCount = 3;


    // 퍼블릭 초기화 및 제어 메서드

    public void Initialize(LocalizationManager _localizeManager = null)
    {
        if (true == isInitialized)
            return;

        localizationManager = _localizeManager;
        onTreeSelectedCallback = OnTreeSelected;
        onTreeDisappearCompleteCallback = OnTreeDisappearComplete;

        // 프리팹을 기반으로 3개의 나무 객체를 지정된 컨테이너의 자식으로 인스턴스화
        if (null != treePropPrefab)
        {
            Transform parentTransform = null != treeContainer ? treeContainer : transform;

            for (int i = 0; i < maxTreePropCount; i++)
            {
                GameObject obj = Instantiate(treePropPrefab, parentTransform);
                if (null != obj)
                {
                    HUD_NavigationTreeProp prop = obj.GetComponent<HUD_NavigationTreeProp>();
                    if (null != prop)
                    {
                        prop.Initialize();
                        prop.gameObject.SetActive(false);
                        spawnedTreeProps[i] = prop;
                    }
                    else
                    {
                        Destroy(obj);
                    }
                }
            }
        }

        isInitialized = true;
    }

    public void SetTreeField(ForestEnvironmentInfo _info)
    {
        if (false == isInitialized)
            Initialize();

        gameObject.SetActive(true);

        if (null == _info.spawnTreeTypes)
            return;

        int dataCount = _info.spawnTreeTypes.Count;

        for (int i = 0; i < maxTreePropCount; i++)
        {
            HUD_NavigationTreeProp prop = spawnedTreeProps[i];
            if (null == prop)
                continue;

            if (dataCount > i)
            {
                prop.gameObject.SetActive(true);
                prop.Setup(_info.spawnTreeTypes[i].treeType, onTreeSelectedCallback, localizationManager);
                prop.PlayAppearAnimation(i * treeAppearDelayGap);
            }
            else
            {
                prop.gameObject.SetActive(false);
            }
        }
    }

    public void PlayDisappearAnimations(Action _onComplete)
    {
        int activeCount = 0;
        List<HUD_NavigationTreeProp> activeProps = new List<HUD_NavigationTreeProp>(maxTreePropCount);

        for (int i = 0; i < maxTreePropCount; i++)
        {
            HUD_NavigationTreeProp prop = spawnedTreeProps[i];
            if (null != prop && true == prop.gameObject.activeSelf)
            {
                activeProps.Add(prop);
                activeCount++;
            }
        }

        if (0 == activeCount)
        {
            gameObject.SetActive(false);
            _onComplete?.Invoke();
            return;
        }

        disappearCompletedCount = 0;
        disappearActiveCount = activeCount;
        allDisappearCompleteCallback = _onComplete;

        int delayIndex = 0;

        for (int i = activeProps.Count - 1; i >= 0; i--)
        {
            HUD_NavigationTreeProp prop = activeProps[i];
            if (null == prop)
                continue;

            float delay = delayIndex * treeDisappearDelayGap;
            delayIndex++;

            prop.PlayDisappearAnimation(delay, onTreeDisappearCompleteCallback);
        }
    }

    private void OnTreeDisappearComplete()
    {
        disappearCompletedCount++;
        if (disappearActiveCount == disappearCompletedCount)
        {
            ResetSelection();
            allDisappearCompleteCallback?.Invoke();
            allDisappearCompleteCallback = null;
        }
    }

    public void ResetSelection()
    {
        for (int i = 0; i < maxTreePropCount; i++)
        {
            HUD_NavigationTreeProp prop = spawnedTreeProps[i];
            if (null != prop)
            {
                prop.ResetAnimation();
                prop.gameObject.SetActive(false);
            }
        }

        gameObject.SetActive(false);
    }


    // 내부 로직

    private void OnTreeSelected(TreeType _treeType)
    {
        treeSelectedEvent?.Invoke(_treeType);
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
