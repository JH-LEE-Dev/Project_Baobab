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

        // //내부 의존성
        private ICharacter character;

        // //퍼블릭 초기화 및 제어 메서드

        public void Initialize(InputManager _inputManager = null, LocalizationManager _localizationManager = null)
        {
            if (null != axeItem)
            {
                axeItem.Initialize(_inputManager, _localizationManager);
            }
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
            }
        }

        // 실제 공격/수리로 도끼 내구도가 변한 경우에만 호출된다. 이때만 파손 사운드/이펙트를 재생한다.
        private void OnAxeAttacked()
        {
            UpdateAxeDurability(true);
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
            if (null == character)
                return;

            IAxeComponent axeComponent = character.armComponent?.axeComponent;

            if (null != axeComponent)
            {
                axeComponent.AxeAttackedEvent -= OnAxeAttacked;
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
