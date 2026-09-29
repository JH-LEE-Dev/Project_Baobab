using System;
using System.Collections.Generic;
using UnityEngine;
using PresentationLayer.DOTweenAnimationSystem;

public class UI_TreeCutter : MonoBehaviour
{
    // //외부 의존성
    [SerializeField] private GameObject uiSlotPrefab;
    [SerializeField] private GameObject mainVisual;
    [SerializeField] private ObjectMotionPlayer omp;
    [SerializeField] private HUD_ProgressBar progressBar;
    [SerializeField] private Vector3 offset;

    // //내부 의존성
    private const float SLOT_Y_STEP = 27f;
    private const float BASE_TOP_Y = 35f;

    private readonly List<CutterSlotUnit> slotUnits = new List<CutterSlotUnit>(3);
    private ILogCutterProvider cutterProvider;
    private Vector2 initialProgressBarPos;

    // 하위 호환성 프로퍼티
    public UI_InventorySlot Slot
    {
        get
        {
            if (0 < slotUnits.Count && null != slotUnits[0])
                return slotUnits[0].slot;
            return null;
        }
    }

    [SerializeField] private string popupTag = "Popup";
    [SerializeField] private string popdownTag = "Popdown";

    private MotionEntry popup;
    private MotionEntry popdown;
    private RectTransform rect;
    private bool bOpen = false;


    // //퍼블릭 초기화 및 제어 메서드

    public void Initialize(Vector2 _offset)
    {
        offset = _offset;
        rect = GetComponent<RectTransform>();

        if (null != mainVisual)
        {
            RectTransform bgRect = mainVisual.GetComponent<RectTransform>();
            if (null != bgRect)
            {
                // 상단 위치를 고정하고 아래쪽으로 슬롯이 늘어나도록 피봇 설정
                bgRect.pivot = new Vector2(0.5f, 1f);
                bgRect.anchoredPosition = new Vector2(bgRect.anchoredPosition.x, BASE_TOP_Y);
            }
        }

        if (null != progressBar)
        {
            RectTransform pbRect = progressBar.GetComponent<RectTransform>();
            if (null != pbRect)
                initialProgressBarPos = pbRect.anchoredPosition;

            progressBar.Initialize();
            progressBar.SetActivate(false);
        }

        // 기본 0번 라인 슬롯 유닛 구성
        EnsureSlotUnits(1);

        if (null != omp)
            omp.Initialize();

        SnapToPerfectPixel();
        OnHide(true);
    }

    public void BindCutterProvider(ILogCutterProvider _provider)
    {
        cutterProvider = _provider;
        int lineCount = null != cutterProvider ? cutterProvider.ActiveLineCount : 1;
        UpdateLineCount(lineCount);
    }

    public void UpdateLineCount(int _lineCount)
    {
        int targetCount = Mathf.Clamp(_lineCount, 1, 3);
        EnsureSlotUnits(targetCount);

        for (int i = 0; i < slotUnits.Count; i++)
        {
            bool bActive = i < targetCount;
            slotUnits[i].SetActive(bActive);

            if (false == bActive)
            {
                slotUnits[i].ResetData();
                slotUnits[i].UnbindLogCutter();
            }
            else if (null != cutterProvider)
            {
                slotUnits[i].BindLogCutter(cutterProvider.GetCutter(i));
                slotUnits[i].SyncWithCutter();
            }
        }

        SnapToPerfectPixel();
    }

    // 단일 라인 바인딩 (하위 호환성 유지)
    public void BindLogCutter(ILogCutter _logCutter)
    {
        EnsureSlotUnits(1);
        if (0 < slotUnits.Count && null != slotUnits[0])
            slotUnits[0].BindLogCutter(_logCutter);
    }

    public void UnbindLogCutter()
    {
        for (int i = 0; i < slotUnits.Count; i++)
            slotUnits[i]?.UnbindLogCutter();
    }

    public void BindItemData(ILogItemData _itemData)
    {
        EnsureSlotUnits(1);
        if (0 < slotUnits.Count && null != slotUnits[0])
            slotUnits[0].BindItemData(_itemData);

        SnapToPerfectPixel();
    }

