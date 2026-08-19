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
    private ILogItemData cachedItemData;
    private ILogCutter logCutter;

    private UI_InventorySlot slot;
    public UI_InventorySlot Slot { get { return slot; } set { slot = value; } }

    [SerializeField] private string popupTag = "Popup";
    [SerializeField] private string popdownTag = "Popdown";

    private MotionEntry popup;
    private MotionEntry popdown;
    private RectTransform rect;
    private bool bOpen = false;


    // //퍼블릭 초기화 및 제어 메서드

    public void Initialize(Vector2 _offset)
    {
        if (null != uiSlotPrefab)
        {
            slot = Instantiate(uiSlotPrefab, mainVisual.transform).GetComponent<UI_InventorySlot>();

            if (null != slot)
            {
                slot.Initialize();
                slot.DisableRayCast();
            }
        }

        offset = _offset;

        rect = GetComponent<RectTransform>();

        if (null != omp)
            omp.Initialize();

        if (null != progressBar)
        {
            progressBar.Initialize();
            progressBar.SetActivate(false);
        }

        SnapToPerfectPixel();

        OnHide(true);
    }

    public void BindItemData(ILogItemData _itemData)
    {
        cachedItemData = _itemData;

        if (null != slot)
        {
            if (null != _itemData)
                // 원목 스프라이트는 이미 색이 입혀진 그림이라 틴트를 걸지 않는다.
                // (황금/다이아/무지개 원목에 나무 종류 색을 곱하면 색이 죽는다)
                slot.UpdateImage(_itemData.sprite, Color.white);
            else
                slot.ResetData();
        }

        if (null != progressBar)
            progressBar.SetActivate(null != _itemData);

        SnapToPerfectPixel();
    }

    public void Refresh()
    {
        if (null != slot && null != cachedItemData)
            slot.UpdateImage(cachedItemData.sprite, Color.white);

        SnapToPerfectPixel();
    }

    public void BindLogCutter(ILogCutter _logCutter)
    {
        logCutter = _logCutter;
    }

    public void BindPosition(Vector3 _newPos)
    {
        if (null != rect)
            rect.position = _newPos + offset;
    }

    public void ResetCutter()
    {
        cachedItemData = null;

        if (null != slot)
            slot.ResetData();

        if (null != progressBar)
            progressBar.SetActivate(false);
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

        // 캔버스를 즉각 강제 갱신하여 비활성화 ➡️ 활성화 전환 직후 프레임 지연으로 크기(Width/Height)가 0으로 잡히는 버그를 완벽히 해결합니다.
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


    // //유니티 이벤트 함수 (Awake, Start, OnDestroy 등 최하단 배치)

    private void Update()
    {
        if (true == bOpen)
        {
            float _ratio = 0f;
            if (null != logCutter)
            {
                float _total = logCutter.totalProcessingTime;
                if (0f < _total)
                    _ratio = Mathf.Clamp01(logCutter.elapsedProcessingTime / _total);
            }

            if (null != progressBar)
                progressBar.UpdateValue(_ratio);
        }
    }

    private void OnDestroy()
    {
    }
}