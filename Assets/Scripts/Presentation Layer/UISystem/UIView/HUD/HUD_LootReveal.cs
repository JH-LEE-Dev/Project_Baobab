using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using PresentationLayer.VFX;

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

    [Tooltip("기둥 윗판 중앙 - 전리품 이미지의 원래 중심 기준(UI 단위). 확대 그림의 밑둥(LootGrowthVisualData.spriteBase)을 여기에 맞춘다.")]
    [SerializeField] private Vector2 lootBaseAnchor = new Vector2(0f, -18f);

    private float lootInitialX;
    private float lootInitialY;
    // 지금 그림의 밑둥을 기둥 윗판 중앙에 맞추는 이동량(SetLootSprite에서 정한다)
    private Vector2 lootBaseOffset;

    // 성장 정보 원천(현재 단계, 다음 단계가 되는 프레스티지 레벨). 윗단이 SetGrowthInfoSource로 연결해 준다.
    private Func<LootType, int> growthTierGetter;
    private LootType revealLootType;
    private Func<LootType, int> growthNextLevelGetter;
    private const string GrowthNextKey = "growth_next";
    private const string GrowthMaxKey = "growth_max";
    [SerializeField] private Ease lootPopEase = Ease.OutBack;

    // ─── VFX ──────────────────────────────────────────────────────────────────
    [Header("── VFX ──────────────────────────────────────────────────────────")]
    [SerializeField] private VFXComponent vfxComponent;
    [SerializeField] private string vfxTag;
    [SerializeField] private Transform effectSpawnPoint;
    [SerializeField] private UI_ItemAuraEffectController auraEffect;
    [SerializeField, Tooltip("전리품 빛(등장 발광 + 평상시 오라). 연결돼 있으면 기존 방사형 광선(auraEffect) 대신 재생한다.")]
    private UI_LootRevealGlow revealGlow;

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

    // ─── Growth (다음 성장 시점 안내) ─────────────────────────────────────────
    [Header("── Growth ───────────────────────────────────────────────────────")]
    [Tooltip("설명 아래 한 줄. \"[꽃 아이콘] N 에서 성장\" 또는 \"최대 성장\"을 보여 준다. 성장하지 않는 전리품이거나 정보가 연결되지 않으면 숨긴다.")]
    [SerializeField] private TMP_Text lootGrowthText;
    [Tooltip("단계별 스프라이트(씨앗/꽃/나무). 비워 두면 LootItemTypeDataBase의 기본 스프라이트를 쓴다.")]
    [SerializeField] private LootGrowthVisualData growthVisualData;

    // ─── Key Icon (설명 문구 속 {key:액션명} → 키 아이콘) ─────────────────────
    [Header("── Key Icon ─────────────────────────────────────────────────────")]
    [Tooltip("설명 문구의 {key:Dash} 같은 토큰을 지금 장치·바인딩의 키 아이콘으로 바꿀 때 쓴다. 비우면 \"[키 이름]\" 텍스트로 대신 나온다.")]
    [SerializeField] private KeyIconDatabase keyIconDatabase;

    // 윗단이 SetInputManager로 넣어 준다. 장치 전환·리바인딩 시 설명을 다시 채우려고 이벤트를 구독한다.
    private InputManager inputManager;
    private LocalizationManager revealLocManager;
    private readonly StringBuilder keyIconBuilder = new StringBuilder(256);
    private Action<EInputDeviceType> cachedOnInputDeviceChanged;
    private Action<EGamepadIconSet> cachedOnGamepadIconSetChanged;
    private Action cachedOnKeyBindingsChanged;

    // 단계별 로컬라이즈 키. 인덱스 = 성장 단계 - 1 (1 씨앗 / 2 꽃 / 3 나무). 용암은 꽃 없이 씨앗 → 나무로 자란다.
    private static readonly string[] lostAndFoundBoxNameKeys = { "name_lostandfoundbox_t1", "name_lostandfoundbox_t2", "name_lostandfoundbox_t3" };
    private static readonly string[] sporePotionNameKeys = { "name_sporepotion_t1", "name_sporepotion_t2", "name_sporepotion_t3" };
    private static readonly string[] starCompassNameKeys = { "name_starcompass_t1", "name_starcompass_t2", "name_starcompass_t3" };
    private static readonly string[] obsidianCharmNameKeys = { "name_obsidiancharm_t1", "name_obsidiancharm_t1", "name_obsidiancharm_t3" };
    private static readonly string[] lostAndFoundBoxDescKeys = { "desc_lostandfoundbox_t1", "desc_lostandfoundbox_t2", "desc_lostandfoundbox_t3" };
    private static readonly string[] sporePotionDescKeys = { "desc_sporepotion_t1", "desc_sporepotion_t2", "desc_sporepotion_t3" };
    private static readonly string[] starCompassDescKeys = { "desc_starcompass_t1", "desc_starcompass_t2", "desc_starcompass_t3" };
    private static readonly string[] obsidianCharmDescKeys = { "desc_obsidiancharm_t1", "desc_obsidiancharm_t1", "desc_obsidiancharm_t3" };

    // ─── Timing ───────────────────────────────────────────────────────────────
    [Header("── Timing ───────────────────────────────────────────────────────")]
    [SerializeField] private float pillarStartDelay = 0f;   // BG 조립 후 기둥 딜레이 (0초 시 즉시 실행)
    [SerializeField] private float lootStartDelay = 0.1f;   // 기둥 안착 후 아이템 딜레이
    [SerializeField] private float textStartDelay = 0.05f;  // 아이템 등장 후 텍스트 딜레이

    // ─── UIView_ScreenModal 연동 준비 ─────────────────────────────────────────
    public event Action OnRevealCompleted;
    public event Action OnHideCompleted;

    // ─── 런타임 상태 ──────────────────────────────────────────────────────────
    private Coroutine revealCoroutine;
    private bool isShowing = false;

    // 닫힘 페이드가 진행 중인지. isShowing은 페이드가 끝나야 false가 되므로 그 사이 들어온 Hide()(ESC/E 광클)를
    // 걸러내지 못한다. 걸러내지 않으면 매번 닫힘 SFX가 다시 울리고 페이드 트윈이 처음부터 재시작돼 닫힘이 계속 밀린다.
    private bool isHiding = false;
    private Material runtimeDissolveMat;

    private TweenCallback cachedOnHideFadeComplete;
    private TweenCallback cachedOnGridRevealUpdate;

    private WaitForSeconds cachedHalfGridRevealDelay;
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
            lootInitialX = lootItemRect.anchoredPosition.x;
            lootInitialY = lootItemRect.anchoredPosition.y;
        }

        // UI 첫 오픈 시 셰이더 컴파일 지연 방지를 위한 사전 웜업
        if (null != auraEffect)
        {
            auraEffect.gameObject.SetActive(true);
            auraEffect.Stop();
        }

        if (null != revealGlow)
        {
            revealGlow.Stop();
        }

        if (null != bgDissolveImage && null == runtimeDissolveMat)
        {
            runtimeDissolveMat = new Material(bgDissolveImage.material);
            bgDissolveImage.material = runtimeDissolveMat;
            runtimeDissolveMat.SetFloat("_DissolveAmount", 0f);
        }

        cachedOnHideFadeComplete = OnHideFadeComplete;
        cachedOnGridRevealUpdate = OnGridRevealUpdate;

        if (0f < bgRevealDuration)
        {
            cachedHalfGridRevealDelay = new WaitForSeconds(bgRevealDuration * 0.5f);
        }

        if (0f < pillarStartDelay)
        {
            cachedPillarDelay = new WaitForSeconds(pillarStartDelay);
        }
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

        if (null != revealGlow)
        {
            revealGlow.Stop();
        }

        if (null != childParticles)
        {
            foreach (var _p in childParticles)
            {
                if (null != _p) _p.DOKill();
            }
        }

        UnsubscribeInputEvents();
    }

    /// <summary>
    /// 설명 문구의 키 아이콘을 지금 장치·바인딩에 맞추려고 InputManager를 연결한다. 윗단(UIView_ScreenModal)이 한 번 호출한다.
    /// 연결하지 않으면 키 토큰은 "[액션명]" 텍스트로 나온다.
    /// </summary>
    public void SetInputManager(InputManager _inputManager)
    {
        UnsubscribeInputEvents();

        inputManager = _inputManager;

        if (null == cachedOnInputDeviceChanged)
        {
            cachedOnInputDeviceChanged = OnInputDeviceChanged;
            cachedOnGamepadIconSetChanged = OnGamepadIconSetChanged;
            cachedOnKeyBindingsChanged = RefreshDescText;
        }

        if (null != inputManager && null != inputManager.inputReader)
        {
            inputManager.inputReader.InputDeviceChangedEvent += cachedOnInputDeviceChanged;
            inputManager.inputReader.GamepadIconSetChangedEvent += cachedOnGamepadIconSetChanged;
            inputManager.inputReader.KeyBindingsChangedEvent += cachedOnKeyBindingsChanged;
        }
    }

    private void UnsubscribeInputEvents()
    {
        if (null == inputManager || null == inputManager.inputReader || null == cachedOnInputDeviceChanged)
            return;

        inputManager.inputReader.InputDeviceChangedEvent -= cachedOnInputDeviceChanged;
        inputManager.inputReader.GamepadIconSetChangedEvent -= cachedOnGamepadIconSetChanged;
        inputManager.inputReader.KeyBindingsChangedEvent -= cachedOnKeyBindingsChanged;
    }

    private void OnInputDeviceChanged(EInputDeviceType _device)
    {
        RefreshDescText();
    }

    private void OnGamepadIconSetChanged(EGamepadIconSet _iconSet)
    {
        RefreshDescText();
    }

    // 떠 있는 동안 장치가 바뀌거나 키를 다시 정하면 설명만 다시 채운다(슬라이드 연출은 건드리지 않는다).
    private void RefreshDescText()
    {
        if (false == isShowing || true == isHiding)
            return;

        SetDescText(revealLootType, revealLocManager);
    }

    /// <summary>
    /// 성장 정보 원천을 연결한다. _tierGetter는 현재 단계(1~3, 성장하지 않으면 0), _nextLevelGetter는 다음 단계가 되는
    /// 프레스티지 레벨(이미 최대면 0)을 돌려준다. 연결하지 않으면 성장 줄은 숨겨진다.
    /// </summary>
    public void SetGrowthInfoSource(Func<LootType, int> _tierGetter, Func<LootType, int> _nextLevelGetter)
    {
        growthTierGetter = _tierGetter;
        growthNextLevelGetter = _nextLevelGetter;
    }

    public void Show(LootType _lootType, LocalizationManager _locManager)
    {
        // 광클 락 해제: 완전히 켜져있는 상태라도 닫히는 중일 수 있으므로 무조건 초기화하고 다시 연출합니다.
        isShowing = true;
        isHiding = false;
        gameObject.SetActive(true);
        Sound.PlayUI(SoundID.ResultUIOpen);

        if (null != revealCoroutine)
        {
            StopCoroutine(revealCoroutine);
            revealCoroutine = null;
        }

        revealLootType = _lootType;
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

        if (null != revealGlow)
        {
            revealGlow.Stop();
        }

        if (false == isShowing)
        {
            // 이미 닫혀있어도 호출자(UIView_ScreenModal)가 OnHideCompleted를 기다리고 있을 수 있으므로
            // 즉시 완료로 처리해 알려준다.
            OnHideCompleted?.Invoke();
            return;
        }

        // 이미 닫히는 중이면 진행 중인 페이드를 그대로 둔다. 그 페이드의 완료 콜백이 OnHideCompleted를 보내므로
        // 호출자(UIView_ScreenModal)의 대기 카운트도 정상적으로 풀린다.
        if (true == isHiding)
        {
            return;
        }

        isHiding = true;
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
            isHiding = false;
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
            isHiding = false;
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

        if (null != revealGlow)
        {
            revealGlow.Stop();
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
            lootItemRect.anchoredPosition = new Vector2(lootInitialX + lootBaseOffset.x, lootInitialY + lootBaseOffset.y);
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

        if (null != lootGrowthText)
        {
            lootGrowthText.rectTransform.DOKill();
            lootGrowthText.rectTransform.anchoredPosition =
                new Vector2(descSlideFromX, lootGrowthText.rectTransform.anchoredPosition.y);
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

        StartGridReveal();

        if (null != cachedHalfGridRevealDelay)
        {
            yield return cachedHalfGridRevealDelay;
        }

        if (0f < pillarStartDelay && null != cachedPillarDelay)
        {
            yield return cachedPillarDelay;
        }

        // 기둥 상승과 텍스트 슬라이드를 동시에 시작
        StartCoroutine(PlayTextSlide());
        yield return StartCoroutine(PlayPillarReveal());

        if (0f < lootStartDelay && null != cachedLootDelay)
        {
            yield return cachedLootDelay;
        }

        PlayVFX();
        yield return StartCoroutine(PlayLootPop());

        OnRevealCompleted?.Invoke();
    }

    private void StartGridReveal()
    {
        if (null == bgDissolveImage || null == runtimeDissolveMat)
            return;

        runtimeDissolveMat.DOKill();
        runtimeDissolveMat.SetFloat("_DissolveAmount", 0f);
        
        runtimeDissolveMat.DOFloat(1f, "_DissolveAmount", bgRevealDuration)
            .SetEase(bgRevealEase)
            .SetUpdate(true) 
            .OnUpdate(cachedOnGridRevealUpdate);
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

        if (null != revealGlow)
        {
            bool _bHasColor = TryGetGrowthAuraColor(out Color _auraColor);
            revealGlow.Play(_auraColor, _bHasColor);
        }
        else if (null != auraEffect)
        {
            ApplyGrowthAuraColors();
            auraEffect.Play();
        }
    }

    // 성장 단계(1~3)의 아우라 색(선형 HDR). 단계가 없으면 false(기둥 빛이 기본 금색을 쓴다).
    private bool TryGetGrowthAuraColor(out Color _color)
    {
        _color = default;
        int _tier = null != growthTierGetter ? growthTierGetter(revealLootType) : 0;
        return 0 < _tier && null != growthVisualData && true == growthVisualData.TryGetAuraColor(_tier, out _color);
    }

    // 성장 단계(1~3)가 있으면 그 단계 색으로 상세보기 아우라를 칠하고, 없으면 아우라 기본색으로 되돌린다.
    private void ApplyGrowthAuraColors()
    {
        int _tier = null != growthTierGetter ? growthTierGetter(revealLootType) : 0;

        if (0 < _tier && null != growthVisualData && true == growthVisualData.TryGetAuraColor(_tier, out Color _tierColor))
        {
            Color _core = Color.Lerp(_tierColor, new Color(2f, 2f, 2f, 1f), 0.45f);
            _core *= 1.6f;
            Color _outer = _tierColor * 0.55f;
            _core.a = 1f;
            _outer.a = 1f;
            _tierColor.a = 1f;
            auraEffect.SetColors(_core, _tierColor, _outer);
        }
        else
        {
            auraEffect.ResetColors();
        }
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
        
        // 등장 뒤에는 떠다니지 않고 기둥 위에 그대로 서 있는다(밑둥이 윗판 중앙에 고정).
        yield return _popSeq.WaitForCompletion();
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

        // 성장 줄은 설명 슬라이드가 끝나갈 즈음 뒤따라 들어온다
        if (null != lootGrowthText && true == lootGrowthText.gameObject.activeSelf)
        {
            RectTransform _growthRT = lootGrowthText.rectTransform;
            _growthRT.anchoredPosition = new Vector2(descSlideFromX, _growthRT.anchoredPosition.y);
            _growthRT.DOAnchorPosX(0f, descSlideDuration).SetEase(descSlideEase).SetDelay(descSlideDelay);
        }

        yield return cachedTextSlideEndDelay;
    }

    // ─── 로컬라이징 ───────────────────────────────────────────────────────────

    private void SetLocalizedTexts(LootType _lootType, LocalizationManager _locManager)
    {
        revealLocManager = _locManager;

        if (null == _locManager)
            return;

        string _nameKey = GetNameKey(_lootType, GetRevealTier(_lootType));

        if (null != lootNameText)
        {
            lootNameText.text = _locManager.GetText(_nameKey);
            lootNameText.ForceMeshUpdate(true);
        }

        SetDescText(_lootType, _locManager);
        SetGrowthText(_lootType, _locManager);
    }

    // 설명에 든 {key:액션명} 토큰은 지금 장치·바인딩의 키 아이콘으로 바꿔 넣는다.
    private void SetDescText(LootType _lootType, LocalizationManager _locManager)
    {
        if (null == lootDescText || null == _locManager)
            return;

        string _desc = _locManager.GetText(GetDescKey(_lootType, GetRevealTier(_lootType)));
        lootDescText.text = TMPKeyIconFormatter.Format(_desc, inputManager, keyIconDatabase, keyIconBuilder);
        lootDescText.ForceMeshUpdate(true);
    }

    // 이름·설명을 고를 성장 단계. 아직 얻지 않았거나(0) 정보가 연결되지 않았으면 첫 단계(씨앗) 문구를 쓴다.
    private int GetRevealTier(LootType _lootType)
    {
        int _tier = null != growthTierGetter ? growthTierGetter(_lootType) : 0;
        return Mathf.Clamp(_tier, 1, 3);
    }

    // 설명 아래 "[꽃] N 에서 성장" / "최대 성장" 한 줄. 정보가 없으면 줄 자체를 숨긴다.
    private void SetGrowthText(LootType _lootType, LocalizationManager _locManager)
    {
        if (null == lootGrowthText)
            return;

        int _tier = null != growthTierGetter ? growthTierGetter(_lootType) : 0;

        if (_tier <= 0 || null == growthNextLevelGetter)
        {
            lootGrowthText.gameObject.SetActive(false);
            return;
        }

        int _nextLevel = growthNextLevelGetter(_lootType);

        lootGrowthText.gameObject.SetActive(true);
        lootGrowthText.text = _nextLevel <= 0
            ? _locManager.GetText(GrowthMaxKey)
            : string.Format(_locManager.GetText(GrowthNextKey), _nextLevel);
        lootGrowthText.ForceMeshUpdate(true);
    }

    // _tier는 1~3 (GetRevealTier로 보정된 값)
    private string GetNameKey(LootType _type, int _tier)
    {
        switch (_type)
        {
            case LootType.LostAndFoundBox:  return lostAndFoundBoxNameKeys[_tier - 1];
            case LootType.SporePotion:      return sporePotionNameKeys[_tier - 1];
            case LootType.StarCompass:      return starCompassNameKeys[_tier - 1];
            case LootType.ObsidianCharm:    return obsidianCharmNameKeys[_tier - 1];
            default:                        return string.Empty;
        }
    }

    // _tier는 1~3 (GetRevealTier로 보정된 값)
    private string GetDescKey(LootType _type, int _tier)
    {
        switch (_type)
        {
            case LootType.LostAndFoundBox:  return lostAndFoundBoxDescKeys[_tier - 1];
            case LootType.SporePotion:      return sporePotionDescKeys[_tier - 1];
            case LootType.StarCompass:      return starCompassDescKeys[_tier - 1];
            case LootType.ObsidianCharm:    return obsidianCharmDescKeys[_tier - 1];
            default:                        return string.Empty;
        }
    }

    // ─── 스프라이트 설정 ──────────────────────────────────────────────────────

    private void SetLootSprite(LootType _lootType, LootItemTypeDataBase _db)
    {
        lootBaseOffset = Vector2.zero;

        if (null == lootItemImage || null == _db)
            return;

        LootItemTypeData _data = _db.Get(_lootType);
        if (null != _data)
            lootItemImage.sprite = _data.sprite;

        // 성장 단계별 확대 스프라이트가 등록돼 있으면 그것을 쓴다(씨앗/꽃/나무). 없으면 위 기본 스프라이트 그대로.
        // 성장하지 않는 전리품(단계 0)은 그 전리품의 가장 낮은 단계 스프라이트를 쓴다.
        int _tier = null != growthTierGetter ? growthTierGetter(_lootType) : 0;

        if (null != growthVisualData
            && true == growthVisualData.TryGetSpriteEntry(_lootType, _tier, out LootGrowthVisualData.TierSprite _entry))
        {
            Sprite _tierSprite = _entry.sprite;
            lootItemImage.sprite = _tierSprite;

            // 그림마다 밑둥이 다른 곳에 있다 - 밑둥(스프라이트 유닛 → 이미지 픽셀)이 기둥 윗판 중앙에 오도록 옮긴다. 픽셀 단위로 맞춘다.
            if (null != _tierSprite && 0f < _tierSprite.rect.width)
            {
                float _scale = _tierSprite.pixelsPerUnit * lootItemImage.rectTransform.rect.width / _tierSprite.rect.width;
                Vector2 _offset = lootBaseAnchor - _entry.spriteBase * _scale;
                lootBaseOffset = new Vector2(Mathf.Floor(_offset.x + 0.5f), Mathf.Floor(_offset.y + 0.5f));
            }
        }
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