    public void SyncWithCutter()
    {
        SyncWithCutters();
    }

    public void SyncWithCutters()
    {
        for (int i = 0; i < slotUnits.Count; i++)
            slotUnits[i]?.SyncWithCutter();

        SnapToPerfectPixel();
    }

    public void Refresh()
    {
        for (int i = 0; i < slotUnits.Count; i++)
            slotUnits[i]?.Refresh();

        SnapToPerfectPixel();
    }

    public void BindPosition(Vector3 _newPos)
    {
        if (null != rect)
            rect.position = _newPos + offset;

        SnapToPerfectPixel();
    }

    public void ResetCutter()
    {
        for (int i = 0; i < slotUnits.Count; i++)
            slotUnits[i]?.ResetData();
    }

    public void OnShow()
    {
        if (true == bOpen)
            return;

        gameObject.SetActive(true);

        if (null == omp)
            return;

        bOpen = true;

        omp.SettingEntryMotion(popdown, true, true);
        popup = omp.Play(popupTag, bReset: true);

        SnapToPerfectPixel();
    }

    public void OnHide(bool _bSkip = false)
    {
        if (null == omp)
            return;

        bOpen = false;

        omp.SettingEntryMotion(popup, true, true);
        popdown = omp.Play(popdownTag, bReset: true, _skip: _bSkip, _onComplete: OnCompletedAnimation);
    }


    // //내부 로직

    private void EnsureSlotUnits(int _count)
    {
        int safeCount = Mathf.Clamp(_count, 1, 3);
        while (slotUnits.Count < safeCount)
        {
            int lineIdx = slotUnits.Count;
            CutterSlotUnit unit = CreateSlotUnit(lineIdx);
            if (null != unit)
                slotUnits.Add(unit);
            else
                break;
        }
    }

    private CutterSlotUnit CreateSlotUnit(int _lineIdx)
    {
        UI_InventorySlot slotInstance = null;
        if (null != uiSlotPrefab && null != mainVisual)
        {
            GameObject slotObj = Instantiate(uiSlotPrefab, mainVisual.transform);
            if (null != slotObj)
            {
                slotInstance = slotObj.GetComponent<UI_InventorySlot>();
                if (null != slotInstance)
                {
                    slotInstance.Initialize();
                    slotInstance.DisableRayCast();
                }
            }
        }

        HUD_ProgressBar progressBarInstance = null;
        if (0 == _lineIdx)
        {
            progressBarInstance = progressBar;
        }
        else if (null != progressBar)
        {
            GameObject pbObj = Instantiate(progressBar.gameObject, progressBar.transform.parent);
            if (null != pbObj)
            {
                progressBarInstance = pbObj.GetComponent<HUD_ProgressBar>();
                RectTransform pbRect = pbObj.GetComponent<RectTransform>();
                if (null != pbRect)
                {
                    pbRect.anchoredPosition = new Vector2(
                        initialProgressBarPos.x,
                        initialProgressBarPos.y - (_lineIdx * SLOT_Y_STEP)
                    );
                }

                if (null != progressBarInstance)
                {
                    progressBarInstance.Initialize();
                    progressBarInstance.SetActivate(false);
                }
            }
        }

        CutterSlotUnit unit = new CutterSlotUnit
        {
            lineIndex = _lineIdx,
            slot = slotInstance,
            progressBar = progressBarInstance
        };

        return unit;
    }

