using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;

namespace PresentationLayer.DOTweenAnimationSystem
{
    /// <summary>
    /// UI 메쉬의 버텍스 오프셋을 조절하여 X축 및 Y축 방향의 비틀기(Shear/Skew) 효과를 표현하는 컴포넌트입니다.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    [AddComponentMenu("UI/Effects/Skew Effect")]
    public class SkewEffect : BaseMeshEffect
    {
        // //외부 의존성
        [SerializeField] private float skewX = 0f;
        [SerializeField] private float skewY = 0f;

        private DOGetter<float> cachedGetSkewX;
        private DOSetter<float> cachedSetSkewX;
        private DOGetter<float> cachedGetSkewY;
        private DOSetter<float> cachedSetSkewY;

        // //퍼블릭 프로퍼티
        public float SkewX
        {
            get => skewX;
            set
            {
                if (Mathf.Approximately(skewX, value))
                    return;

                skewX = value;
                if (null != graphic)
                    graphic.SetVerticesDirty();
            }
        }

        public float SkewY
        {
            get => skewY;
            set
            {
                if (Mathf.Approximately(skewY, value))
                    return;

                skewY = value;
                if (null != graphic)
                    graphic.SetVerticesDirty();
            }
        }

        protected override void Awake()
        {
            base.Awake();
            cachedGetSkewX = GetSkewXVal;
            cachedSetSkewX = SetSkewXVal;
            cachedGetSkewY = GetSkewYVal;
            cachedSetSkewY = SetSkewYVal;
        }

        private float GetSkewXVal() => skewX;
        private void SetSkewXVal(float _v) => SkewX = _v;
        private float GetSkewYVal() => skewY;
        private void SetSkewYVal(float _v) => SkewY = _v;

        public TweenerCore<float, float, FloatOptions> DOSkewX(float _endValue, float _duration)
        {
            if (null == cachedGetSkewX)
            {
                cachedGetSkewX = GetSkewXVal;
                cachedSetSkewX = SetSkewXVal;
            }
            return DOTween.To(cachedGetSkewX, cachedSetSkewX, _endValue, _duration).SetTarget(this);
        }

        public TweenerCore<float, float, FloatOptions> DOSkewY(float _endValue, float _duration)
        {
            if (null == cachedGetSkewY)
            {
                cachedGetSkewY = GetSkewYVal;
                cachedSetSkewY = SetSkewYVal;
            }
            return DOTween.To(cachedGetSkewY, cachedSetSkewY, _endValue, _duration).SetTarget(this);
        }

        // //퍼블릭 제어 메서드 및 오버라이드

        /// <summary>
        /// 버텍스를 변환하여 비틀기 효과를 메쉬에 적용합니다.
        /// </summary>
        public override void ModifyMesh(VertexHelper _vh)
        {
            if (false == IsActive())
                return;

            int count = _vh.currentVertCount;
            if (0 >= count)
                return;

            UIVertex vertex = default;
            Rect rect = graphic.rectTransform.rect;
            float height = rect.height;
            float width = rect.width;

            if (Mathf.Approximately(height, 0f) || Mathf.Approximately(width, 0f))
                return;

            for (int i = 0; i < count; i++)
            {
                _vh.PopulateUIVertex(ref vertex, i);

                float normalizedY = vertex.position.y / height;
                float normalizedX = vertex.position.x / width;

                vertex.position.x += normalizedY * skewX * width;
                vertex.position.y += normalizedX * skewY * height;

                _vh.SetUIVertex(vertex, i);
            }
        }
    }
}
