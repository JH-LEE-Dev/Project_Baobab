using System;
using System.Collections.Generic;
using UnityEngine;
using PresentationLayer.DOTweenAnimationSystem;
using DG.Tweening;

public class UI_Storage : MonoBehaviour
{
    // //외부 의존성
    [SerializeField] private GameObject uiSlotPrefab;
    [SerializeField] private GameObject slotBackground;
    [SerializeField] private ObjectMotionPlayer omp;
    [SerializeField] private Vector2 offset;

    [Header("Dynamic Positioning")]
    [SerializeField] private bool useDynamicPositioning = true;
    [SerializeField] private float tweenDuration = 0.3f;
    [SerializeField] private Ease easeType = Ease.OutQuad;

    // //내부 의존성
    private const int defaultCap = 2;

    // 캐릭터가 보관함과 X축상 거의 같은 위치(예: 바로 위/아래)에 있을 때, 여유(deadzone) 없이 딱 붙은
    // 부등호로만 좌/우를 판정하면 아주 미세한 위치 변화만으로도 판정이 계속 뒤바뀌어 UI가 좌우로
    // 떨리듯 튀는 문제가 있었다. 이 임계값을 넘어 확실히 한쪽으로 벗어났을 때만 좌/우 상태를 갱신한다.
    private const float POSITION_DEADZONE = 0.15f;

    private IInventory storage;
    private List<UI_InventorySlot> storageSlots;
    public bool IsOpening { get; private set; } = false;
    public bool IsOpen => isOnShow;

    [SerializeField] private string popupTag = "Popup";
    [SerializeField] private string popdownTag = "Popdown";
    [SerializeField] private string popdownLeftTag = "PopdownLeft";
    [SerializeField] private string popdownRightTag = "PopdownRight";

    private MotionEntry popup;
    private MotionEntry popdown;
    private RectTransform rect;
    private Transform playerTransform;
    private Tween positioningTween;
    private bool isPlayerOnLeft = true;
    private bool isPendingHide = false;

    private bool isOnShow = false;
    public bool IsCollShow { get; set; } = false;


    // //퍼블릭 초기화 및 제어 메서드

    public void Initialize(Vector2 _offset)
    {
        storageSlots = new List<UI_InventorySlot>(SYSTEM_VAR.MAX_STORAGE_CNT);
        gameObject.SetActive(false);
        offset = _offset;

        if (null != omp)
            omp.Initialize();

        rect = GetComponent<RectTransform>();

        SnapToPerfectPixel();
    }

    public void BindStorage(IInventory _storage)
    {
        storage = _storage;
        if (null != storage)
            UpdateMaxSlotCount(storage.inventorySlots.Count);
    }

    public void BindPlayer(Transform _playerTrans)
    {
        playerTransform = _playerTrans;
    }

    public void UpdateMaxSlotCount(int _cnt)
    {
        if (null == uiSlotPrefab)
            return;

        int needCount = _cnt - storageSlots.Count;

        while (0 < needCount--)
        {
            UI_InventorySlot slot = Instantiate(uiSlotPrefab, slotBackground.transform).GetComponent<UI_InventorySlot>();

            if (null == slot)
                return;

            slot.Initialize();
            slot.DisableRayCast();

            storageSlots.Add(slot);
        }

        SnapToPerfectPixel();
    }

    public void Refresh()
    {
        if (null == storage)
            return;

        UpdateMaxSlotCount(storage.inventorySlots.Count);
        UpdateSlots(storage.inventorySlots);

        // 슬롯 구성이 바뀌면서 가로폭이 변경되었을 수 있으므로, 현재 노출 중이면 위치를 재계산한다.
        if (true == isOnShow && true == useDynamicPositioning && null != rect)
            rect.position = GetTargetWorldPosition();

        SnapToPerfectPixel();
    }

    public void UpdateSlots(IReadOnlyList<IInventorySlot> _items = null)
    {
        if (null == _items && null == storage)
            return;

        if (null == _items)
            _items = storage.inventorySlots;

        int itemCount = storage.currentSlotCnt;

        for (int i = 0; i < storageSlots.Count; ++i)
        {
            UI_InventorySlot slot = storageSlots[i];
            IInventorySlot item = _items[i];

            slot.gameObject.SetActive(i < itemCount);
            slot.UpdateBindSlotData(item, storage.maxItemCntPerSlot);
        }
    }

