using UnityEngine;
using UnityEngine.UI;

namespace ProjectBaobab
{
    [ExecuteAlways]
    [RequireComponent(typeof(Image))]
    public class NotificationBadgeLinker : MonoBehaviour
    {
        // 외부 의존성
        [SerializeField] private RectTransform targetIcon;

        // 내부 의존성
        private Image badgeImage;
        private Material badgeMaterial;
        private static readonly int maskRectPropertyId = Shader.PropertyToID("_MaskRect");

        public void Initialize(RectTransform _targetIcon)
        {
            targetIcon = _targetIcon;
            SetupReferences();
        }

        private void SetupReferences()
        {
            if (badgeImage == null)
            {
                badgeImage = GetComponent<Image>();
            }

            // 머티리얼 인스턴스를 생성하여 개별 속성을 제어합니다.
            if (badgeImage != null && (badgeMaterial == null || badgeMaterial != badgeImage.material))
            {
                badgeMaterial = badgeImage.material;
            }
        }

        private void UpdateMaskRect()
        {
            if (targetIcon == null || badgeMaterial == null)
            {
                return;
            }

            Canvas canvas = targetIcon.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            // 아이콘의 화면 좌표(Pixel) 가져오기
            Camera cam = (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, targetIcon.position);
            
            // 현재 화면 해상도로 정규화 (0~1 범위)
            float sw = Screen.width;
            float sh = Screen.height;

            Vector4 maskRect;
            maskRect.x = screenPoint.x / sw;
            maskRect.y = screenPoint.y / sh;

            // 아이콘의 화면상 크기 계산 (0~1 범위)
            Vector3[] corners = new Vector3[4];
            targetIcon.GetWorldCorners(corners);
            
            // 월드 좌표 간의 거리를 화면 픽셀 거리로 변환
            Vector2 screenCorner0 = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 screenCorner2 = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            
            float screenWidth = Mathf.Abs(screenCorner2.x - screenCorner0.x);
            float screenHeight = Mathf.Abs(screenCorner2.y - screenCorner0.y);

            maskRect.z = screenWidth / sw;
            maskRect.w = screenHeight / sh;

            badgeMaterial.SetVector(maskRectPropertyId, maskRect);
        }

        private void Awake()
        {
            SetupReferences();
        }

        private void Update()
        {
            UpdateMaskRect();
        }

        // 에디터에서 값이 변경될 때 대응
        private void OnValidate()
        {
            SetupReferences();
        }
    }
}
