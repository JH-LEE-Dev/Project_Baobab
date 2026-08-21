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
            
            // 카메라가 실제로 그리는 영역을 기준으로 정규화한다 (0~1 범위).
            // 크롭(Pillarbox)이 켜지면 셰이더가 보는 화면 공간이 화면 전체가 아니라 잘린 영역이라,
            // Screen 크기로 나누면 마스크 위치가 어긋난다. 크롭이 없으면 결과가 같다.
            // [ExecuteAlways]라 에디터에서도 도므로 싱글톤 대신 캔버스의 카메라를 직접 쓴다.
            Rect viewRect = (cam != null && 0f < cam.pixelRect.width)
                ? cam.pixelRect
                : new Rect(0f, 0f, Screen.width, Screen.height);

            float sw = viewRect.width;
            float sh = viewRect.height;

            Vector4 maskRect;
            maskRect.x = (screenPoint.x - viewRect.xMin) / sw;
            maskRect.y = (screenPoint.y - viewRect.yMin) / sh;

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
