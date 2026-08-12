using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전리품 획득 시 화면 전체를 덮으며 등장하는 시네마틱 스타일의 정보 표시 UI입니다.
/// 연출은 4단계로 구성됩니다:
///   1단계 - 프로시저럴 그리드 셀들이 중앙에서 좌우로 순차 점등
///   2단계 - 마스킹된 기둥 이미지가 부들부들 떨리며 빠르게 상승
///   3단계 - VFX 이펙트 + 전리품 이미지 뽀잉 등장
///   4단계 - 전리품 이름 → 설명 텍스트 마스킹 슬라이드 등장
/// </summary>
public class HUD_LootReveal : MonoBehaviour
{
    // ─── References ──────────────────────────────────────────────────────────
    [Header("── References ──────────────────────────────────────────────────")]
    [SerializeField] private CanvasGroup rootCanvasGroup;
    [SerializeField] private RectTransform bgRoot;          // 그리드 셀 생성 부모

    [Header("── DataBase ────────────────────────────────────────────────────")]
    [SerializeField] private LootItemTypeDataBase lootDataBase;

    // ─── Tetris BG (Procedural Grid) ─────────────────────────────────────────
    [Header("── Tetris BG (Procedural Grid) ─────────────────────────────────")]
    [SerializeField] private Sprite cellSprite;             // null이면 단색 처리
    [SerializeField] private Color cellColor = new Color(0.1f, 0.1f, 0.15f, 1f);
    [SerializeField] private int gridColumns = 10;
    [SerializeField] private int gridRows = 6;
    [SerializeField] private float cellGap = 3f;
    [SerializeField] private float pieceAppearInterval = 0.04f;   // 열 1개 등장 간격
    [SerializeField] private float pieceRevealDuration = 0.12f;   // 셀 페이드인 시간
    [SerializeField] private Ease pieceRevealEase = Ease.OutQuad;

    // ─── Pillar ───────────────────────────────────────────────────────────────
    [Header("── Pillar ───────────────────────────────────────────────────────")]
    [SerializeField] private RectTransform pillarMaskRoot;  // Mask 컴포넌트 부착 루트
    [SerializeField] private RectTransform pillarRect;
    [SerializeField] private float pillarStartY = -300f;    // 시작 Y (마스크 하단 밖)
    [SerializeField] private float pillarTargetY = 0f;      // 최종 Y
    [SerializeField] private float pillarMoveDuration = 0.35f;
    [SerializeField] private float pillarShakeStrength = 4f;
    [SerializeField] private float pillarShakeDuration = 0.25f;
    [SerializeField] private Ease pillarMoveEase = Ease.OutCubic;

    // ─── Loot Item ────────────────────────────────────────────────────────────
    [Header("── Loot Item ────────────────────────────────────────────────────")]
    [SerializeField] private RectTransform lootItemRect;
    [SerializeField] private Image lootItemImage;
    [SerializeField] private float lootPopDuration = 0.25f;
    [SerializeField] private Vector3 lootPopOvershoot = new Vector3(1.3f, 1.3f, 1f);
    [SerializeField] private Ease lootPopEase = Ease.OutBack;

    // ─── VFX ──────────────────────────────────────────────────────────────────
    [Header("── VFX ──────────────────────────────────────────────────────────")]
    [SerializeField] private VFXComponent vfxComponent;
    [SerializeField] private string vfxTag;
    [SerializeField] private Transform effectSpawnPoint;
    [SerializeField] private UI_ItemAuraEffectController auraEffect;

    // ─── Text ─────────────────────────────────────────────────────────────────
    [Header("── Text ─────────────────────────────────────────────────────────")]
    [SerializeField] private RectTransform nameMaskRoot;
    [SerializeField] private TMP_Text lootNameText;
    [SerializeField] private float nameSlideFromX = -250f;
    [SerializeField] private float nameSlideDuration = 0.3f;
    [SerializeField] private Ease nameSlideEase = Ease.OutCubic;
    [SerializeField] private RectTransform descMaskRoot;
    [SerializeField] private TMP_Text lootDescText;
    [SerializeField] private float descSlideDelay = 0.1f;
    [SerializeField] private float descSlideFromX = -250f;
    [SerializeField] private float descSlideDuration = 0.3f;
    [SerializeField] private Ease descSlideEase = Ease.OutCubic;

    // ─── Timing ───────────────────────────────────────────────────────────────
    [Header("── Timing ───────────────────────────────────────────────────────")]
    [SerializeField] private float pillarStartDelay = 0.1f; // BG 조립 후 기둥 딜레이
    [SerializeField] private float lootStartDelay = 0.1f;   // 기둥 안착 후 아이템 딜레이
    [SerializeField] private float textStartDelay = 0.05f;  // 아이템 등장 후 텍스트 딜레이

