using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;

namespace PresentationLayer.DOTweenAnimationSystem
{
    /// <summary>
    /// 오브젝트를 공중에 뜬 부유석처럼 둥실둥실 움직이게 하는 모션 클래스입니다.
    /// </summary>
    public class OMB_FloatingMove : ObjectMotionBase
    {
        [System.Serializable]
        public class ValueSettings
        {
            [Tooltip("부유 세기 (각 축으로 이동할 최대 거리)")]
            public Vector3 floatingStrength = new Vector3(8f, 12f, 0f);
            [Tooltip("부유 속도 (전체적인 움직임의 빠르기)")]
            public float floatingSpeed = 1f;
            [Tooltip("회전(Tilt) 세기 (둥실거리며 기울어지는 정도)")]
            public Vector3 rotationStrength = new Vector3(2f, 2f, 4f);
        }

        // //외부 의존성
        [Header("Floating Settings")]
        [SerializeField] private ValueSettings valueSettings = new ValueSettings();

        // //퍼블릭 초기화 및 제어 메서드
        private void Reset()
        {
            forwardDuration = 2.0f; // 기본 주기 (속도 1 기준)
            forwardEase = Ease.InOutSine; 
            valueSettings = new ValueSettings();
        }

        protected override void OnRectTransform(Sequence _seq, RectTransform _rect, Ease _currPublicEase)
        {
            if (null == _seq || null == _rect) return;

            Vector2 initialPos = _rect.anchoredPosition;
            Vector3 initialRot = _rect.localEulerAngles;
            
            stateCache.Add(new TargetInitialState {
                rectTransform = _rect,
                anchoredPosition = initialPos,
                localRotation = initialRot,
                localScale = _rect.localScale
            });

            float speedInvert = 1f / Mathf.Max(valueSettings.floatingSpeed, 0.01f);
            float randomDelay = Random.Range(0f, 1f);

            // 각 축에 서로 다른 소수 주기를 주어 리사주 곡선 형성 (절대 튀지 않고 부드러움)
            _seq.Join(_rect.DOAnchorPosX(initialPos.x + valueSettings.floatingStrength.x, speedInvert * 1.43f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetDelay(randomDelay));
            _seq.Join(_rect.DOAnchorPosY(initialPos.y + valueSettings.floatingStrength.y, speedInvert * 1.00f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetDelay(randomDelay));
            _seq.Join(_rect.DORotate(new Vector3(0, 0, initialRot.z + valueSettings.rotationStrength.z), speedInvert * 1.71f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetDelay(randomDelay));
        }

        protected override void OnTransform(Sequence _seq, Transform _trans, Ease _currPublicEase)
        {
            if (null == _seq || null == _trans) return;

            Vector3 initialPos = _trans.localPosition;
            Vector3 initialRot = _trans.localEulerAngles;

            stateCache.Add(new TargetInitialState {
                transform = _trans,
                localPosition = initialPos,
                localRotation = initialRot,
                localScale = _trans.localScale
            });

            float speedInvert = 1f / Mathf.Max(valueSettings.floatingSpeed, 0.01f);
            float randomDelay = Random.Range(0f, 1f);

            // 3D 유영 (X, Y, Z 독립 주기)
            _seq.Join(_trans.DOLocalMoveX(initialPos.x + valueSettings.floatingStrength.x, speedInvert * 1.32f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetDelay(randomDelay));
            _seq.Join(_trans.DOLocalMoveY(initialPos.y + valueSettings.floatingStrength.y, speedInvert * 1.00f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetDelay(randomDelay));
            _seq.Join(_trans.DOLocalMoveZ(initialPos.z + valueSettings.floatingStrength.z, speedInvert * 1.56f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetDelay(randomDelay));
            
            _seq.Join(_trans.DOLocalRotate(new Vector3(
                initialRot.x + valueSettings.rotationStrength.x, 
                initialRot.y + valueSettings.rotationStrength.y, 
                initialRot.z + valueSettings.rotationStrength.z), 
                speedInvert * 1.84f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetDelay(randomDelay));
        }

        protected override void ApplyTweenSettings(Tween _tween)
        {
            // 베이스 설정만 적용 (루프는 내부 트윈에서 처리)
            base.ApplyTweenSettings(_tween);
        }
    }
}
