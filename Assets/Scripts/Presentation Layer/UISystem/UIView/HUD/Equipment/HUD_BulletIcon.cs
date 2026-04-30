using UnityEngine;
using UnityEngine.UI;
using PresentationLayer.DOTweenAnimationSystem;
using System;
using UnityEngine.Events;
using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem.Motions.UI;

namespace PresentationLayer.UISystem.UIView.HUD.Equipment
{
    /// <summary>
    /// 개별 총알 아이콘의 시각적 상태와 연출을 관리하는 컴포넌트입니다.
    /// 모든 연출 시작 전 StopAllMotions를 통해 중복 실행 문제를 방지합니다.
    /// </summary>
    public class HUD_BulletIcon : MonoBehaviour
    {
        // //외부 의존성
        [SerializeField] private Image emptyImage;         // 배경 (빈 탄약)
        [SerializeField] private Image filledImage;        // 전경 (차 있는 탄약)
        [SerializeField] private GameObject outline;

        [Header("Motions")]
        private UIMotion_GravityRot ejectMotion;
        private UIMotion_Reset resetMotion;
        private UIMotion_Reload reloadMotion;
        private UIMotion_Gather gatherMotion;


        // //내부 의존성
        private bool isCurrentlyFilled = true;
        private RectTransform cachedRect;

        private UnityAction onStartEvent;
        private UnityAction onCompleteEvent;

        // //퍼블릭 초기화 및 제어 메서드

        public void Initialize(bool _startFilled)
        {
            isCurrentlyFilled = _startFilled;
            cachedRect = GetComponent<RectTransform>();

            if (null != filledImage)
                filledImage.gameObject.SetActive(isCurrentlyFilled);

            if (null == ejectMotion)
                ejectMotion = GetComponentInChildren<UIMotion_GravityRot>();

            ejectMotion?.Initialize();

            if (null == resetMotion)
                resetMotion = GetComponentInChildren<UIMotion_Reset>();

            resetMotion?.Initialize();

            if (null == reloadMotion)
                reloadMotion = GetComponentInChildren<UIMotion_Reload>();

            reloadMotion?.Initialize();

            if (null == gatherMotion)
                gatherMotion = GetComponentInChildren<UIMotion_Gather>();

            gatherMotion?.Initialize();
        }

        public Vector3 GetPosition()
        {
            return (null != cachedRect) ? cachedRect.position : Vector3.zero;
        }

        /// <summary>
        /// 실행 중인 모든 모션을 정지하고 트윈을 제거합니다.
        /// </summary>
        public void StopAllMotions()
        {
            ejectMotion?.Stop();
            resetMotion?.Stop();
            reloadMotion?.Stop();
            gatherMotion?.Stop();

            // 트윈 엔진 차원에서도 확실히 정리
            if (null != cachedRect)
                cachedRect.DOKill();
        }

        /// <param name="_isFilled">채워짐 여부</param>
        /// <param name="_shouldAnimate">연출 재생 여부</param>
        public void SetState(bool _isFilled, bool _shouldAnimate)
        {
            if (_isFilled == isCurrentlyFilled)
                return;

            isCurrentlyFilled = _isFilled;

            if (false == _isFilled && true == _shouldAnimate)
                PlayEjectMotion();
        }

#region Motions

        private void PlayEjectMotion()
        {
            //StopAllMotions();

            if (null == ejectMotion)
                return;

            ejectMotion.Play(HandleEjectComplete);
        }

        private void HandleEjectComplete()
        {
            if (null == filledImage)
                return;

            filledImage.gameObject.SetActive(false);
        }

        public void PlayResetMotion(float _delay, bool _isFilled, UnityAction _onStart, UnityAction _onComplete)
        {
            StopAllMotions();

            isCurrentlyFilled = _isFilled;

            if (null != filledImage)
                filledImage.gameObject.SetActive(_isFilled);

            onStartEvent = _onStart;
            onCompleteEvent = _onComplete;

            resetMotion?.Play(_delay, HandleResetStart, HandleResetComplete);
        }

        private void HandleResetStart()
        {
            if (null != onStartEvent)
                onStartEvent.Invoke();
        }

        private void HandleResetComplete()
        {
            onCompleteEvent?.Invoke();
        }

        public void PlayReloadMotion(float _delay, UnityAction _onComplete)
        {
            StopAllMotions();

            if (null == reloadMotion)
                return;

            ejectMotion?.ResetToInitialState();

            reloadMotion.Play(_delay, _onComplete);
        }

        public void PlayGatherMotion(float _delay, Vector3 _targetPos, UnityAction _onStart, UnityAction _onComplete)
        {
            StopAllMotions();

            if (null == gatherMotion)
                return;

            gatherMotion.Play(_delay, _targetPos, _onStart, _onComplete);
        }

#endregion

        public void SetActive(bool _isActive)
        {
            if (null == outline)
                return;

            outline.SetActive(_isActive);
        }
    }
}