    public void OnShow()
    {
        if (true == isOnShow)
            return;

        gameObject.SetActive(IsOpening = true);
        isPendingHide = false;
        isOnShow = true;

        if (null != positioningTween && true == positioningTween.IsActive())
            positioningTween.Kill();

        if (null != rect && null != storage)
        {
            Vector3 _storagePos = storage.GetTransform().position;
            if (null != playerTransform)
                isPlayerOnLeft = (playerTransform.position.x < _storagePos.x);

            rect.position = GetTargetWorldPosition();
        }

        SnapToPerfectPixel();

        if (null == omp)
            return;

        omp.SettingEntryMotion(popdown, true, true);
        popup = omp.Play(popupTag, bReset: true);
    }

    public void OnHide()
    {
        isOnShow = false;

        if (true == useDynamicPositioning && null != positioningTween && true == positioningTween.IsActive() && true == positioningTween.IsPlaying())
        {
            isPendingHide = true;
            return;
        }

        if (false == IsCollShow)
            StartHideMotion();
    }

    // //내부 로직

    private void OnCompleteAnim()
    {
        gameObject.SetActive(IsOpening = false);
    }

    private void OnPositioningTweenComplete()
    {
        SnapToPerfectPixel();

        if (true == isPendingHide)
            StartHideMotion();
    }

    private void StartHideMotion()
    {
        isPendingHide = false;

        if (null != positioningTween && true == positioningTween.IsActive())
            positioningTween.Kill();

        if (null == omp)
        {
            gameObject.SetActive(IsOpening = false);
            return;
        }

        string _targetPopdownTag = popdownTag;

        if (true == useDynamicPositioning)
            _targetPopdownTag = (true == isPlayerOnLeft) ? popdownLeftTag : popdownRightTag;

        omp.SettingEntryMotion(popup, true, true);
        popdown = omp.Play(_targetPopdownTag, bReset: true, _onComplete: OnCompleteAnim);
    }

    /// <summary>
    /// 캐릭터의 실시간 X축 위치를 분석하여, 보관함의 좌우 반대편에 대응하는 UI 타겟 월드 좌표를 산출합니다.
    /// 슬롯이 가변적으로 늘어나도 UI의 끝점(edge) 기준으로 밀려나도록 반폭을 반영합니다.
    /// </summary>
    private Vector3 GetTargetWorldPosition()
    {
        if (null == storage)
            return Vector3.zero;

        Vector3 _storagePos = storage.GetTransform().position;
        float _targetOffsetX = offset.x;

        if (true == useDynamicPositioning && null != playerTransform)
        {
            // 슬롯 변경 직후에도 정확한 가로폭을 얻기 위해 레이아웃을 강제 갱신한다.
            Canvas.ForceUpdateCanvases();

            float _absX = Mathf.Abs(offset.x);

            // UI의 실제 가로 반폭(월드 단위)을 계산하여 끝점(edge) 기준으로 오프셋 적용
            float _halfWorldWidth = 0f;
            if (null != slotBackground)
            {
                RectTransform _bgRect = slotBackground.GetComponent<RectTransform>();
                if (null != _bgRect)
                    _halfWorldWidth = _bgRect.rect.width * _bgRect.lossyScale.x * 0.5f;
            }

            float _totalOffset = _absX + _halfWorldWidth;
            _targetOffsetX = (_storagePos.x > playerTransform.position.x) ? _totalOffset : -_totalOffset;
        }

        Vector3 _targetPos = _storagePos;
        _targetPos.x += _targetOffsetX;
        _targetPos.y += offset.y;

        return _targetPos;
    }

    /// <summary>
    /// 캐릭터의 좌우 상태 변경이 일어난 정확한 트리거 프레임 시점에 Ease 곡선 트윈을 기동합니다.
    /// </summary>
    private void TriggerPositioningTween()
    {
        if (null == rect)
            return;

        if (null != positioningTween && true == positioningTween.IsActive())
            positioningTween.Kill();

        Vector3 _targetPos = GetTargetWorldPosition();

        positioningTween = rect.DOMove(_targetPos, tweenDuration)
            .SetEase(easeType)
            .SetAutoKill(true)
            .OnUpdate(SnapToPerfectPixel)
            .OnComplete(OnPositioningTweenComplete);
    }

