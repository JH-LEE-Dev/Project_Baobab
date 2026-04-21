using UnityEngine;
using UnityEngine.UI;

namespace PresentationLayer.DOTweenAnimationSystem
{
    [System.Serializable]
    public class MotionTarget
    {
        // //외부 의존성
        public Transform transform;
        public RectTransform rectTransform;
        public CanvasGroup canvasGroup;
        public SpriteRenderer spriteRenderer;
        public Graphic uiGraphic;

        // //퍼블릭 제어 메서드
        public Component GetAny()
        {
            if (null != rectTransform) 
                return rectTransform;
            if (null != transform) 
                return transform;
            if (null != canvasGroup) 
                return canvasGroup;
            if (null != spriteRenderer) 
                return spriteRenderer;
            if (null != uiGraphic) 
                return uiGraphic;
                
            return null;
        }
    }
}