using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem.Motions.UI;
using UnityEngine;

namespace PresentationLayer.UISystem.UIView.HUD.Equipment
{
    /// <summary>
    /// 장비 HUD 시스템을 총괄하며 도끼 HUD 아이템을 관리합니다.
    /// </summary>
    public class HUD_Equipment : MonoBehaviour
    {
        // //외부 의존성
        [Header("Component Settings")]
        [SerializeField] private HUD_EquipmentAxe axeItem;
        [SerializeField] private UIMotion_Pop axePop;
        [Tooltip("도끼 HUD 왼쪽의 과열 게이지. 보일지 말지는 UIView_HUD가 정한다")]
        [SerializeField] private HUD_HeatGauge heatGauge;

        [Header("Axe Deposit Slide")]
        [Tooltip("도끼를 모루에 맡기면 도끼 HUD만 이만큼(px) 아래로 내려가 화면 밖으로 빠진다")]
        [SerializeField] private float axeDepositHiddenOffsetY = 110f;
        [SerializeField] private float axeDepositAnticipationY = 5f;
        [SerializeField] private float axeDepositAnticipationDuration = 0.08f;
        [SerializeField] private float axeDepositExitDuration = 0.26f;
        [SerializeField] private float axeDepositReturnDuration = 0.42f;
        [SerializeField] private float axeDepositReturnOvershoot = 2.2f;

        // //내부 의존성
        private ICharacter character;
        private IOverheatComponent overheatState;
        private int shownOverheatStage; // 마지막으로 도끼 HUD에 넣은 과열 단계(단계가 오를 때만 섬광 연출)

        // 모루에 도끼를 맡긴 동안 도끼 HUD를 내리는 연출(위치만 움직인다 - 크기 연출은 도끼 HUD 자체 연출과 겹치지 않게 쓰지 않는다)
        private RectTransform axeRect;
        private Vector2 axeBasePosition;
        private bool bAxeBaseCaptured;
        private bool bAxeHiddenByDeposit;
        private Sequence axeDepositSequence;

        // //퍼블릭 초기화 및 제어 메서드

        public void Initialize(InputManager _inputManager = null, LocalizationManager _localizationManager = null)
        {
            if (null != axeItem)
            {
                axeItem.Initialize(_inputManager, _localizationManager);
            }

            if (null != heatGauge)
                heatGauge.Initialize();
        }

        /// <summary>
        /// 과열 게이지를 보이거나 숨긴다. _bAnimate가 true면 아래에서 튀어 오르는 등장 / 아래로 빠지는 퇴장 연출을 한다.
        /// </summary>
        public void SetHeatGaugeVisible(bool _bVisible, bool _bAnimate)
        {
            if (null != heatGauge)
                heatGauge.SetVisible(_bVisible, _bAnimate);
        }

        public void BindingRef(ICharacter _character)
        {
            character = _character;

            // 최초 바인딩은 파손 연출과 무관한 초기 동기화이므로 사운드/이펙트 없이 갱신한다.
            UpdateAxeDurability(false);

            IAxeComponent axeComponent = character.armComponent?.axeComponent;

            if (null != axeComponent)
            {
                axeComponent.AxeAttackedEvent -= OnAxeAttacked;
                axeComponent.AxeAttackedEvent += OnAxeAttacked;
                axeComponent.DurabilityRepairedEvent -= OnAxeRepaired;
                axeComponent.DurabilityRepairedEvent += OnAxeRepaired;
            }

            // 모루 맡기기: 도끼 HUD만 내려갔다 올라온다. 바인딩 시점에는 지금 상태로 바로 맞춘다.
            character.AxeDepositStateChangedEvent -= OnAxeDepositStateChanged;
            character.AxeDepositStateChangedEvent += OnAxeDepositStateChanged;
            SetAxeHiddenByDeposit(character.bAxeDeposited, false);

            // 과열 아우라: 이전에 바인딩한 캐릭터의 구독은 풀고, 지금 상태를 먼저 맞춘 뒤 이후 변화는 이벤트로 받는다.
            UnbindOverheat();
            overheatState = character.overheatState;
            if (null != overheatState)
            {
                overheatState.OverheatStateChangedEvent += OnOverheatStateChanged;
                overheatState.OverheatStageChangedEvent += OnOverheatStageChanged;
                overheatState.OverheatLevelChangedEvent += OnOverheatLevelChanged;

                // 바인딩 시점 동기화는 단계 전환 연출 없이 맞춘다(단계를 먼저 넣어야 켜질 때 그 단계 모습으로 점화한다)
                shownOverheatStage = overheatState.Stage;
                if (null != axeItem)
                {
                    axeItem.SetOverheatStage(overheatState.Stage, overheatState.StageProgress);
                    axeItem.SetOverheat(overheatState.IsActive);
                }
            }

            // 과열 게이지도 같은 상태를 구독한다(null이면 게이지는 0 · x1.00으로 남는다).
            if (null != heatGauge)
                heatGauge.Bind(overheatState);
        }

        private void OnOverheatStateChanged(bool _bActive)
        {
            if (null != axeItem)
                axeItem.SetOverheat(_bActive);
        }

        // 단계 전환. OverheatComponent는 단계 이벤트를 켜짐 이벤트(OverheatStateChangedEvent)보다 먼저 보내므로 0 -> 1에서도 단계가 먼저 들어간다.
        // 단계가 오를 때만 도끼 섬광/튕김을 재생한다(0으로 돌아가는 초기화는 소화 연출만 한다).
        private void OnOverheatStageChanged(int _stage)
        {
            if (null == axeItem || null == overheatState)
                return;

            axeItem.SetOverheatStage(_stage, overheatState.StageProgress);
            if (_stage > shownOverheatStage)
                axeItem.PlayOverheatStageEffect(_stage);

            shownOverheatStage = _stage;
        }