    /// <summary>
    /// slotBackground RectTransform의 가로/세로 크기(홀수/짝수)와 피봇(0.5, 0, 1) 설정에 맞추어
    /// UI 렌더링 시 픽셀 경계가 뭉개지지 않고 선명하게 출력(Pixel-perfect)되도록 anchoredPosition을 스냅 정렬합니다.
    /// </summary>
    private void SnapToPerfectPixel()
    {
        if (null == slotBackground)
            return;

        // 캔버스를 즉각 강제 갱신하여 비활성화 ➡️ 활성화 전환 직후 프레임 지연으로 크기(Width/Height)가 0으로 잡히는 버그를 완벽히 해결합니다.
        Canvas.ForceUpdateCanvases();

        RectTransform _bgRect = slotBackground.GetComponent<RectTransform>();
        if (null == _bgRect)
            return;

        if (null == rect)
            rect = GetComponent<RectTransform>();

        if (null == rect)
            return;

        Vector2 _pos = rect.anchoredPosition;
        float _width = _bgRect.rect.width;
        float _height = _bgRect.rect.height;
        float _pivotX = _bgRect.pivot.x;
        float _pivotY = _bgRect.pivot.y;

        // 1. X축 스냅 (가로 크기 홀짝 분석 및 피봇 0.5 / 0 / 1 정밀 매칭)
        int _roundedWidth = Mathf.RoundToInt(_width);
        bool _isWidthOdd = (0 != _roundedWidth % 2);

        if (true == _isWidthOdd)
        {
            if (0.01f > Mathf.Abs(_pivotX - 0.5f))
                _pos.x = Mathf.Round(_pos.x - 0.5f) + 0.5f;
            else if (false == (0.01f > Mathf.Abs(_pivotX - 0.5f)) && (0.01f > Mathf.Abs(_pivotX - 0f) || 0.01f > Mathf.Abs(_pivotX - 1f)))
                _pos.x = Mathf.Round(_pos.x);
        }
        else
        {
            if (0.01f > Mathf.Abs(_pivotX - 0.5f) || 0.01f > Mathf.Abs(_pivotX - 0f) || 0.01f > Mathf.Abs(_pivotX - 1f))
                _pos.x = Mathf.Round(_pos.x);
        }

        // 2. Y축 스냅 (세로 크기 홀짝 분석 및 피봇 0.5 / 0 / 1 정밀 매칭)
        int _roundedHeight = Mathf.RoundToInt(_height);
        bool _isHeightOdd = (0 != _roundedHeight % 2);

        if (true == _isHeightOdd)
        {
            if (0.01f > Mathf.Abs(_pivotY - 0.5f))
                _pos.y = Mathf.Round(_pos.y - 0.5f) + 0.5f;
            else if (false == (0.01f > Mathf.Abs(_pivotY - 0.5f)) && (0.01f > Mathf.Abs(_pivotY - 0f) || 0.01f > Mathf.Abs(_pivotY - 1f)))
                _pos.y = Mathf.Round(_pos.y);
        }
        else
        {
            if (0.01f > Mathf.Abs(_pivotY - 0.5f) || 0.01f > Mathf.Abs(_pivotY - 0f) || 0.01f > Mathf.Abs(_pivotY - 1f))
                _pos.y = Mathf.Round(_pos.y);
        }

        rect.anchoredPosition = _pos;
    }


    // //유니티 이벤트 함수 (Awake, Start, OnDestroy 등 최하단 배치)

    private void Update()
    {
        if (true == IsOpening && null != rect && null != storage && null != playerTransform && true == useDynamicPositioning)
        {
            Vector3 _storagePos = storage.GetTransform().position;
            float _deltaX = playerTransform.position.x - _storagePos.x;

            // 임계값 안에 있으면(거의 같은 X좌표) 좌/우 판정을 갱신하지 않고 이전 상태를 그대로 유지한다.
            if (Mathf.Abs(_deltaX) < POSITION_DEADZONE)
                return;

            bool _currentLeft = _deltaX < 0f;

            if (_currentLeft != isPlayerOnLeft)
            {
                isPlayerOnLeft = _currentLeft;
                TriggerPositioningTween();
            }
        }
    }

    private void OnDestroy()
    {
        if (null != positioningTween && true == positioningTween.IsActive())
            positioningTween.Kill();
    }
}
