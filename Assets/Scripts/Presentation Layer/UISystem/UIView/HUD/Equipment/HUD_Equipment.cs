using PresentationLayer.DOTweenAnimationSystem;
using PresentationLayer.DOTweenAnimationSystem.Motions.UI;
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
        [SerializeField] private RectTransform icons;
        [SerializeField] private CanvasGroup iconGroup;

        [Header("Axe Position Settings")]
        [SerializeField] private Vector2 axePosWithRifle;
        [SerializeField] private Vector2 axePosAxeOnly;

        [SerializeField] private UIMotion_Pop ammoBoxPop;
        [SerializeField] private UIMotion_Pop axePop;
        [SerializeField] private UIMotion_Pop riflePop;

        // //내부 의존성
        private ICharacter character;
        private RectTransform axeRect;

        // //퍼블릭 초기화 및 제어 메서드

        public void Initialize()
        {
            if (null != axeItem)
            {
                axeItem.Initialize();
                axeRect = axeItem.GetComponent<RectTransform>();
            }

            if (null != rifleItem)
            {
                rifleItem.Initialize();
            }
        }

        public void BindingRef(ICharacter _character)
        {
            character = _character;

            UpdateAmmo();
            UpdateAxeDurability();
            UpdateRifleVisibility();

            IRifleComponent rifleComponent = character.armComponent?.rifleComponent;
            IAxeComponent axeComponent = character.armComponent?.axeComponent;
            IStatComponent statComponent = character.statComponent;

            if (null != rifleComponent)
            {
                rifleComponent.RifleFiredEvent -= UpdateAmmo;
                rifleComponent.RifleFiredEvent += UpdateAmmo;

                rifleComponent.ReloadStartEvent -= PlayReloadMotion;
                rifleComponent.ReloadStartEvent += PlayReloadMotion;

                rifleComponent.ReloadFinishedEvent -= UpdateAmmo;
                rifleComponent.ReloadFinishedEvent += UpdateAmmo;

                rifleComponent.ReloadFinishedEvent -= PlayResetMotion;
                rifleComponent.ReloadFinishedEvent += PlayResetMotion;
            }

            if (null != axeComponent)
            {
                axeComponent.AxeAttackedEvent -= UpdateAxeDurability;
                axeComponent.AxeAttackedEvent += UpdateAxeDurability;
            }

            if (null != statComponent)
            {
                statComponent.CanHuntEvent -= UpdateRifleVisibility;
                statComponent.CanHuntEvent += UpdateRifleVisibility;
            }
        }

        public void UpdateAmmo()
        {
            if (null == character)
                return;

            IRifleComponent rifleComponent = character.armComponent?.rifleComponent;
            IStatComponent statComponent = character.statComponent;

            if (null == rifleComponent || null == statComponent)
                return;

            if (null != rifleItem)
                rifleItem.UpdateAmmo(rifleComponent.mag, statComponent.magCap, rifleComponent.ammo);   
        }

        private void UpdateAxeDurability()
        {
            if (null == character)
                return;

            IAxeComponent axeComponent = character.armComponent?.axeComponent;
            IStatComponent statComponent = character.statComponent;

            if (null == axeComponent || null == statComponent)
                return;

            if (null != axeItem)
                axeItem.UpdateGauge(axeComponent.durability / statComponent.axeDurability);
        }

        private void PlayReloadMotion()
        {
            if (null == character || null == character.statComponent)
                return;

            if (null != rifleItem)
                rifleItem.PlayReloadMotion(character.statComponent.reloadDuration);
        }

        private void PlayResetMotion()
        {
            if (null != rifleItem)
                rifleItem.PlayResetMotion(PlayAmmoBoxPop);
        }

        private void PlayAmmoBoxPop()
        {
            ammoBoxPop?.Play();
        }

        public void UpdateRifleVisibility()
        {
            if (null == character || null == character.statComponent)
                return;

            if (null == rifleItem || null == axeItem)
                return;

            bool _isRifleVisible = character.statComponent.bCanHunting;
            
            // 라이플 UI 노출 제어
            rifleItem.gameObject.SetActive(_isRifleVisible);

            // 도끼 위치 제어
            if (null == axeRect)
                axeRect = axeItem.GetComponent<RectTransform>();

            if (null != axeRect)
                axeRect.anchoredPosition = _isRifleVisible ? axePosWithRifle : axePosAxeOnly;

            UpdateIconsVisibility();
        }

        private void UpdateIconsVisibility()
        {
            if (null == icons || null == iconGroup || null == axeItem || null == rifleItem)
                return;

            bool activate = true == axeItem.gameObject.activeSelf && true == rifleItem.gameObject.activeSelf;

            icons.gameObject.SetActive(activate);
        }

        /// <summary>
        /// 무기 모드에 따라 HUD 아이템들의 활성 상태를 업데이트합니다.
        /// </summary>
        /// <param name="_mode">현재 무기 모드</param>
        public void UpdateState(WeaponMode _mode)
        {
            if (null != axeItem)
                axeItem.SetActivate(WeaponMode.Axe == _mode);

            if (null != rifleItem)
                rifleItem.SetActivate(WeaponMode.Rifle == _mode);

            if (WeaponMode.Axe == _mode)
            {
                axePop?.Play();
                rifleItem?.PlayGatherMotion();
            }
            else if (WeaponMode.Rifle == _mode)
            {
                riflePop?.Play();
                rifleItem?.PlayResetMotion();
            }
        }

        public void OnDestroy()
        {
            if (null == character)
                return;

            IRifleComponent rifleComponent = character.armComponent?.rifleComponent;
            IAxeComponent axeComponent = character.armComponent?.axeComponent;
            IStatComponent statComponent = character.statComponent;

            if (null != rifleComponent)
            {
                rifleComponent.RifleFiredEvent -= UpdateAmmo;
                rifleComponent.ReloadStartEvent -= PlayReloadMotion;
                rifleComponent.ReloadFinishedEvent -= UpdateAmmo;
                rifleComponent.ReloadFinishedEvent -= PlayResetMotion;
            }

            if (null != axeComponent)
            {
                axeComponent.AxeAttackedEvent -= UpdateAxeDurability;
            }

            if (null != statComponent)
            {
                statComponent.CanHuntEvent -= UpdateRifleVisibility;
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
