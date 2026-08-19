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
    
    [Space(5)]
    [Tooltip("전리품 아이템 부유 거리(위아래 폭)")]
    [SerializeField] private float lootFloatDistance = 5f;
    [Tooltip("전리품 아이템 1회 왕복 소요 시간")]
    [SerializeField] private float lootFloatDuration = 4.0f;

    private float lootInitialY; 
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
    public event Action OnRevealCompleted;
    public event Action OnHideCompleted;

    // ─── 런타임 상태 ──────────────────────────────────────────────────────────
    private Coroutine revealCoroutine;
    private bool isShowing = false;
    private Material runtimeDissolveMat;

    private TweenCallback cachedOnHideFadeComplete;
    private TweenCallback cachedOnGridRevealUpdate;

    private WaitForSeconds cachedWait01f;
    private WaitForSeconds cachedPillarDelay;
    private WaitForSeconds cachedLootDelay;
    private WaitForSeconds cachedTextDelay;
    private WaitForSeconds cachedDescSlideDelay;
    private WaitForSeconds cachedTextSlideEndDelay;

    // ─── 초기화 ───────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (false == isShowing)
        {
            gameObject.SetActive(false);
        }
        InitParticles();

        if (null != lootItemRect)
        {
            lootInitialY = lootItemRect.anchoredPosition.y;
        }

        // UI 첫 오픈 시 셰이더 컴파일 지연 방지를 위한 사전 웜업
        if (null != auraEffect)
        {
            auraEffect.gameObject.SetActive(true);
            auraEffect.Stop();
        }

        if (null != bgDissolveImage && null == runtimeDissolveMat)
        {
            runtimeDissolveMat = new Material(bgDissolveImage.material);
            bgDissolveImage.material = runtimeDissolveMat;
            runtimeDissolveMat.SetFloat("_DissolveAmount", 0f);
        }

        cachedOnHideFadeComplete = OnHideFadeComplete;
        cachedOnGridRevealUpdate = OnGridRevealUpdate;

        cachedWait01f = new WaitForSeconds(0.1f);
        cachedPillarDelay = new WaitForSeconds(pillarStartDelay);
        cachedLootDelay = new WaitForSeconds(lootStartDelay);
        cachedTextDelay = new WaitForSeconds(textStartDelay);
        cachedDescSlideDelay = new WaitForSeconds(descSlideDelay);
        cachedTextSlideEndDelay = new WaitForSeconds(Mathf.Max(nameSlideDuration, descSlideDuration + descSlideDelay));
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

        if (null != pillarRect)
        {
            particlesRoot.SetParent(pillarRect.parent, true);
            particlesRoot.SetAsLastSibling();
        }

        foreach (var _p in childParticles)
        {
            if (null == _p) continue;
            
            float _duration = UnityEngine.Random.Range(particleFloatDurationMin, particleFloatDurationMax);
            float _dist = UnityEngine.Random.Range(particleFloatDistance * 0.5f, particleFloatDistance);
            float _delay = UnityEngine.Random.Range(0f, 1f);
            
            _p.DOAnchorPosY(_p.anchoredPosition.y + _dist, _duration)
              .SetEase(Ease.InOutSine)
              .SetDelay(_delay)
              .SetLoops(-1, LoopType.Yoyo);
        }
    }
    
    private void Update()
    {
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
        if (null != auraEffect)
        {
            auraEffect.Stop();
        }

        if (null != childParticles)
        {
            foreach (var _p in childParticles)
            {
                if (null != _p) _p.DOKill();
            }
        }
        DOTween.Kill("LootFloat");
    }

    public void Show(LootType _lootType, LocalizationManager _locManager)
    {
        // 광클 락 해제: 완전히 켜져있는 상태라도 닫히는 중일 수 있으므로 무조건 초기화하고 다시 연출합니다.
        isShowing = true;
        gameObject.SetActive(true);
        Sound.PlayUI(SoundID.ResultUIOpen);

        if (null != revealCoroutine)
        {
            StopCoroutine(revealCoroutine);
            revealCoroutine = null;
        }

        SetLocalizedTexts(_lootType, _locManager);
        SetLootSprite(_lootType, lootDataBase);
        
        // 렌더 및 트윈 완벽 초기화
        ResetState();

        revealCoroutine = StartCoroutine(PlayRevealSequence());
    }

    public void Hide()
    {
        if (null != auraEffect)
        {
            auraEffect.Stop();
        }

        if (false == isShowing)
        {
            // 이미 닫혀있어도 호출자(UIView_ScreenModal)가 OnHideCompleted를 기다리고 있을 수 있으므로
            // 즉시 완료로 처리해 알려준다.
            OnHideCompleted?.Invoke();
            return;
        }

        Sound.PlayUI(SoundID.ResultUIClose);

        if (null != revealCoroutine)
        {
            StopCoroutine(revealCoroutine);
            revealCoroutine = null;
        }

        if (null != rootCanvasGroup)
        {
            rootCanvasGroup.DOKill();
            rootCanvasGroup.DOFade(0f, 0.25f).OnComplete(cachedOnHideFadeComplete);
        }
        else
        {
            isShowing = false;
            gameObject.SetActive(false);
            OnHideCompleted?.Invoke();
        }
    }

    private void OnHideFadeComplete()
    {
        // 페이드아웃 중 다시 Show가 불려 알파값이 오르는 상황이면 끄지 않음
        if (0.05f >= rootCanvasGroup.alpha) 
        {
            isShowing = false;
            gameObject.SetActive(false);
            OnHideCompleted?.Invoke();
        }
    }

    private void ResetState()
    {
        if (null != auraEffect)
        {
            auraEffect.Stop();
        }

        if (null != rootCanvasGroup)
        {
            rootCanvasGroup.DOKill();
            rootCanvasGroup.alpha = 0f;
        }

        if (null != pillarRect)
        {
            pillarRect.DOKill();
            pillarRect.anchoredPosition = new Vector2(pillarRect.anchoredPosition.x, pillarStartY);
            
            if (null != particlesRoot)
            {
                particlesRoot.anchoredPosition = pillarRect.anchoredPosition;
                particlesVelocity = Vector2.zero;
            }
        }

        if (null != lootItemRect)
        {
            lootItemRect.DOKill(); // 부유 및 스케일 모션 강제 정지
            lootItemRect.localScale = Vector3.zero;
            lootItemRect.anchoredPosition = new Vector2(lootItemRect.anchoredPosition.x, lootInitialY);
            lootItemRect.gameObject.SetActive(false);
        }

        if (null != lootNameText)
        {
            lootNameText.rectTransform.DOKill();
            lootNameText.rectTransform.anchoredPosition =
                new Vector2(nameSlideFromX, lootNameText.rectTransform.anchoredPosition.y);
            lootNameText.ForceMeshUpdate(true);
        }
        if (null != lootDescText)
        {
            lootDescText.rectTransform.DOKill();
            lootDescText.rectTransform.anchoredPosition =
                new Vector2(descSlideFromX, lootDescText.rectTransform.anchoredPosition.y);
            lootDescText.ForceMeshUpdate(true);
        }

        if (null != bgDissolveImage && null != bgDissolveImage.material)
        {
            if (null == runtimeDissolveMat)
            {
                runtimeDissolveMat = new Material(bgDissolveImage.material);
                bgDissolveImage.material = runtimeDissolveMat;
            }

            if (null != runtimeDissolveMat)
                runtimeDissolveMat.DOKill();

            runtimeDissolveMat.SetFloat("_DissolveAmount", 0f);
            runtimeDissolveMat.SetFloat("_GridSize", gridSize);
            
            float _aspectRatio = 1f;
            if (0 < bgDissolveImage.rectTransform.rect.height)
            {
                _aspectRatio = bgDissolveImage.rectTransform.rect.width / bgDissolveImage.rectTransform.rect.height;
            }
            runtimeDissolveMat.SetFloat("_AspectRatio", _aspectRatio);
            
            bgDissolveImage.SetMaterialDirty(); 
            bgDissolveImage.SetVerticesDirty(); 
        }
    }

    private IEnumerator PlayRevealSequence()
    {
        if (null != rootCanvasGroup)
        {
            rootCanvasGroup.DOKill();
            rootCanvasGroup.DOFade(1f, 0.2f);
        }

        yield return cachedWait01f;

        yield return StartCoroutine(PlayGridReveal());

        yield return cachedPillarDelay;

        yield return StartCoroutine(PlayPillarReveal());

        yield return cachedLootDelay;

        PlayVFX();
        yield return StartCoroutine(PlayLootPop());

        yield return cachedTextDelay;

        yield return StartCoroutine(PlayTextSlide());

        OnRevealCompleted?.Invoke();
    }

    private IEnumerator PlayGridReveal()
    {
        if (null == bgDissolveImage || null == runtimeDissolveMat)
            yield break;

        runtimeDissolveMat.DOKill();
        runtimeDissolveMat.SetFloat("_DissolveAmount", 0f);
        
        yield return runtimeDissolveMat.DOFloat(1f, "_DissolveAmount", bgRevealDuration)
            .SetEase(bgRevealEase)
            .SetUpdate(true) 
            .OnUpdate(cachedOnGridRevealUpdate)
            .WaitForCompletion();
    }

    private void OnGridRevealUpdate()
    {
        bgDissolveImage.SetMaterialDirty();
        bgDissolveImage.SetVerticesDirty();
    }

    private IEnumerator PlayPillarReveal()
    {
        if (null == pillarRect)
            yield break;

        Sound.PlayUI(SoundID.LootUIShelfUp);
        pillarRect.anchoredPosition = new Vector2(pillarRect.anchoredPosition.x, pillarStartY);

        Sequence _pillarSeq = DOTween.Sequence();
        _pillarSeq.Append(
            pillarRect.DOAnchorPosY(pillarTargetY, pillarMoveDuration).SetEase(pillarMoveEase)
        );

        yield return _pillarSeq.WaitForCompletion();
    }

    private void PlayVFX()
    {
        if (null != vfxComponent && false == string.IsNullOrEmpty(vfxTag))
        {
            Vector3 _pos = null != effectSpawnPoint ? effectSpawnPoint.position : transform.position;
            Sound.PlayUI(SoundID.LootUIShiny);
            vfxComponent.Play(new VFXPlaySettings(vfxTag, _pos, Quaternion.identity));
        }

        if (null != auraEffect)
            auraEffect.Play();
    }

    // 3단계 ─ 전리품 이미지 뽀잉 등장
    private IEnumerator PlayLootPop()
    {
        if (null == lootItemRect)
            yield break;

        lootItemRect.gameObject.SetActive(true);
        lootItemRect.localScale = Vector3.zero;

        Sequence _popSeq = DOTween.Sequence();
        _popSeq.Append(lootItemRect.DOScale(lootPopOvershoot, lootPopDuration * 0.6f).SetEase(lootPopEase));
        _popSeq.Append(lootItemRect.DOScale(Vector3.one, lootPopDuration * 0.4f).SetEase(Ease.OutQuad));
        
        yield return _popSeq.WaitForCompletion();

        // 등장 스케일 애니메이션이 완전히 끝난 후, 아이템이 파티클처럼 부유(Floating)하도록 모션 연결
        lootItemRect.DOAnchorPosY(lootInitialY + lootFloatDistance, lootFloatDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetId("LootFloat"); 
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

        yield return cachedDescSlideDelay;

        // 설명 텍스트 슬라이드
        if (null != lootDescText)
        {
            RectTransform _descRT = lootDescText.rectTransform;
            _descRT.anchoredPosition = new Vector2(descSlideFromX, _descRT.anchoredPosition.y);
            _descRT.DOAnchorPosX(0f, descSlideDuration).SetEase(descSlideEase);
        }

        yield return cachedTextSlideEndDelay;
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
            lootNameText.ForceMeshUpdate(true);
        }

        if (null != lootDescText)
        {
            lootDescText.text = _locManager.GetText(_descKey);
            lootDescText.ForceMeshUpdate(true);
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
                if (0 < bgDissolveImage.rectTransform.rect.height)
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
