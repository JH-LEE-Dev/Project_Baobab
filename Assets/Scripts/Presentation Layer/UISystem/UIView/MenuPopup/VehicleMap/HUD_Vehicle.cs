using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem;

public class HUD_Vehicle : MonoBehaviour
{
    // //이벤트
    public event Action<MapType, ForestType> MapSelectedEvent;

    // //외부 의존성
    [SerializeField] private Image lightImage;
    [SerializeField] private ObjectMotionPlayer omp;
    [SerializeField] private HUD_VehicleNavigation navigation;
    [SerializeField] private HUD_NavigationSubField subField;
    [SerializeField] private HUD_VehicleMapSelectorButton okButton;
    [SerializeField] private HUD_VehicleMapSelectorButton cancelButton;

    [SerializeField] private string blinkMotionTag = "Blink";
    [SerializeField] private string backgroundMotionTag = "Background";
    [SerializeField] private string controlBoardMotionTag = "ControlBoard";

    // //내부 의존성
    private IMapDataProvider mapDataProvider;
    private Tweener blinkTween;
    private bool isBlinking = false;


    // //퍼블릭 초기화 및 제어 메서드

    public void Initialize(IMapDataProvider _mapDataProvider, Action _onClose)
    {
        isBlinking = false;

        if (null != blinkTween && blinkTween.IsActive())
            blinkTween.Kill();

        if (null != lightImage)
            lightImage.color = new Color(lightImage.color.r, lightImage.color.g, lightImage.color.b, 0f);

        mapDataProvider = _mapDataProvider;

        if (null != navigation)
        {
            navigation.Initialize(mapDataProvider);
            navigation.regionSelectedEvent -= HandleRegionSelected;
            navigation.regionSelectedEvent += HandleRegionSelected;
        }

        if (null != subField)
        {
            subField.Initialize();
            subField.subRegionSelectedEvent -= HandleSubRegionSelected;
            subField.subRegionSelectedEvent += HandleSubRegionSelected;
        }

        if (null != okButton)
        {
            okButton.Initialize(HandleConfirm);
            okButton.SetButtonActive(false, false);
        }

        if (null != cancelButton)
            cancelButton.Initialize(_onClose);

        if (null != omp)
            omp.Initialize();

        Close();
    }

    public void Open()
    {
        gameObject.SetActive(true);

        omp.Play(backgroundMotionTag, bReset: true);
        omp.Play(controlBoardMotionTag, bReset: true);
    }

    public void Close()
    {
        isBlinking = false;

        if (null != blinkTween && blinkTween.IsActive())
            blinkTween.Kill();

        if (null != lightImage)
            lightImage.color = new Color(lightImage.color.r, lightImage.color.g, lightImage.color.b, 0f);

        if (null != navigation)
            navigation.ResetSelection();

        if (null != subField)
            subField.ResetSelection();

        if (null != okButton)
            okButton.SetButtonActive(false, false);

        omp.PlayBackward(backgroundMotionTag, bReset: true);
        omp.PlayBackward(controlBoardMotionTag, bReset: true, _onComplete: HandleClose);
    }

    private void HandleClose() => gameObject.SetActive(false);

    // //내부 로직

    private void HandleInteractButtonClicked()
    {
        isBlinking = !isBlinking;

        if (null != blinkTween && blinkTween.IsActive())
            blinkTween.Kill();

        if (true == isBlinking)
        {
            if (null != lightImage)
            {
                lightImage.color = new Color(lightImage.color.r, lightImage.color.g, lightImage.color.b, 0f);
                blinkTween = lightImage.DOFade(1f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
            }
        }
        else
        {
            if (null != lightImage)
                lightImage.color = new Color(lightImage.color.r, lightImage.color.g, lightImage.color.b, 0f);
        }
    }

    private void HandleRegionSelected(MapType _mapType)
    {
        if (null == mapDataProvider || null == subField)
            return;

        MapEnvironmentDatabase db = mapDataProvider.GetMapEnvironmentDatabase();
        if (null == db.mapDatas)
            return;

        MapEnvironmentDataInfo targetInfo = default;
        bool isFound = false;
        for (int i = 0; i < db.mapDatas.Count; i++)
        {
            if (_mapType == db.mapDatas[i].mapType)
            {
                targetInfo = db.mapDatas[i];
                isFound = true;
                break;
            }
        }

        if (true == isFound && null != targetInfo.forestDatas)
            subField.SetSubRegions(_mapType, targetInfo.forestDatas);

        UpdateOkButtonState();
    }

    private void HandleSubRegionSelected()
    {
        UpdateOkButtonState();
    }

    private void UpdateOkButtonState()
    {
        if (null == okButton || null == navigation || null == subField)
            return;

        bool isRegionSelected = (MapType.None != navigation.GetSelectedMapType());
        bool isSubRegionSelected = (ForestType.None != subField.GetSelectedForestType());

        okButton.SetButtonActive(isRegionSelected && isSubRegionSelected, true);
    }

    private void HandleConfirm()
    {
        if (null == navigation || null == subField)
            return;

        MapType mapType = navigation.GetSelectedMapType();
        ForestType forestType = subField.GetSelectedForestType();

        if (MapType.None != mapType && ForestType.None != forestType)
            MapSelectedEvent?.Invoke(mapType, forestType);
    }

    private void HandleMapSelected(MapType _mapType, ForestType _forestType)
    {
        MapSelectedEvent?.Invoke(_mapType, _forestType);
    }


    // //유니티 이벤트 함수 (Awake, Start, OnDestroy 등 최하단 배치)

    private void OnDestroy()
    {
        if (null != navigation)
            navigation.regionSelectedEvent -= HandleRegionSelected;

        if (null != subField)
            subField.subRegionSelectedEvent -= HandleSubRegionSelected;
    }

    private void OnDisable()
    {
        isBlinking = false;

        if (null != blinkTween && blinkTween.IsActive())
            blinkTween.Kill();

        if (null != lightImage)
            lightImage.color = new Color(lightImage.color.r, lightImage.color.g, lightImage.color.b, 0f);

        if (null != omp)
            omp.ResetAllMotions();

        if (null != navigation)
            navigation.ResetSelection();

        if (null != subField)
            subField.ResetSelection();

        if (null != okButton)
            okButton.SetButtonActive(false, false);
    }
}
