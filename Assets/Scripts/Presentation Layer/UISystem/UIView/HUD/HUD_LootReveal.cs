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

    // ─── Tetris BG (Procedural Grid / Shader) ────────────────────────────────
    [Header("── Tetris BG (Procedural Grid / Shader) ──────────────────────────")]
    [SerializeField] private Image bgDissolveImage;         // 디졸브 셰이더(UI_PixelDissolve)가 적용된 머티리얼을 가진 이미지
    [SerializeField] private int gridSize = 12;             // 세로축(Row) 기준 픽셀 조각 개수 (가로축은 비율에 맞춰 자동 계산)
    [SerializeField] private float bgRevealDuration = 0.8f; // 확산에 걸리는 총 시간
    [SerializeField] private Ease bgRevealEase = Ease.OutQuad;

    // ─── Pillar ───────────────────────────────────────────────────────────────
    [Header("── Pillar ───────────────────────────────────────────────────────")]
    [SerializeField] private RectTransform pillarMaskRoot;  // Mask 컴포넌트 부착 루트
    [SerializeField] private RectTransform pillarRect;
    [SerializeField] private float pillarStartY = -300f;    // 시작 Y (마스크 하단 밖)
    [SerializeField] private float pillarTargetY = 0f;      // 최종 Y
    [SerializeField] private float pillarMoveDuration = 0.35f;
    [SerializeField] private Ease pillarMoveEase = Ease.OutCubic;

    // ─── Particles ────────────────────────────────────────────────────────────
    [Header("── Particles ────────────────────────────────────────────────────")]
    [SerializeField] private RectTransform particlesRoot;
    
    [Tooltip("기둥(Pillar)을 쫓아갈 때의 지연 시간. 값이 클수록 늦게(고무줄처럼) 따라갑니다.")]
    [SerializeField] private float particlesFollowSmoothTime = 0.15f; 
    
    [Space(5)]
    [Tooltip("파티클이 위아래로 움직일 최대 거리(폭). 좁을수록 은은합니다.")]
    [SerializeField] private float particleFloatDistance = 3f;
    
    [Tooltip("파티클 1회 왕복에 걸리는 최소 시간 (매우 느리게 하려면 6 이상)")]
    [SerializeField] private float particleFloatDurationMin = 8.0f;
    
    [Tooltip("파티클 1회 왕복에 걸리는 최대 시간 (매우 느리게 하려면 10 이상)")]
    [SerializeField] private float particleFloatDurationMax = 12.0f;

    private RectTransform[] childParticles;
    private Vector2 particlesVelocity = Vector2.zero;

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
    private Coroutine revealCoroutine;
    private bool isShowing = false;
    private Material runtimeDissolveMat;

    // ─── 초기화 ───────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Show() 함수에 의해 정식으로 호출되어 켜진 경우가 아닐 때만 강제로 끕니다.
        // (씬 시작 시 에디터에 켜진 채로 저장되어 자동 켜짐 방지용)
        if (false == isShowing)
        {
            gameObject.SetActive(false);
        }
        
        InitParticles();
    }

    private void InitParticles()
    {
        if (null == particlesRoot) return;

        int _childCount = particlesRoot.childCount;
        childParticles = new RectTransform[_childCount];
        for (int i = 0; i < _childCount; i++)
        {
            childParticles[i] = particlesRoot.GetChild(i) as RectTransform;
        }

        // 1. 부모 종속 해제 (Pillar에 묶이지 않고 독립적으로 따라가게 함)
        if (null != pillarRect)
        {
            particlesRoot.SetParent(pillarRect.parent, true);
            // 렌더링 순서(Z-Order)를 맞춰 가장 나중에 그려지게 하여(하이어라키 상 가장 아래) 다른 UI에 가려지지 않고 화면 가장 앞에 나타나도록 설정
            particlesRoot.SetAsLastSibling();
        }

        // 2. 파티클 부유(Floating) 애니메이션
        foreach (var _p in childParticles)
        {
            if (null == _p) continue;
            
            float _duration = UnityEngine.Random.Range(particleFloatDurationMin, particleFloatDurationMax);
            float _dist = UnityEngine.Random.Range(particleFloatDistance * 0.5f, particleFloatDistance);
            float _delay = UnityEngine.Random.Range(0f, 1f); // 동일한 타이밍에 움직이지 않게 엇박자
            
            _p.DOAnchorPosY(_p.anchoredPosition.y + _dist, _duration)
              .SetEase(Ease.InOutSine)
              .SetDelay(_delay)
              .SetLoops(-1, LoopType.Yoyo);
        }
    }
    
    private void Update()
    {
        // 3. 살짝 느리게 따라오게 하는 방식 (관성 SmoothDamp 추적)
        if (null != particlesRoot && null != pillarRect)
        {
            particlesRoot.anchoredPosition = Vector2.SmoothDamp(
                particlesRoot.anchoredPosition, 
                pillarRect.anchoredPosition, 
                ref particlesVelocity, 
                particlesFollowSmoothTime
            );
        }
    }
    
    private void OnDestroy()
    {
        if (null != childParticles)
        {
            foreach (var _p in childParticles)
            {
                if (null != _p) _p.DOKill();
            }
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
        {
            pillarRect.anchoredPosition = new Vector2(pillarRect.anchoredPosition.x, pillarStartY);
            
            // 파티클도 기둥을 따라 텔레포트 (멀리서부터 날아오지 않도록 리셋)
            if (null != particlesRoot)
            {
                particlesRoot.anchoredPosition = pillarRect.anchoredPosition;
                particlesVelocity = Vector2.zero;
            }
        }

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

        // 배경 셰이더 프로퍼티 초기화
        if (null != bgDissolveImage && null != bgDissolveImage.material)
        {
            if (null == runtimeDissolveMat)
            {
                runtimeDissolveMat = new Material(bgDissolveImage.material);
                bgDissolveImage.material = runtimeDissolveMat;
            }

            runtimeDissolveMat.SetFloat("_DissolveAmount", 0f);
            runtimeDissolveMat.SetFloat("_GridSize", gridSize);
            
            // 완벽한 정사각형 타일을 유지하기 위해 UI 객체의 실제 종횡비(가로/세로 비율) 계산 후 셰이더 전달
            float _aspectRatio = 1f;
            if (bgDissolveImage.rectTransform.rect.height > 0)
            {
                _aspectRatio = bgDissolveImage.rectTransform.rect.width / bgDissolveImage.rectTransform.rect.height;
            }
            runtimeDissolveMat.SetFloat("_AspectRatio", _aspectRatio);
            
            // [Fix] 사용자가 설정한 원본 알파(투명도) 값을 1.0으로 덮어쓰지 않고 보존하도록 해당 줄 삭제
            
            // UGUI 갱신 강제 (초기화 시점 화면 프리즈 방지)
            bgDissolveImage.SetMaterialDirty(); 
            bgDissolveImage.SetVerticesDirty(); 
        }
    }

    // (기존의 객체 다중 생성 BuildGrid() 삭제됨)

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

        // 1단계: 셰이더 디졸브 애니메이션 재생
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

    // 1단계 ─ 셰이더 프로퍼티 애니메이팅
    private IEnumerator PlayGridReveal()
    {
        if (null == bgDissolveImage || null == runtimeDissolveMat)
            yield break;

        // 중앙에서 좌우로 디졸브(_DissolveAmount: 0 -> 1)
        runtimeDissolveMat.DOKill();
        runtimeDissolveMat.SetFloat("_DissolveAmount", 0f);
        
        yield return runtimeDissolveMat.DOFloat(1f, "_DissolveAmount", bgRevealDuration)
            .SetEase(bgRevealEase)
            .SetUpdate(true) // 타임스케일 0일 때도 정상 동작 보장
            .OnUpdate(() => 
            {
                // UGUI Canvas 최적화 억제 (강제 리빌드)
                // 단순히 머티리얼 값만 바뀌면 화면을 갱신하지 않는 현상을 막기 위함
                bgDissolveImage.SetMaterialDirty();
                bgDissolveImage.SetVerticesDirty();
            })
            .WaitForCompletion();
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
    // (TestGridBuild 버튼 삭제됨)

    [NaughtyAttributes.Button("▶ Reset State")]
    private void TestReset()
    {
        ResetState();
    }
    
    private void OnValidate()
    {
        // Application.isPlaying 제약을 제거하여 에디터 모드에서도 종횡비 연산이 즉시 동작하도록 락 해제
        if (null != bgDissolveImage && null != bgDissolveImage.rectTransform)
        {
            Material _mat = (Application.isPlaying && null != runtimeDissolveMat) ? runtimeDissolveMat : bgDissolveImage.material;
            if (null != _mat)
            {
                _mat.SetFloat("_GridSize", gridSize);
                
                float _aspectRatio = 1f;
                if (bgDissolveImage.rectTransform.rect.height > 0)
                {
                    _aspectRatio = bgDissolveImage.rectTransform.rect.width / bgDissolveImage.rectTransform.rect.height;
                }
                _mat.SetFloat("_AspectRatio", _aspectRatio);
                
                bgDissolveImage.SetMaterialDirty();
            }
        }
    }
#endif
}
