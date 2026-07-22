using System;
using UnityEngine;
using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem;

public class HUD_PopupNav_TreeInfoView : MonoBehaviour
{
    [Header("Data Base")]
    [Tooltip("나무 비주얼 정보를 가져올 데이터베이스")]
    [SerializeField] private TreeVisualDataBase treeVisualDataBase;

    [Header("UI References")]
    [Tooltip("팝업 위치의 기준점이 될 자기 자신의 RectTransform")]
    [SerializeField] private RectTransform rectTransform;
    [Tooltip("나무 비주얼 슬롯 스크립트 목록 (1번 나무, 2번 나무)")]
    [SerializeField] private HUD_PopupNav_TreeProp[] treeProps;


    private bool isVisible = false;

    private void Awake()
    {
        gameObject.SetActive(false);
    }

    public void Initialize()
    {
        gameObject.SetActive(false);
        isVisible = false;

        // 인스펙터 연결이 누락되었거나, 씬 오브젝트가 아닌 프리팹(프로젝트 에셋)이 잘못 연결된 경우를 대비한 자동 복구
        bool _needsAutoFind = (null == treeProps || 0 == treeProps.Length);
        if (false == _needsAutoFind && null != treeProps[0] && null == treeProps[0].gameObject.scene.name)
        {
            _needsAutoFind = true;
        }

        if (true == _needsAutoFind)
        {
            treeProps = GetComponentsInChildren<HUD_PopupNav_TreeProp>(true);
            Debug.Log($"[TreeInfoView] TreeProps 인스펙터 연결이 잘못되어 자식 오브젝트에서 자동으로 {treeProps.Length}개를 찾아서 복구했습니다!");
        }

        if (null != treeProps)
        {
            for (int i = 0; i < treeProps.Length; i++)
            {
                if (null != treeProps[i])
                {
                    treeProps[i].Initialize();
                    treeProps[i].gameObject.SetActive(false);
                }
            }
        }
    }

    [Header("Settings")]
    [Tooltip("서브지역 버튼에서 떨어질 간격 오프셋")]
    [SerializeField] private Vector2 anchorOffset = new Vector2(0f, 150f);

    public void SetVisibility(bool _isVisible)
    {
        if (isVisible == _isVisible && _isVisible == gameObject.activeSelf)
        {
            return;
        }

        isVisible = _isVisible;

        if (true == _isVisible)
        {
            gameObject.SetActive(true);
        }
        else
        {
            OnDisappearMotionComplete();
        }
    }

    private void OnDisappearMotionComplete()
    {
        gameObject.SetActive(false);
    }

    public void ShowTreeInfo(ForestEnvironmentInfo _info, Transform _subRegionTransform)
    {
        Debug.Log($"[TreeInfoView] ShowTreeInfo 호출됨. 설정된 나무 종류 개수: {(_info.spawnTreeTypes != null ? _info.spawnTreeTypes.Count.ToString() : "null")}");

        if (null == treeVisualDataBase)
        {
            Debug.LogWarning("[TreeInfoView] TreeVisualDataBase가 인스펙터에 연결되지 않았습니다!");
            return;
        }

        if (null == _info.spawnTreeTypes || 0 == _info.spawnTreeTypes.Count)
        {
            Debug.LogWarning("[TreeInfoView] 해당 서브지역의 spawnTreeTypes 정보가 없습니다!");
            return;
        }

        if (null != rectTransform && null != _subRegionTransform)
        {
            rectTransform.position = _subRegionTransform.position;
            rectTransform.anchoredPosition += anchorOffset;
        }

        UpdateVisuals(_info);
        SetVisibility(true);
    }

    private void UpdateVisuals(ForestEnvironmentInfo _info)
    {
        if (null == treeProps)
        {
            return;
        }

        int _dataCount = _info.spawnTreeTypes.Count;
        for (int i = 0; i < treeProps.Length; i++)
        {
            HUD_PopupNav_TreeProp _prop = treeProps[i];
            if (null == _prop)
            {
                continue;
            }

            if (i < _dataCount)
            {
                TreeType _targetTreeType = _info.spawnTreeTypes[i].treeType;
                TreeVisualData _visualData = treeVisualDataBase.Get(_targetTreeType);
                
                if (TreeType.None == _visualData.treeType)
                {
                    Debug.LogWarning($"[TreeInfoView] '{_targetTreeType}' 타입의 나무 비주얼 데이터를 TreeVisualDataBase에서 찾을 수 없습니다! 데이터베이스를 확인해주세요.");
                }

                _prop.Setup(_visualData);
            }
            else
            {
                _prop.gameObject.SetActive(false);
            }
        }
    }
}
