using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메인메뉴 로그라이크 하위 메뉴의 오른쪽 최고 기록판(시안 v3). Panel_Book 9-슬라이스 위에 제목 명판(최고 기록)과 세 줄:
///   깃발 · 최고 웨이브(n / 30) / 무한대 · 언리밋 최고(Unlimit-n) / 나무 · 최다 벌목(n). 기록이 없으면 "기록 없음".
/// 이름과 값을 한 줄에 나란히 두지 않는다(언어마다 이름 길이가 크게 달라 겹친다) - 이름 줄 아래에 값 줄을 같은 왼쪽 선으로 둔다.
/// 폭은 18개 언어 중 가장 긴 이름 기준 최소값(SpecWidth)과 현재 언어 폭 중 큰 값이고, 판의 오른쪽 끝을 기준으로 왼쪽으로 늘어난다.
/// 저장 파일은 읽기만 한다(RoguelikeRecordViewBuilder). 나타남 · 사라짐은 오른쪽 화면 밖에서 정수 픽셀로 미끄러지는 연출(unscaled)이다.
/// 사라짐이 끝나면 완료 콜백을 부른다(기록 창은 이 판이 화면 밖으로 빠진 뒤에 열린다 - UI_MainMenu).
/// </summary>
public class UI_RoguelikeMenuRecordPanel : MonoBehaviour
{
    // 18개 언어 중 가장 긴 줄 이름(시안 v3 측정 231px) + 아이콘 · 여백 40.
    private const float SpecWidth = 272f;
    private const float SpecTitlePlateWidth = 216f;
    private const int TotalWaves = 30;
    private const float RowGap = 4f;
    // 화면 밖까지: 판 폭 + 오른쪽 여백(16) + 여유.
    private const float SlideMargin = 32f;
    private const float SlideSeconds = 0.22f;
    // 줄 아이콘이 놓이는 칸 한 변(그림은 이 칸 안에 가운데 맞춤).
    private const float IconSlotSize = 9f;

    private const string TitleKey = "Roguelike_Menu_BestTitle";
    private const string NoRecordKey = "Roguelike_Menu_NoRecord";
    private static readonly string[] LabelKeys = { "Roguelike_Menu_BestWave", "Roguelike_Menu_UnlimitBest", "Roguelike_Menu_TreesBest" };

    //외부 의존성 (프리팹에서 연결 - LumberBoy/UI/Build Roguelike Main Menu)
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform panel;
    [SerializeField] private RectTransform titlePlate;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private RectTransform[] icons = new RectTransform[3];
    [SerializeField] private TMP_Text[] labels = new TMP_Text[3];
    [SerializeField] private TMP_Text[] values = new TMP_Text[3];
    [SerializeField] private RectTransform[] separators = new RectTransform[2];
    [SerializeField] private float plateHeight = 29f;
    [SerializeField] private float lineHeight = 18f;

    //내부 의존성
    private LocalizationManager localizationManager;

    //상태 변수
    private Vector2 basePosition;
    private bool bBaseCaptured;
    private TweenCallback deactivateCallback;
    private Action hideCompletedCallback;

    public void Initialize(LocalizationManager _localizationManager)
    {
        localizationManager = _localizationManager;
        deactivateCallback = Deactivate;
    }

    /// <summary>저장 파일을 읽어 세 줄을 채우고 판 크기를 다시 잡는다.</summary>
    public void Refresh()
    {
        RoguelikeRecordData records = RoguelikeRecordRepository.Load();
        string noRecord = Localize(NoRecordKey);

        int bestWave = RoguelikeRecordViewBuilder.GetBestWave(records);
        int bestUnlimit = RoguelikeRecordViewBuilder.GetBestUnlimit(records);
        int mostTrees = RoguelikeRecordViewBuilder.GetMostTrees(records);

        SetText(titleText, Localize(TitleKey));
        for (int i = 0; labels.Length > i && LabelKeys.Length > i; i++) SetText(labels[i], Localize(LabelKeys[i]));

        SetValue(0, 0 < bestWave ? bestWave + " / " + TotalWaves : noRecord);
        SetValue(1, 0 < bestUnlimit ? "Unlimit-" + bestUnlimit : noRecord);
        SetValue(2, 0 < mostTrees ? mostTrees.ToString() : noRecord);

        Layout();
    }