    private void OnCompletedAnimation()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// mainVisual RectTransform의 가로/세로 크기(홀수/짝수)와 피봇(0.5, 0, 1) 설정에 맞추어
    /// UI 렌더링 시 픽셀 경계가 뭉개지지 않고 선명하게 출력(Pixel-perfect)되도록 anchoredPosition을 스냅 정렬합니다.
    /// </summary>
    private void SnapToPerfectPixel()
    {
        if (null == mainVisual)
            return;

        // 캔버스를 즉각 강제 갱신하여 비활성화 ➡️ 활성화 전환 직후 프레임 지연으로 크기(Width/Height)가 0으로 잡히는 버그를 해결합니다.
        Canvas.ForceUpdateCanvases();

        RectTransform _bgRect = mainVisual.GetComponent<RectTransform>();
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
            else if (0.01f > Mathf.Abs(_pivotX - 0f) || 0.01f > Mathf.Abs(_pivotX - 1f))
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
            else if (0.01f > Mathf.Abs(_pivotY - 0f) || 0.01f > Mathf.Abs(_pivotY - 1f))
                _pos.y = Mathf.Round(_pos.y);
        }
        else
        {
            if (0.01f > Mathf.Abs(_pivotY - 0.5f) || 0.01f > Mathf.Abs(_pivotY - 0f) || 0.01f > Mathf.Abs(_pivotY - 1f))
                _pos.y = Mathf.Round(_pos.y);
        }

        rect.anchoredPosition = _pos;
    }


    // //유니티 이벤트 함수

    private void Update()
    {
        if (true == bOpen)
        {
            for (int i = 0; i < slotUnits.Count; i++)
            {
                slotUnits[i]?.UpdateProgress();
            }
        }
    }

    private void OnDestroy()
    {
        UnbindLogCutter();
    }


    // //내부 라인 슬롯 단위 관리 클래스

    private class CutterSlotUnit
    {
        public int lineIndex;
        public UI_InventorySlot slot;
        public HUD_ProgressBar progressBar;
        public ILogCutter logCutter;
        public ILogItemData cachedItemData;

        public void BindLogCutter(ILogCutter _logCutter)
        {
            UnbindLogCutter();

            logCutter = _logCutter;
            if (null == logCutter)
                return;

            logCutter.CuttingStartEvent += OnCuttingStart;
            logCutter.CuttingDoneEvent += OnCuttingDone;
        }

        public void UnbindLogCutter()
        {
            if (null == logCutter)
                return;

            logCutter.CuttingStartEvent -= OnCuttingStart;
            logCutter.CuttingDoneEvent -= OnCuttingDone;
            logCutter = null;
        }

        private void OnCuttingStart(ILogItemData _itemData)
        {
            ILogItemData targetData = (null != _itemData) ? _itemData : (null != logCutter ? logCutter.logToCut : null);
            BindItemData(targetData);
        }

        private void OnCuttingDone()
        {
            ResetData();
        }

        public void BindItemData(ILogItemData _itemData)
        {
            cachedItemData = _itemData;

            if (null != slot)
            {
                if (null != _itemData)
                {
                    slot.UpdateImage(_itemData.sprite, Color.white);
                    slot.UpdateRarityEffect(_itemData);
                }
                else
                {
                    slot.ResetData();
                }
            }

            if (null != progressBar)
                progressBar.SetActivate(null != _itemData);
        }

        public void ResetData()
        {
            cachedItemData = null;

            if (null != slot)
                slot.ResetData();

            if (null != progressBar)
                progressBar.SetActivate(false);
        }

        public void Refresh()
        {
            if (null != slot && null != cachedItemData)
            {
                slot.UpdateImage(cachedItemData.sprite, Color.white);
                slot.UpdateRarityEffect(cachedItemData);
            }
        }

        public void SyncWithCutter()
        {
            if (null != logCutter && null != logCutter.logToCut && true == logCutter.bIsCutting)
                BindItemData(logCutter.logToCut);
            else
                ResetData();
        }

        public void UpdateProgress()
        {
            if (null == progressBar || null == cachedItemData || null == logCutter)
                return;

            if (false == logCutter.bIsCutting)
                return;

            float _total = logCutter.totalProcessingTime;
            float _ratio = (0f < _total) ? Mathf.Clamp01(logCutter.elapsedProcessingTime / _total) : 0f;

            progressBar.UpdateValue(_ratio);
        }

        public void SetActive(bool _isActive)
        {
            if (null != slot)
                slot.gameObject.SetActive(_isActive);

            if (null != progressBar)
            {
                if (false == _isActive)
                    progressBar.SetActivate(false);
                else
                    progressBar.SetActivate(null != cachedItemData);
            }
        }
    }
}