    // ─── UIView_ScreenModal 연동 준비 ─────────────────────────────────────────
    // UIView_ScreenModal 구현 완료 후 아래 이벤트를 외부에서 구독하여 활용하세요.
    public event Action OnRevealCompleted;
    public event Action OnHideCompleted;

    // ─── 런타임 상태 ──────────────────────────────────────────────────────────
    private readonly List<Image> gridCells = new List<Image>();
    private Coroutine revealCoroutine;
    private bool isShowing = false;

    // ─── 초기화 ───────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Show() 함수에 의해 정식으로 호출되어 켜진 경우가 아닐 때만 강제로 끕니다.
        // (씬 시작 시 에디터에 켜진 채로 저장되어 자동 켜짐 방지용)
        if (false == isShowing)
        {
            gameObject.SetActive(false);
        }
    }

    // ─── 퍼블릭 진입점 ────────────────────────────────────────────────────────

    /// <summary>
    /// 외부(UIView_ScreenModal 등)에서 전리품 타입과 함께 호출하는 진입점입니다.
    /// </summary>
    public void Show(LootType _lootType, LocalizationManager _locManager)
    {
        if (true == isShowing)
            return;

        isShowing = true;
        gameObject.SetActive(true);

        SetLocalizedTexts(_lootType, _locManager);
        SetLootSprite(_lootType, lootDataBase);
        ResetState();

        if (null != revealCoroutine)
            StopCoroutine(revealCoroutine);

        revealCoroutine = StartCoroutine(PlayRevealSequence());
    }

    /// <summary>
    /// 닫힘 진입점. 키 입력 처리는 상단에서 담당하므로 이 메서드만 노출합니다.
    /// </summary>
    public void Hide()
    {
        if (false == isShowing)
            return;

        if (null != revealCoroutine)
        {
            StopCoroutine(revealCoroutine);
            revealCoroutine = null;
        }

        if (null != rootCanvasGroup)
        {
            rootCanvasGroup.DOKill();
            rootCanvasGroup.DOFade(0f, 0.25f).OnComplete(() =>
            {
                isShowing = false;
                gameObject.SetActive(false);
                OnHideCompleted?.Invoke();
            });
        }
        else
        {
            isShowing = false;
            gameObject.SetActive(false);
            OnHideCompleted?.Invoke();
        }
    }

    // ─── 초기화 ───────────────────────────────────────────────────────────────

    private void ResetState()
    {
        if (null != rootCanvasGroup)
            rootCanvasGroup.alpha = 0f;

        // 기둥 초기 위치
        if (null != pillarRect)
            pillarRect.anchoredPosition = new Vector2(pillarRect.anchoredPosition.x, pillarStartY);

        // 전리품 아이콘 초기 스케일
        if (null != lootItemRect)
        {
            lootItemRect.localScale = Vector3.zero;
            lootItemRect.gameObject.SetActive(false);
        }

        // 텍스트 초기 위치 (마스킹 영역 왼쪽 밖)
        if (null != lootNameText)
        {
            lootNameText.rectTransform.anchoredPosition =
                new Vector2(nameSlideFromX, lootNameText.rectTransform.anchoredPosition.y);
            lootNameText.ForceMeshUpdate(true);
        }
        if (null != lootDescText)
        {
            lootDescText.rectTransform.anchoredPosition =
                new Vector2(descSlideFromX, lootDescText.rectTransform.anchoredPosition.y);
            lootDescText.ForceMeshUpdate(true);
        }

        // 셀 초기화
        foreach (Image _cell in gridCells)
        {
            if (null != _cell)
                _cell.color = new Color(cellColor.r, cellColor.g, cellColor.b, 0f);
        }
    }

    // ─── 그리드 생성 ──────────────────────────────────────────────────────────

    private void BuildGrid()
    {
        // 기존 셀 제거
        foreach (Image _cell in gridCells)
        {
            if (null != _cell)
                Destroy(_cell.gameObject);
        }
        gridCells.Clear();

        if (null == bgRoot)
            return;

        Vector2 _bgSize = bgRoot.rect.size;
        float _cellW = (_bgSize.x - cellGap * (gridColumns - 1)) / gridColumns;
        float _cellH = (_bgSize.y - cellGap * (gridRows - 1)) / gridRows;

        for (int _row = 0; _row < gridRows; _row++)
        {
            for (int _col = 0; _col < gridColumns; _col++)
            {
                GameObject _cellGO = new GameObject($"Cell_{_col}_{_row}", typeof(RectTransform), typeof(Image));
                _cellGO.transform.SetParent(bgRoot, false);

                RectTransform _rt = _cellGO.GetComponent<RectTransform>();
                _rt.anchorMin = Vector2.zero;
                _rt.anchorMax = Vector2.zero;
                _rt.pivot = Vector2.zero;
                _rt.sizeDelta = new Vector2(_cellW, _cellH);

                float _x = _col * (_cellW + cellGap);
                float _y = _row * (_cellH + cellGap);
                _rt.anchoredPosition = new Vector2(_x, _y);

                Image _img = _cellGO.GetComponent<Image>();
                _img.sprite = cellSprite;
                _img.color = new Color(cellColor.r, cellColor.g, cellColor.b, 0f);

                gridCells.Add(_img);
            }
        }
    }

    // ─── 연출 시퀀스 ──────────────────────────────────────────────────────────

    private IEnumerator PlayRevealSequence()
    {
        // 루트 페이드인
        if (null != rootCanvasGroup)
        {
            rootCanvasGroup.DOKill();
            rootCanvasGroup.DOFade(1f, 0.2f);
        }

        yield return new WaitForSeconds(0.1f);

        // 1단계: 그리드 셀 중앙→좌우 순차 점등
        BuildGrid();
        yield return StartCoroutine(PlayGridReveal());

        yield return new WaitForSeconds(pillarStartDelay);

        // 2단계: 기둥 상승
        yield return StartCoroutine(PlayPillarReveal());

        yield return new WaitForSeconds(lootStartDelay);

        // 3단계: VFX + 전리품 이미지 뽀잉 등장
        PlayVFX();
        PlayLootPop();
        yield return new WaitForSeconds(lootPopDuration);

        yield return new WaitForSeconds(textStartDelay);

        // 4단계: 이름 → 설명 텍스트 슬라이드
        yield return StartCoroutine(PlayTextSlide());

        OnRevealCompleted?.Invoke();
    }

    // 1단계 ─ 그리드 셀 중앙 열부터 좌우 순차 점등
    private IEnumerator PlayGridReveal()
    {
        if (0 == gridCells.Count)
            yield break;

        // 중앙 열 인덱스 계산 후 좌우 확산 순서로 열 인덱스 정렬
        List<int> _columnOrder = BuildColumnOrder();

        foreach (int _col in _columnOrder)
        {
            for (int _row = 0; _row < gridRows; _row++)
            {
                int _idx = _row * gridColumns + _col;
                if (_idx < 0 || _idx >= gridCells.Count)
                    continue;

                Image _cell = gridCells[_idx];
                if (null == _cell)
                    continue;

                _cell.DOKill();
                _cell.DOFade(1f, pieceRevealDuration).SetEase(pieceRevealEase);
            }

            yield return new WaitForSeconds(pieceAppearInterval);
        }

        // 마지막 열 연출이 끝날 때까지 대기
        yield return new WaitForSeconds(pieceRevealDuration);
    }

    // 중앙에서 좌우로 퍼지는 열 등장 순서 계산
    private List<int> BuildColumnOrder()
    {
        List<int> _order = new List<int>(gridColumns);
        int _mid = gridColumns / 2;
        int _left = _mid - 1;
        int _right = (0 == gridColumns % 2) ? _mid : _mid + 1;

        // 중앙 열 먼저
        if (0 == gridColumns % 2)
        {
            _order.Add(_mid - 1);
            _order.Add(_mid);
        }
        else
        {
            _order.Add(_mid);
        }

        // 좌우로 동시 확산
        _left = _mid - (0 == gridColumns % 2 ? 2 : 1);
        _right = _mid + 1;

        while (_left >= 0 || _right < gridColumns)
        {
            if (_left >= 0)
            {
                _order.Add(_left);
                _left--;
            }
            if (_right < gridColumns)
            {
                _order.Add(_right);
                _right++;
            }
        }

        return _order;
    }

    // 2단계 ─ 기둥 부들부들 떨리며 상승
    private IEnumerator PlayPillarReveal()
    {
        if (null == pillarRect)
            yield break;

        pillarRect.anchoredPosition = new Vector2(pillarRect.anchoredPosition.x, pillarStartY);

        Sequence _pillarSeq = DOTween.Sequence();
        _pillarSeq.Append(
            pillarRect.DOAnchorPosY(pillarTargetY, pillarMoveDuration).SetEase(pillarMoveEase)
        );
        _pillarSeq.Append(
            pillarRect.DOShakeAnchorPos(pillarShakeDuration, new Vector2(pillarShakeStrength, 0f), 15, 90f, false, true)
        );

        yield return _pillarSeq.WaitForCompletion();
    }

    // 3단계 ─ VFX 재생
    private void PlayVFX()
    {
        if (null != vfxComponent && false == string.IsNullOrEmpty(vfxTag))
        {
            Vector3 _pos = null != effectSpawnPoint ? effectSpawnPoint.position : transform.position;
            vfxComponent.Play(new VFXPlaySettings(vfxTag, _pos, Quaternion.identity));
        }

        if (null != auraEffect)
            auraEffect.Play();
    }

    // 3단계 ─ 전리품 이미지 뽀잉 등장
    private void PlayLootPop()
    {
        if (null == lootItemRect)
            return;

        lootItemRect.gameObject.SetActive(true);
        lootItemRect.localScale = Vector3.zero;

        Sequence _popSeq = DOTween.Sequence();
        _popSeq.Append(lootItemRect.DOScale(lootPopOvershoot, lootPopDuration * 0.6f).SetEase(lootPopEase));
        _popSeq.Append(lootItemRect.DOScale(Vector3.one, lootPopDuration * 0.4f).SetEase(Ease.OutQuad));
    }

    // 4단계 ─ 이름/설명 텍스트 마스킹 슬라이드
    private IEnumerator PlayTextSlide()
    {
        // 이름 텍스트 슬라이드
        if (null != lootNameText)
        {
            RectTransform _nameRT = lootNameText.rectTransform;
            _nameRT.anchoredPosition = new Vector2(nameSlideFromX, _nameRT.anchoredPosition.y);
            _nameRT.DOAnchorPosX(0f, nameSlideDuration).SetEase(nameSlideEase);
        }

        yield return new WaitForSeconds(descSlideDelay);

        // 설명 텍스트 슬라이드
        if (null != lootDescText)
        {
            RectTransform _descRT = lootDescText.rectTransform;
            _descRT.anchoredPosition = new Vector2(descSlideFromX, _descRT.anchoredPosition.y);
            _descRT.DOAnchorPosX(0f, descSlideDuration).SetEase(descSlideEase);
        }

        yield return new WaitForSeconds(Mathf.Max(nameSlideDuration, descSlideDuration + descSlideDelay));
    }

    // ─── 로컬라이징 ───────────────────────────────────────────────────────────

    private void SetLocalizedTexts(LootType _lootType, LocalizationManager _locManager)
    {
        if (null == _locManager)
            return;

        string _nameKey = GetNameKey(_lootType);
        string _descKey = GetDescKey(_lootType);

        if (null != lootNameText)
        {
            lootNameText.text = _locManager.GetText(_nameKey);
            LayoutRebuilder.ForceRebuildLayoutImmediate(lootNameText.rectTransform);
        }

        if (null != lootDescText)
        {
            lootDescText.text = _locManager.GetText(_descKey);
            LayoutRebuilder.ForceRebuildLayoutImmediate(lootDescText.rectTransform);
        }
    }

    private string GetNameKey(LootType _type)
    {
        switch (_type)
        {
            case LootType.LostAndFoundBox:  return "name_lostandfoundbox";
            case LootType.SporePotion:      return "name_sporepotion";
            case LootType.StarCompass:      return "name_starcompass";
            case LootType.ObsidianCharm:    return "name_obsidiancharm";
            default:                        return string.Empty;
        }
    }

    private string GetDescKey(LootType _type)
    {
        switch (_type)
        {
            case LootType.LostAndFoundBox:  return "foundbox";
            case LootType.SporePotion:      return "sporepotion";
            case LootType.StarCompass:      return "starcompass";
            default:                        return string.Empty;
        }
    }

    // ─── 스프라이트 설정 ──────────────────────────────────────────────────────

    private void SetLootSprite(LootType _lootType, LootItemTypeDataBase _db)
    {
        if (null == lootItemImage || null == _db)
            return;

        LootItemTypeData _data = _db.Get(_lootType);
        if (null != _data)
            lootItemImage.sprite = _data.sprite;
    }

    // ─── 에디터 테스트 ────────────────────────────────────────────────────────
#if UNITY_EDITOR
    [NaughtyAttributes.Button("▶ Test Grid Build")]
    private void TestGridBuild()
    {
        BuildGrid();
    }

    [NaughtyAttributes.Button("▶ Reset State")]
    private void TestReset()
    {
        ResetState();
    }
#endif
}
