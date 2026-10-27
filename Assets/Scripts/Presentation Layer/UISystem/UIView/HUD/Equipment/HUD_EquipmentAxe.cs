using PresentationLayer.DOTweenAnimationSystem;
using System.Collections.Generic;
using Coffee.UIEffects;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace PresentationLayer.UISystem.UIView.HUD.Equipment
{
    public class HUD_EquipmentAxe : HUD_EquipmentItem
    {
        private enum AxeMode { DB100, DB75, DB50, DB25, ZERO }

        [Header("Images")]
        [SerializeField] List<Sprite> axeImages;

        // //외부 의존성
        [Header("Axe Specific UI")]
        [SerializeField] private Image axeImage; // 도끼 이미지
        [SerializeField] private HUD_HPBar axeGaugeBar; // 도끼 특수 게이지 바

        // [Header("Key Image & Localization")]
        // [SerializeField] private UI_KeyboardImage keyboardImage;
        // [SerializeField] private TextMeshProUGUI actionText;
        // [SerializeField] private int localizationJsonId = 12;
        // [SerializeField] private int localizationEntryId = 1;

        [Header("Animations")]
        [SerializeField] private ObjectMotionPlayer omp;
        [SerializeField] private string brokenAnimTag;

        [Header("VFX Settings")]
        [SerializeField] private VFXComponent vfxComponent;
        [SerializeField] private GameObject axeHead;
        [SerializeField] private string axeBrokenTag;
        [SerializeField] private string axeLastBrokenTag;

        [Header("Overheat Aura")]
        [SerializeField] private HUD_AxeOverheatAura overheatAura;

        [Header("Repair Effect")]
        [Tooltip("수리 번쩍임. 도끼 그림 위에 같은 스프라이트로 겹쳐 두고, 색은 repairFlashEffect의 색 치환(Replace)이 하얗게 칠한다.")]
        [SerializeField] private Image repairFlashOverlay;
        [SerializeField] private UIEffect repairFlashEffect;
        [Tooltip("뽀잉으로 크기가 출렁이는 대상(도끼 그림·외곽선·그림자를 묶은 부모)")]
        [SerializeField] private RectTransform repairBounceTarget;
        [SerializeField] private float repairFlashDuration = 0.3f;
        [SerializeField] private float repairBounceScale = 1.2f;
        [SerializeField] private float repairBounceDuration = 0.5f;

        // //내부 의존성
        private AxeMode axeMode = AxeMode.DB100;
        private float previousRatio = 1f;
        private Sequence repairSequence;
        private Vector3 repairBounceBaseScale = Vector3.one;

        // private LocalizationManager localizationManager;
        // private Action cachedRefreshLocalizedText;

        // //퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 초기 설정 및 의존성 구성.
        /// </summary>
        public override void Initialize()
        {
            Initialize(null, null);
        }

        public void Initialize(InputManager _inputManager, LocalizationManager _localizationManager)
        {
            base.Initialize();

            if (null != axeGaugeBar)
                axeGaugeBar.Initialize();

            if (null != vfxComponent)
                vfxComponent.Initialize();

            if (null != omp)
                omp.Initialize();

            if (null != overheatAura)
                overheatAura.Initialize(axeImage, axeImages);

            if (null != repairBounceTarget)
                repairBounceBaseScale = repairBounceTarget.localScale;

            if (null != repairFlashEffect)
            {
                repairFlashEffect.colorFilter = ColorFilter.Replace;
                repairFlashEffect.colorIntensity = 1f;
                repairFlashEffect.color = Color.white;
            }

            SetRepairFlashAlpha(0f);

            /*
            if (null != keyboardImage && null != _inputManager)
            {
                keyboardImage.Initialize(_inputManager);
            }

            localizationManager = _localizationManager;
            if (null != localizationManager)
            {
                if (null == cachedRefreshLocalizedText)
                    cachedRefreshLocalizedText = RefreshLocalizedText;

                localizationManager.OnLanguageChanged -= cachedRefreshLocalizedText;
                localizationManager.OnLanguageChanged += cachedRefreshLocalizedText;

                RefreshLocalizedText();
            }
            */
        }

        /*
        public void RefreshLocalizedText()
        {
            if (null == actionText || null == localizationManager)
                return;

            string _localized = localizationManager.GetText(localizationJsonId, localizationEntryId);
            if (false == string.IsNullOrEmpty(_localized))
            {
                actionText.text = _localized;
            }
        }
        */

        /// <summary>
        /// 캐릭터의 과열 상태가 바뀌었을 때 호출된다. 도끼 HUD 둘레의 과열 아우라를 켜고 끈다.
        /// </summary>
        public void SetOverheat(bool _bActive)
        {
            if (null != overheatAura)
                overheatAura.SetOverheat(_bActive);
        }

        protected override void UpdateVisuals()
        {
            base.UpdateVisuals();
        }

        /// <summary>
        /// 도끼 게이지 값을 업데이트합니다.
        /// </summary>
        /// <param name="_ratio">0~1 사이의 비율</param>
        /// <param name="_bPlayEffects">
        /// true면 진짜 공격/수리로 인한 내구도 변화이므로 사운드·VFX를 재생한다.
        /// false면 도끼 내구도 강화 스킬 등으로 최대 내구도가 바뀌어 비율만 재계산되는
        /// "무음 재동기화"이므로 스프라이트/게이지만 갱신하고 사운드·VFX는 재생하지 않는다.
        /// </param>
        public void UpdateGauge(float _ratio, bool _bPlayEffects = true)
        {
            if (null == axeGaugeBar)
                return;

            axeGaugeBar.UpdateValue(_ratio);
            UpdateAxeImage(_ratio, _bPlayEffects);
        }

        private void UpdateAxeImage(float _ratio, bool _bPlayEffects)
        {
            if (null == axeImages || null == axeImage)
                return;

            AxeMode prevMode = axeMode;

            // 수리(RepairDurability)로 비율이 좋아지는 방향에서는 "깨지는" 소리가 나면 안 되므로 방향을 기록해둔다.
            bool isBreaking = _ratio < previousRatio;
            previousRatio = _ratio;

            if (_ratio > 0.75f)
                axeMode = AxeMode.DB100;
            else if (_ratio > 0.5f)
                axeMode = AxeMode.DB75;
            else if (_ratio > 0.25f)
                axeMode = AxeMode.DB50;
            else if (_ratio > 0.0f)
                axeMode = AxeMode.DB25;
            else
                axeMode = AxeMode.ZERO;

            axeImage.sprite = axeImages[(int)axeMode];

            // 번쩍임 오버레이는 도끼 실루엣을 그대로 따라가야 한다.
            if (null != repairFlashOverlay)
                repairFlashOverlay.sprite = axeImage.sprite;

            // 도끼 모양이 바뀌는 지점은 여기뿐이다(내구도 비율이 0.75 / 0.5 / 0.25 / 0을 지날 때). 과열 아우라는 이 호출에서만 새 실루엣으로 교체된다.
            if (null != overheatAura)
                overheatAura.OnAxeSpriteChanged(axeImage.sprite);

            // 무음 재동기화(스킬로 최대 내구도만 바뀌는 등)에서는 스프라이트/모드/previousRatio만
            // 갱신하고, 실제 파손 사운드·VFX·애니메이션은 재생하지 않는다.
            if (false == _bPlayEffects)
                return;

            // 수리로 좋아지는 방향에서는 깨짐 흔들림·파편 대신 PlayRepairEffect(흰 번쩍임 + 뽀잉)가 나간다.
            if (true == isBreaking && 1f > _ratio && prevMode != axeMode)
            {
                // 수리 직후 곧바로 깨지면 깨짐 연출이 우선이다. 하얀 실루엣은 MainImage의 흔들림을 따라가지 않으므로
                // 남아 있으면 둘이 어긋나 보인다 - 번쩍임·뽀잉을 여기서 바로 끝낸다(OnKill이 원래 모습으로 되돌린다).
                KillRepairSequence();

                if (null != omp)
                {
                    omp.Play(brokenAnimTag, bReset: true);
                }

                // AxeBreaking(깨지는 소리)은 파티클 발생 여부와 무관하게 스프라이트가 바뀔 때마다 재생하되,
                // 수리로 좋아지는 방향에서는 재생하지 않는다.
                if (true == isBreaking)
                {
                    Sound.PlayUI(SoundID.AxeBreaking);
                }

                if (null != vfxComponent && null != axeHead && 0.75f > _ratio)
                {
                    ParticleSystem brokenEffect = vfxComponent.Play(axeBrokenTag, axeHead.transform.position, Quaternion.identity, transform);

                    // AxeBreaking_ex(파티클이 바닥에 흩뿌려지는 소리)는 실제로 나빠지는 방향이면서 파티클이 나갔을 때만 딜레이 재생한다.
                    // HUD가 비활성 상태(예: 어빌리티 트리 UI가 열려있는 동안)면 코루틴을 시작할 수 없으므로 건너뛴다.
                    if (true == isBreaking && null != brokenEffect && true == gameObject.activeInHierarchy)
                    {
                        StartCoroutine(PlayAxeBreakingExDelayed());
                    }

                    if (AxeMode.ZERO == axeMode)
                    {
                        ParticleSystem temp = vfxComponent.Play(axeLastBrokenTag, axeHead.transform.position, Quaternion.identity, transform);
#if UNITY_EDITOR
                        Debug.Log(temp);
#endif

                        // AxeBreakingFinal도 마찬가지로 나빠지는 방향이면서 파티클이 실제로 나갔을 때만 재생한다.
                        if (true == isBreaking && null != temp)
                        {
                            Sound.PlayUI(SoundID.AxeBreakingFinal);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 모루로 수리했을 때 호출된다. 스프라이트가 바뀌지 않는 수리여도 항상 재생한다.
        /// 도끼가 하얗게 번쩍였다가 지금 내구도의 모습으로 돌아오며 뽀잉 튀어오른다.
        /// </summary>
        public void PlayRepairEffect()
        {
            KillRepairSequence();

            if (null != repairFlashOverlay && null != axeImage)
                repairFlashOverlay.sprite = axeImage.sprite;

            SetRepairFlashAlpha(1f);
            if (null != repairBounceTarget)
                repairBounceTarget.localScale = repairBounceBaseScale;

            repairSequence = DOTween.Sequence();

            if (null != repairFlashOverlay)
            {
                repairSequence.Insert(0f, DOTween.To(GetRepairFlashAlpha, SetRepairFlashAlpha, 0f, repairFlashDuration)
                    .SetEase(Ease.InQuad));
            }

            // HUD_Loot와 같은 쫀득한 뽀잉: 빠르게 커졌다가 탄성 있게 제자리로
            if (null != repairBounceTarget)
            {
                repairSequence.Insert(0f, repairBounceTarget.DOScale(repairBounceBaseScale * repairBounceScale, repairBounceDuration * 0.3f)
                    .SetEase(Ease.OutQuad));
                repairSequence.Insert(repairBounceDuration * 0.3f, repairBounceTarget.DOScale(repairBounceBaseScale, repairBounceDuration * 0.7f)
                    .SetEase(Ease.OutElastic));
            }

            repairSequence.OnKill(ResetRepairEffect);
        }

        private float repairFlashAlpha;

        private float GetRepairFlashAlpha()
        {
            return repairFlashAlpha;
        }

        private void SetRepairFlashAlpha(float _alpha)
        {
            repairFlashAlpha = _alpha;

            if (null == repairFlashOverlay)
                return;

            // 색 치환(UIEffect)이 실루엣을 하얗게 칠하므로 이미지 색은 알파만 담당한다.
            Color _color = Color.white;
            _color.a = _alpha;
            repairFlashOverlay.color = _color;
        }

        private void KillRepairSequence()
        {
            if (null != repairSequence && true == repairSequence.IsActive())
                repairSequence.Kill();
            repairSequence = null;
        }

        private void ResetRepairEffect()
        {
            SetRepairFlashAlpha(0f);
            if (null != repairBounceTarget)
                repairBounceTarget.localScale = repairBounceBaseScale;
        }

        private void OnDisable()
        {
            // HUD가 꺼질 때(어빌리티 트리 UI 등) 연출 도중이면 커진 채로 남지 않도록 정리한다.
            KillRepairSequence();
            ResetRepairEffect();
        }

        private System.Collections.IEnumerator PlayAxeBreakingExDelayed()
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(0.5f, 1.5f));
            Sound.PlayUI(SoundID.AxeBreakingEx);
        }

        private void OnDestroy()
        {
            KillRepairSequence();

            /*
            if (null != localizationManager && null != cachedRefreshLocalizedText)
            {
                localizationManager.OnLanguageChanged -= cachedRefreshLocalizedText;
            }
            */
        }
        // //유니티 이벤트 함수
    }
}