    public void PlayShow()
    {
        PlayShow(SlideSeconds);
    }

    /// <summary>오른쪽 화면 밖에서 _seconds초 동안 미끄러져 들어온다(로그라이크 메뉴 진입은 기록 · 옵션 복귀보다 조금 느리다).</summary>
    public void PlayShow(float _seconds)
    {
        CaptureBase();
        gameObject.SetActive(true);

        RectTransform rect = (RectTransform)transform;
        rect.DOKill();
        rect.anchoredPosition = basePosition + new Vector2(GetSlideDistance(), 0f);
        rect.DOAnchorPos(basePosition, _seconds, true).SetEase(Ease.OutCubic).SetUpdate(true);

        if (null != group)
        {
            group.DOKill();
            group.alpha = 0f;
            group.DOFade(1f, _seconds).SetUpdate(true);
        }
    }

    /// <summary>오른쪽 화면 밖으로 빠진 뒤 숨는다. 끝나면(이미 숨어 있으면 바로) _onCompleted를 부른다.</summary>
    public void PlayHide(Action _onCompleted = null)
    {
        hideCompletedCallback = _onCompleted;
        if (false == gameObject.activeSelf)
        {
            InvokeHideCompleted();
            return;
        }
        CaptureBase();

        RectTransform rect = (RectTransform)transform;
        rect.DOKill();
        rect.DOAnchorPos(basePosition + new Vector2(GetSlideDistance(), 0f), SlideSeconds, true).SetEase(Ease.InCubic).SetUpdate(true).OnComplete(deactivateCallback);

        if (null != group)
        {
            group.DOKill();
            group.DOFade(0f, SlideSeconds).SetUpdate(true);
        }
    }

    public void HideImmediate()
    {
        hideCompletedCallback = null;
        CaptureBase();
        ((RectTransform)transform).DOKill();
        if (null != group) group.DOKill();
        Deactivate();
    }

    // // 배치 (판 오른쪽 위 기준, 정수 픽셀)

    private void Layout()
    {
        if (null == panel) return;

        float width = SpecWidth;
        for (int i = 0; labels.Length > i; i++) width = Mathf.Max(width, PreferredWidth(labels[i]) + 40f);
        for (int i = 0; values.Length > i; i++) width = Mathf.Max(width, PreferredWidth(values[i]) + 40f);
        float plateWidth = Mathf.Max(SpecTitlePlateWidth, PreferredWidth(titleText) + 28f);
        width = Mathf.Max(width, plateWidth + 16f);
        width = EvenCeil(width);
        plateWidth = EvenCeil(plateWidth);

        float y = Mathf.Ceil(plateHeight * 0.5f) + 6f;
        for (int i = 0; labels.Length > i; i++)
        {
            PlaceIcon(icons[i], 10f, y + Mathf.Floor((lineHeight - IconSlotSize) * 0.5f));
            PlaceLeft(null != labels[i] ? labels[i].rectTransform : null, 22f, y, width - 30f, lineHeight);
            PlaceLeft(null != values[i] ? values[i].rectTransform : null, 22f, y + lineHeight, width - 30f, lineHeight);
            y += lineHeight * 2f + RowGap;

            if (separators.Length > i && null != separators[i]) PlaceLeft(separators[i], 8f, y - 3f, width - 16f, 1f);
        }

        float height = Mathf.Ceil(y + 4f);
        panel.sizeDelta = new Vector2(width, height);

        if (null != titlePlate)
        {
            titlePlate.anchorMin = new Vector2(0.5f, 1f);
            titlePlate.anchorMax = new Vector2(0.5f, 1f);
            titlePlate.pivot = new Vector2(0.5f, 0.5f);
            titlePlate.sizeDelta = new Vector2(plateWidth, plateHeight);
            titlePlate.anchoredPosition = Vector2.zero;
        }
    }

