using System;
using System.Collections.Generic;
using UnityEngine;

public class HUD_NavigationSubField : MonoBehaviour
{
    // //외부 의존성
    [Header("UI Elements")]
    [SerializeField] private GameObject subRegionPrefab;
    [SerializeField] private Transform subRegionContainer;

    // //내부 의존성
    private readonly List<HUD_NavigationSubRegion> spawnedSubRegions = new List<HUD_NavigationSubRegion>(3);
    private int currentSelectedNumber = -1;
    private bool isInitialized = false;


    // //퍼블릭 초기화 및 제어 메서드

    public void Initialize()
    {
        if (true == isInitialized)
            return;

        currentSelectedNumber = -1;

        for (int i = 0; i < spawnedSubRegions.Count; i++)
            if (null != spawnedSubRegions[i])
                spawnedSubRegions[i].gameObject.SetActive(false);

        isInitialized = true;
    }

    public void SetSubRegions(List<ForestEnvironmentInfo> _forestDatas)
    {
        if (false == isInitialized)
            Initialize();

        currentSelectedNumber = -1;

        if (null == subRegionPrefab || null == subRegionContainer || null == _forestDatas)
            return;

        // 항상 최대 3개의 SubRegion 버튼을 보장하여 생성 및 풀링
        while (spawnedSubRegions.Count < 3)
        {
            GameObject obj = Instantiate(subRegionPrefab, subRegionContainer);
            if (null == obj)
                break;

            HUD_NavigationSubRegion sub = obj.GetComponent<HUD_NavigationSubRegion>();
            if (null != sub)
                spawnedSubRegions.Add(sub);
        }

        int dataCount = _forestDatas.Count;
        for (int i = 0; i < spawnedSubRegions.Count; i++)
        {
            if (null == spawnedSubRegions[i])
                continue;

            if (i < 3 && i < dataCount)
            {
                spawnedSubRegions[i].PlayOpenAnimation();
                spawnedSubRegions[i].Setup(_forestDatas[i], i + 1, OnSubRegionHoverEntered, OnSubRegionHoverExited, OnSubRegionSelected);
            }
            else
                spawnedSubRegions[i].PlayCloseAnimation();
        }
    }

    public ForestType GetSelectedForestType()
    {
        int index = currentSelectedNumber - 1;
        if (null == spawnedSubRegions || index < 0 || index >= spawnedSubRegions.Count)
            return ForestType.None;

        if (null == spawnedSubRegions[index])
            return ForestType.None;

        return spawnedSubRegions[index].GetForestType();
    }


    // //내부 로직 (콜백 메서드)

    private void OnSubRegionHoverEntered(RectTransform _targetRect, Vector2 _targetSize)
    {
        // 호버 시 부가 연출이 필요하다면 구현 가능
    }

    private void OnSubRegionHoverExited()
    {
        // 호버 해제 시 부가 연출
    }

    private void OnSubRegionSelected(int _number)
    {
        currentSelectedNumber = _number;

        if (null != spawnedSubRegions)
            for (int i = 0; i < spawnedSubRegions.Count; i++)
                if (null != spawnedSubRegions[i])
                    spawnedSubRegions[i].SetSelect(spawnedSubRegions[i].GetNumber() == _number);
    }
}
