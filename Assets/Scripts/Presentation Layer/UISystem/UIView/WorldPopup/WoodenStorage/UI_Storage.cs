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

    [Header("Smart Swap Settings")]
    [Tooltip("스마트 스왑 단일 인디케이터 프리팹 (Visuals 하위에 1회 인스턴스화)")]
    [SerializeField] private GameObject swapIndicatorPrefab;
    [Tooltip("인디케이터가 대상 슬롯 머리 위를 가리키는 로컬 오프셋")]
    [SerializeField] private Vector2 indicatorOffset = new Vector2(0f, 24f);

    private UI_SwapIndicator sharedSwapIndicator;
    private int currentSwapSlotIndex = -1;

    // //내부 의존성

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
    private bool isOpenAnimated = false;


    // //퍼블릭 초기화 및 제어 메서드

    public void Initialize(Vector2 _offset, InputManager _inputManager = null)
    {
        storageSlots = new List<UI_InventorySlot>(SYSTEM_VAR.MAX_STORAGE_CNT);
        gameObject.SetActive(false);
        offset = _offset;

        if (null != omp)
            omp.Initialize();

        rect = GetComponent<RectTransform>();

        SnapToPerfectPixel();

        InitSwapIndicator(_inputManager);

        LogSwapInfoChangedEvent -= HandleLogSwapInfoChanged;
        LogSwapInfoChangedEvent += HandleLogSwapInfoChanged;

        LogSwapExecutedEvent -= HandleLogSwapExecuted;
        LogSwapExecutedEvent += HandleLogSwapExecuted;
    }

    private void InitSwapIndicator(InputManager _inputManager)
    {
        if (null == sharedSwapIndicator && null != swapIndicatorPrefab)
        {
            Transform _parent = transform.Find("Visuals");
            if (null == _parent)
            {
                _parent = transform;
            }

            GameObject _inst = Instantiate(swapIndicatorPrefab, _parent);
            if (null != _inst)
            {
                sharedSwapIndicator = _inst.GetComponent<UI_SwapIndicator>();
                if (null != sharedSwapIndicator)
                {
                    sharedSwapIndicator.Initialize(_inputManager);
                    sharedSwapIndicator.transform.SetAsLastSibling();
                    sharedSwapIndicator.HideImmediate();
                }
            }
        }
        else if (null != sharedSwapIndicator)
        {
            sharedSwapIndicator.Initialize(_inputManager);
            sharedSwapIndicator.transform.SetAsLastSibling();
            sharedSwapIndicator.HideImmediate();
        }
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

    // //원목 교체(교체 시스템)
    //
    // 이동식 운반 상자(ui_CarStorage)에만 내려온다. 마을의 나무 보관함은 교체 대상이 아니라
    // 이 값들이 계속 None이다. 표시는 하지 않고 데이터만 들고 있으니, 실제 연출/강조는 아래 이벤트를
    // 구독해서 값을 읽어 그리면 된다. 자세한 사용법은 Docs/LogSwapUI.md 참고.

    /// <summary>
    /// 교체 대상 슬롯 정보가 달라졌을 때 발생합니다(생김 / 사라짐 / 다른 슬롯으로 이동 / 개수 변화).
    /// LogSwapInfo와 ActiveLogSwapTarget을 다시 읽어 표시를 맞추세요.
    /// </summary>
    public event Action LogSwapInfoChangedEvent;

    /// <summary>
    /// 교체가 실제로 일어나 상자 슬롯 하나가 비워졌을 때 발생합니다. 인자는 방금 버려진 슬롯의
    /// 내용(slotIndex / treeType / logState / count)입니다 - 데이터는 이미 지워진 뒤입니다.
    /// </summary>
    public event Action<LogSwapSlotInfo> LogSwapExecutedEvent;

    private LogSwapSlotInfo logSwapInfo = LogSwapSlotInfo.None;
    private ELogSwapTarget activeLogSwapTarget = ELogSwapTarget.None;

    /// <summary>
    /// 지금 교체하면 버려질 상자 슬롯입니다. bHasSlot이 false면 교체 대상이 없습니다.
    /// slotIndex는 storage.inventorySlots(= 이 UI가 그리는 슬롯 목록)와 같은 인덱스입니다.
    /// </summary>
    public LogSwapSlotInfo LogSwapInfo => logSwapInfo;

    /// <summary>교체 키를 누르면 실제로 버려지는 쪽입니다.</summary>
    public ELogSwapTarget ActiveLogSwapTarget => activeLogSwapTarget;

    /// <summary>교체 키가 이 상자의 슬롯을 버리게 되는 상태인지입니다(안내를 켜는 기본 조건).</summary>
    public bool IsLogSwapReady => logSwapInfo.bHasSlot && ELogSwapTarget.OffroadContainer == activeLogSwapTarget;

    /// <summary>
    /// 교체 대상 슬롯 정보를 갱신합니다(UIView_WorldPopup이 호출). 내용이 실제로 달라졌을 때만
    /// LogSwapInfoChangedEvent를 발생시킵니다.
    /// </summary>
    public void SetLogSwapInfo(in LogSwapSlotInfo _info, ELogSwapTarget _activeTarget)
    {
        bool _bChanged = false == LogSwapSlotInfo.IsSame(in _info, in logSwapInfo)
            || _activeTarget != activeLogSwapTarget;

        logSwapInfo = _info;
        activeLogSwapTarget = _activeTarget;

        if (true == _bChanged)
            LogSwapInfoChangedEvent?.Invoke();
    }

    /// <summary>교체로 슬롯 하나가 버려졌습니다(UIView_WorldPopup이 호출).</summary>
    public void LogSwapExecuted(in LogSwapSlotInfo _info)
    {
        LogSwapExecutedEvent?.Invoke(_info);
    }

    private void HandleLogSwapInfoChanged()
    {
        // IsOpening은 퇴장 모션이 끝나야 false가 되므로, 닫히는 도중에 도착한 갱신이 인디케이터를
        // 다시 띄우는 일이 있었다(상자 제안은 사정권 안에서 0.25초마다 갱신된다). isOnShow는 OnHide에서
        // 즉시 내려가므로 "지금 그려도 되는 창인지"는 이쪽을 봐야 한다.
        if (false == IsOpening || false == isOnShow || false == IsLogSwapReady)
        {
            ClearAllSwapIndicators(false);
            return;
        }

        if (true == isOpenAnimated)
            return;

        int _slotIndex = logSwapInfo.slotIndex;
        if (0 <= _slotIndex && storageSlots.Count > _slotIndex)
        {
            SetSwapCandidateSlot(_slotIndex, true);
        }
        else
        {
            ClearAllSwapIndicators(false);
        }
    }

    private void HandleLogSwapExecuted(LogSwapSlotInfo _info)
    {
        HandleLogSwapInfoChanged();
    }

    public void UpdateMaxSlotCount(int _cnt)
    {
        if (null == uiSlotPrefab)
            return;

        int _needCount = _cnt - storageSlots.Count;

        while (0 < _needCount--)
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

        int _itemCount = storage.currentSlotCnt;

        // 슬롯 뷰는 늘어나기만 하므로(UpdateMaxSlotCount), 더 작은 보관함으로 다시 바인드되면
        // 뷰가 데이터보다 많아진다. 범위를 넘는 칸은 빈 칸으로 그려 예외를 막되, 조용히 넘어가면
        // 나중에 원인을 찾기 어려우므로 흔적은 남긴다. (슬롯마다 찍으면 도배되니 루프 밖에서 한 번)
        if (storageSlots.Count > _items.Count)
        {
            Debug.LogWarning($"[UI_Storage] 슬롯 뷰({storageSlots.Count})가 데이터({_items.Count})보다 많습니다. 범위를 넘는 칸은 빈 칸으로 그립니다.");
        }

        for (int _i = 0; _i < storageSlots.Count; ++_i)
        {
            UI_InventorySlot slot = storageSlots[_i];
            if (null == slot)
                continue;

            IInventorySlot item = _items.Count > _i ? _items[_i] : null;

            slot.gameObject.SetActive(_i < _itemCount);
            slot.UpdateBindSlotData(item, storage.maxItemCntPerSlot);
        }

        if (true == IsOpening)
        {
            HandleLogSwapInfoChanged();
        }
    }

    /// <summary>
    /// 특정 저장소 슬롯의 스마트 스왑 아웃라인을 켜고, 공유 인디케이터를 대상 슬롯 위치로 이동시켜 활성화합니다.
    /// </summary>
    public void SetSwapCandidateSlot(int _slotIndex, bool _active, bool _immediate = false)
    {
        if (null == storageSlots || 0 > _slotIndex || storageSlots.Count <= _slotIndex)
            return;

        UI_InventorySlot _slot = storageSlots[_slotIndex];
        if (null == _slot)
            return;

        // 꺼져 있는 슬롯은 레이아웃이 잡히지 않아 좌표가 낡아 있다. 그 위에 인디케이터를 붙이면
        // 허공을 가리키게 되므로, 켜 달라는 요청이어도 받지 않고 전부 끈다. 지금 배선으로는
        // 여기에 걸릴 일이 없으니(SelectVictim이 currentSlotCnt 안에서만 고른다), 걸렸다면
        // 교체 판정과 슬롯 뷰가 어긋났다는 뜻이라 흔적을 남긴다.
        if (true == _active && false == _slot.gameObject.activeInHierarchy)
        {
            Debug.LogWarning($"[UI_Storage] 교체 대상 슬롯({_slotIndex})이 꺼져 있어 인디케이터를 띄우지 않습니다. 교체 판정과 슬롯 뷰가 어긋났습니다.");
            ClearAllSwapIndicators(_immediate);
            return;
        }

        if (true == _active)
        {
            // 이전에 켜져 있던 슬롯이 있다면 아웃라인 해제
            if (-1 != currentSwapSlotIndex && _slotIndex != currentSwapSlotIndex && storageSlots.Count > currentSwapSlotIndex)
            {
                UI_InventorySlot _prevSlot = storageSlots[currentSwapSlotIndex];
                if (null != _prevSlot)
                {
                    _prevSlot.SetSwapIndicator(false, _immediate);
                }
            }

            bool _isSameSlot = (currentSwapSlotIndex == _slotIndex);
            currentSwapSlotIndex = _slotIndex;
            _slot.SetSwapIndicator(true, _immediate);

            if (null != sharedSwapIndicator)
            {
                Vector3 _targetWorldPos = _slot.transform.TransformPoint(indicatorOffset);
                if (true == _isSameSlot && true == sharedSwapIndicator.IsActiveAndShowing)
                {
                    // 같은 슬롯을 계속 가리키는 중이면 위치만 맞춘다. 여기서 SetAsLastSibling까지
                    // 부르면 제안이 갱신될 때마다 캔버스 계층이 더럽혀져 매번 리빌드가 걸린다.
                    sharedSwapIndicator.UpdateTargetPosition(_targetWorldPos);
                }
                else
                {
                    sharedSwapIndicator.transform.SetAsLastSibling();
                    sharedSwapIndicator.Show(_targetWorldPos);
                }
            }
        }
        else
        {
            if (_slotIndex == currentSwapSlotIndex)
            {
                currentSwapSlotIndex = -1;
            }

            _slot.SetSwapIndicator(false, _immediate);

            if (null != sharedSwapIndicator)
            {
                if (true == _immediate)
                {
                    sharedSwapIndicator.HideImmediate();
                }
                else
                {
                    sharedSwapIndicator.Hide();
                }
            }
        }
    }

    /// <summary>
    /// 모든 저장소 슬롯의 스마트 스왑 아웃라인 및 공유 인디케이터를 해제합니다.
    /// </summary>
    public void ClearAllSwapIndicators(bool _immediate = false)
    {
        currentSwapSlotIndex = -1;

        if (null == storageSlots)
            return;

        int _count = storageSlots.Count;
        for (int _i = 0; _i < _count; ++_i)
        {
            UI_InventorySlot _slot = storageSlots[_i];
            if (null != _slot)
            {
                _slot.SetSwapIndicator(false, _immediate);
            }
        }

        if (null != sharedSwapIndicator)
        {
            if (true == _immediate)
            {
                sharedSwapIndicator.HideImmediate();
            }
            else
            {
                sharedSwapIndicator.Hide();
            }
        }
    }

    public void OnShow()
    {
        if (true == isOnShow)
            return;

        gameObject.SetActive(IsOpening = true);
        isPendingHide = false;
        isOnShow = true;
        Sound.PlayUI(SoundID.HUDEverySlotOpen);

        if (null != positioningTween && true == positioningTween.IsActive())
            positioningTween.Kill();

        if (null != rect && null != storage)
        {
            Vector3 _storagePos = storage.GetTransform().position;
            if (null != playerTransform)
                isPlayerOnLeft = (_storagePos.x > playerTransform.position.x);

            rect.position = GetTargetWorldPosition();
        }

        SnapToPerfectPixel();

        if (null != omp)
        {
            isOpenAnimated = true;
            omp.SettingEntryMotion(popdown, true, true);
            popup = omp.Play(popupTag, _onComplete: OnShowCompletedAnimation, bReset: true);
        }
        else
        {
            OnShowCompletedAnimation();
        }
    }

    private void OnShowCompletedAnimation()
    {
        isOpenAnimated = false;
        Canvas.ForceUpdateCanvases();
        HandleLogSwapInfoChanged();
    }

    public void OnHide()
    {
        isOpenAnimated = false;
        ClearAllSwapIndicators(false);
        isOnShow = false;

        if (true == useDynamicPositioning && null != positioningTween && true == positioningTween.IsActive() && true == positioningTween.IsPlaying())
        {
            isPendingHide = true;
            return;
        }

        StartHideMotion();
    }

    // //내부 로직

    private void OnCompleteAnim()
    {
        ClearAllSwapIndicators(true);
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

            bool _currentLeft = 0f > _deltaX;

            if (isPlayerOnLeft != _currentLeft)
            {
                isPlayerOnLeft = _currentLeft;
                TriggerPositioningTween();
            }
        }
    }

    private void OnDestroy()
    {
        LogSwapInfoChangedEvent -= HandleLogSwapInfoChanged;
        LogSwapExecutedEvent -= HandleLogSwapExecuted;
        ClearAllSwapIndicators(true);

        if (null != positioningTween && true == positioningTween.IsActive())
            positioningTween.Kill();

        storageSlots?.Clear();
    }
}
