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

    private IInventory storage;
    private List<UI_InventorySlot> storageSlots;
    public bool isOpening { get; private set; } = false;

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
    public bool isCollShow { get; set; } = false;


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

        gameObject.SetActive(isOpening = true);
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

        if (false == isCollShow)
            StartHideMotion();
    }

    // //내부 로직

    private void OnCompleteAnim()
    {
        gameObject.SetActive(isOpening = false);
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
            gameObject.SetActive(isOpening = false);
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
    /// </summary>
    private Vector3 GetTargetWorldPosition()
    {
        if (null == storage)
            return Vector3.zero;

        Vector3 _storagePos = storage.GetTransform().position;
        float _targetOffsetX = offset.x;

        if (true == useDynamicPositioning && null != playerTransform)
        {
            float _absX = Mathf.Abs(offset.x);
            _targetOffsetX = (playerTransform.position.x < _storagePos.x) ? _absX : -_absX;
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
            else if (0.01f > Mathf.Abs(_pivotX - 0.5f) == false && (0.01f > Mathf.Abs(_pivotX - 0f) || 0.01f > Mathf.Abs(_pivotX - 1f)))
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
            else if (0.01f > Mathf.Abs(_pivotY - 0.5f) == false && (0.01f > Mathf.Abs(_pivotY - 0f) || 0.01f > Mathf.Abs(_pivotY - 1f)))
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
        if (true == isOpening && null != rect && null != storage && null != playerTransform && true == useDynamicPositioning)
        {
            Vector3 _storagePos = storage.GetTransform().position;
            bool _currentLeft = (playerTransform.position.x < _storagePos.x);

            if (_currentLeft != isPlayerOnLeft)
            {
                isPlayerOnLeft = _currentLeft;
                TriggerPositioningTween();
            }
        }
    }
}