        // 단계 안 진행도(예열 불티 양, 가열 불꽃 높이)를 따라 올린다
        private void OnOverheatLevelChanged(int _level)
        {
            if (null != axeItem && null != overheatState)
                axeItem.SetOverheatStage(overheatState.Stage, overheatState.StageProgress);
        }

        private void UnbindOverheat()
        {
            if (null != heatGauge)
                heatGauge.Unbind();

            if (null != overheatState)
            {
                overheatState.OverheatStateChangedEvent -= OnOverheatStateChanged;
                overheatState.OverheatStageChangedEvent -= OnOverheatStageChanged;
                overheatState.OverheatLevelChangedEvent -= OnOverheatLevelChanged;
                overheatState = null;
            }
        }

        private void OnAxeDepositStateChanged(bool _bDeposited)
        {
            SetAxeHiddenByDeposit(_bDeposited, true);
        }

        // 도끼를 맡기면 도끼 HUD가 단독으로 아래로 빠지고(움찔 → 빨려 내려감), 돌려받으면 아래에서 튀어 오른다.
        // 과열 게이지 등장/퇴장(HUD_HeatGauge)과 같은 움직임이다. 시간은 unscaled.
        private void SetAxeHiddenByDeposit(bool _bHidden, bool _bAnimate)
        {
            if (null == axeItem)
                return;

            if (null == axeRect)
                axeRect = axeItem.transform as RectTransform;

            if (null == axeRect)
                return;

            if (false == bAxeBaseCaptured)
            {
                axeBasePosition = axeRect.anchoredPosition;
                bAxeBaseCaptured = true;
            }

            if (_bHidden == bAxeHiddenByDeposit && (null == axeDepositSequence || false == axeDepositSequence.IsActive()))
            {
                if (false == _bAnimate)
                    axeRect.anchoredPosition = HiddenOrBasePosition(_bHidden);
                return;
            }

            bAxeHiddenByDeposit = _bHidden;
            KillAxeDepositSlide();

            if (false == _bAnimate || false == gameObject.activeInHierarchy)
            {
                axeRect.anchoredPosition = HiddenOrBasePosition(_bHidden);
                return;
            }

            axeDepositSequence = DOTween.Sequence().SetUpdate(true);

            if (_bHidden)
            {
                axeDepositSequence.Append(axeRect.DOAnchorPosY(axeBasePosition.y + axeDepositAnticipationY, axeDepositAnticipationDuration, true)
                    .SetEase(Ease.OutQuad));
                axeDepositSequence.Append(axeRect.DOAnchorPosY(axeBasePosition.y - axeDepositHiddenOffsetY, axeDepositExitDuration, true)
                    .SetEase(Ease.InBack));
            }
            else
            {
                axeDepositSequence.Append(axeRect.DOAnchorPosY(axeBasePosition.y, axeDepositReturnDuration, true)
                    .SetEase(Ease.OutBack, axeDepositReturnOvershoot));
            }
        }

        private Vector2 HiddenOrBasePosition(bool _bHidden)
        {
            return _bHidden ? new Vector2(axeBasePosition.x, axeBasePosition.y - axeDepositHiddenOffsetY) : axeBasePosition;
        }

        private void KillAxeDepositSlide()
        {
            if (null != axeDepositSequence)
            {
                axeDepositSequence.Kill();
                axeDepositSequence = null;
            }
        }

        // 실제 공격/수리로 도끼 내구도가 변한 경우에만 호출된다. 이때만 파손 사운드/이펙트를 재생한다.
        private void OnAxeAttacked()
        {
            UpdateAxeDurability(true);
        }

        // 모루 수리. 내구도 갱신(OnAxeAttacked)이 먼저 끝난 뒤 불리므로 도끼 그림은 이미 새 내구도 모습이다.
        private void OnAxeRepaired()
        {
            if (null != axeItem)
                axeItem.PlayRepairEffect();
        }

        /// <summary>
        /// 도끼 게이지를 현재 스탯에 맞춰 재계산한다.
        /// </summary>
        /// <param name="_bPlayEffects">
        /// true면 실제 내구도 변화(공격/수리)로 간주해 파손 사운드/VFX를 재생한다.
        /// false면 도끼 내구도 강화 스킬 등으로 최대치만 바뀌어 비율이 재계산되는 경우이므로
        /// (실제로 깎인 게 아니므로) 사운드/VFX 없이 게이지만 조용히 갱신한다.
        /// </param>
        public void UpdateAxeDurability(bool _bPlayEffects = false)
        {
            if (null == character)
                return;

            IAxeComponent axeComponent = character.armComponent?.axeComponent;
            IStatComponent statComponent = character.statComponent;

            if (null == axeComponent || null == statComponent)
                return;

            if (null != axeItem)
            {
                float _ratio = Mathf.Clamp01(axeComponent.durability / statComponent.axeDurability);
                axeItem.UpdateGauge(_ratio, _bPlayEffects);
            }
        }
        public void OnDestroy()
        {
            UnbindOverheat();
            KillAxeDepositSlide();

            if (null == character)
                return;

            character.AxeDepositStateChangedEvent -= OnAxeDepositStateChanged;

            IAxeComponent axeComponent = character.armComponent?.axeComponent;

            if (null != axeComponent)
            {
                axeComponent.AxeAttackedEvent -= OnAxeAttacked;
                axeComponent.DurabilityRepairedEvent -= OnAxeRepaired;
            }
        }

        public void OnShow()
        {
           
        }

        public void OnHide()
        {
            
        }
    }
}