    /// <summary>
    /// 줄 아이콘을 IconSlotSize 칸 안에 놓는다. 아이콘 스프라이트는 가로 세로가 짝수가 되도록 그림의 오른쪽 열 / 위쪽 행에 투명 1px이 붙어 있어(그림 = 스프라이트 크기 - 1)
    /// 칸은 스프라이트 크기 그대로(늘리지 않는다), 그림을 칸 안에서 가로 · 세로 가운데(내림)에 두고 투명 줄은 그림 오른쪽 · 위로 남긴다. _x는 칸 왼쪽, _slotY는 칸 위쪽(판 위 기준)이다.
    /// </summary>
    private static void PlaceIcon(RectTransform _rect, float _x, float _slotY)
    {
        if (null == _rect) return;

        Image image = _rect.GetComponent<Image>();
        Sprite sprite = null != image ? image.sprite : null;
        if (null == sprite) return;

        Vector2 size = sprite.rect.size;
        float artWidth = size.x - 1f;
        float artHeight = size.y - 1f;
        float left = _x + Mathf.Floor((IconSlotSize - artWidth) * 0.5f);
        float artTop = _slotY + Mathf.Floor((IconSlotSize - artHeight) * 0.5f);

        // 그림 위에 투명 1행이 있어 스프라이트 위 모서리는 그림 위보다 한 줄 위다.
        PlaceLeft(_rect, left, artTop - 1f, size.x, size.y);
    }

    private static void PlaceLeft(RectTransform _rect, float _x, float _y, float _width, float _height)
    {
        if (null == _rect) return;
        _rect.anchorMin = new Vector2(0f, 1f);
        _rect.anchorMax = new Vector2(0f, 1f);
        _rect.pivot = new Vector2(0f, 1f);
        _rect.sizeDelta = new Vector2(Mathf.Round(_width), Mathf.Round(_height));
        _rect.anchoredPosition = new Vector2(Mathf.Round(_x), -Mathf.Round(_y));
    }

    private static float PreferredWidth(TMP_Text _text)
    {
        if (null == _text) return 0f;
        return Mathf.Ceil(_text.GetPreferredValues(_text.text, 10000f, 10000f).x);
    }

    private static float EvenCeil(float _value)
    {
        int v = Mathf.CeilToInt(_value);
        return 0 == v % 2 ? v : v + 1;
    }

    private void SetValue(int _index, string _value)
    {
        if (values.Length > _index) SetText(values[_index], _value);
    }

    private static void SetText(TMP_Text _text, string _value)
    {
        if (null != _text) _text.text = _value;
    }

    private string Localize(string _key)
    {
        return null != localizationManager ? localizationManager.GetText(_key) : string.Empty;
    }

    private void CaptureBase()
    {
        if (true == bBaseCaptured) return;
        basePosition = ((RectTransform)transform).anchoredPosition;
        bBaseCaptured = true;
    }

    private void Deactivate()
    {
        ((RectTransform)transform).anchoredPosition = basePosition;
        gameObject.SetActive(false);
        InvokeHideCompleted();
    }

    private void InvokeHideCompleted()
    {
        Action callback = hideCompletedCallback;
        hideCompletedCallback = null;
        callback?.Invoke();
    }

    private float GetSlideDistance()
    {
        float width = null != panel ? panel.sizeDelta.x : SpecWidth;
        return Mathf.Ceil(width + SlideMargin);
    }

    private void OnDestroy()
    {
        ((RectTransform)transform).DOKill();
        if (null != group) group.DOKill();
    }
}
