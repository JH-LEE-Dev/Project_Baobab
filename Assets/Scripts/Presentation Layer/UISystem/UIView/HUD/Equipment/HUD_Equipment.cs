using Unity.VisualScripting;
using UnityEngine;

namespace PresentationLayer.UISystem.UIView.HUD.Equipment
{
    /// <summary>
    /// 장비 HUD 시스템을 총괄하며 도끼와 라이플 HUD 아이템을 관리합니다.
    /// </summary>
    public class HUD_Equipment : MonoBehaviour
    {
        // //외부 의존성
        [SerializeField] private HUD_EquipmentAxe axeItem;
        [SerializeField] private HUD_EquipmentRifle rifleItem;

        // //내부 의존성
        private ICharacter character;

        // //퍼블릭 초기화 및 제어 메서드

        public void Initialize()
        {
            if (null != axeItem)
            {
                axeItem.Initialize();
            }

            if (null != rifleItem)
            {
                rifleItem.Initialize();
            }
        }

        public void BindingRef(ICharacter _character)
        {
            character = _character;

            Init_DefaultSettings();
        }

        private void Init_DefaultSettings()
        {
            if (null == character)
                return;

            IRifleComponent rifleComponent = character.armComponent?.rifleComponent;
            IAxeComponent axeComponent = character.armComponent?.axeComponent;
            IStatComponent statComponent = character.statComponent;

            if (null == rifleComponent || null == axeComponent || null == statComponent)
                return;

            if (null != rifleItem)
                rifleItem.UpdateAmmo(rifleComponent.mag, statComponent.magCap, rifleComponent.ammo);   
        }

        /// <summary>
        /// 무기 모드에 따라 HUD 아이템들의 활성 상태를 업데이트합니다.
        /// </summary>
        /// <param name="_mode">현재 무기 모드</param>
        /// <param name="_index">인덱스 (필요 시 확장용)</param>
        public void UpdateState(WeaponMode _mode)
        {
            if (null != axeItem)
                axeItem.SetActivate(WeaponMode.Axe == _mode);

            if (null != rifleItem)
                rifleItem.SetActivate(WeaponMode.Rifle == _mode);
        }

        public void OnDestroy()
        {
            
        }

        public void OnShow()
        {
           
        }

        public void OnHide()
        {
            
        }
    }
}
