using PresentationLayer.DOTweenAnimationSystem;
using System.Collections.Generic;
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

        // //내부 의존성
        private AxeMode axeMode = AxeMode.DB100;
        private float previousRatio = 1f;

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

            // 무음 재동기화(스킬로 최대 내구도만 바뀌는 등)에서는 스프라이트/모드/previousRatio만
            // 갱신하고, 실제 파손 사운드·VFX·애니메이션은 재생하지 않는다.
            if (false == _bPlayEffects)
                return;

            if (1f > _ratio && prevMode != axeMode)
            {
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
                        Debug.Log(temp);

                        // AxeBreakingFinal도 마찬가지로 나빠지는 방향이면서 파티클이 실제로 나갔을 때만 재생한다.
                        if (true == isBreaking && null != temp)
                        {
                            Sound.PlayUI(SoundID.AxeBreakingFinal);
                        }
                    }
                }
            }
        }

        private System.Collections.IEnumerator PlayAxeBreakingExDelayed()
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(0.5f, 1.5f));
            Sound.PlayUI(SoundID.AxeBreakingEx);
        }

        private void OnDestroy()
        {
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
