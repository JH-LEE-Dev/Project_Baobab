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

        public void Initialize()
        {
            if (null != axeItem)
            {
                axeItem.Initialize();
            }
        }

        public void BindingRef(ICharacter _character)
        {
            character = _character;

            UpdateAxeDurability();

            IAxeComponent axeComponent = character.armComponent?.axeComponent;

            if (null != axeComponent)
            {
                axeComponent.AxeAttackedEvent -= UpdateAxeDurability;
                axeComponent.AxeAttackedEvent += UpdateAxeDurability;
            }
        }

        public void UpdateAxeDurability()
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
                axeItem.UpdateGauge(_ratio);
            }
        }
        public void OnDestroy()
        {
            if (null == character)
                return;

            IAxeComponent axeComponent = character.armComponent?.axeComponent;

            if (null != axeComponent)
            {
                axeComponent.AxeAttackedEvent -= UpdateAxeDurability;
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
