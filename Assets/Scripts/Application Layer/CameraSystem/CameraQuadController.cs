using UnityEngine;

public class CameraQuadController : MonoBehaviour
{
    public Camera finalCamera; // Final Output Camera를 할당하세요.
    [SerializeField] private float pixelsPerUnit = 32f;
    private const float quadZOffset = 10f;

    public void Ready()
    {
        // [안전 여백 계산] 쿼드가 서브픽셀 단위로 움직여도 틈이 생기지 않도록 상하좌우 2픽셀 정도의 여유분 추가
        float pixelSize = 1f / pixelsPerUnit;
        float padding = pixelSize * 2f;

        // 카메라의 세로 크기 계산 (여백 포함)
        float height = (finalCamera.orthographicSize * 2f) + padding;
        // 화면 비율(Aspect Ratio) 계산 (여백 포함)
        float width = (height * finalCamera.aspect) + padding;

        // Quad 크기 조정
        transform.localScale = new Vector3(width, height, 1f);
        // 카메라 바로 앞에 위치 (Z축은 카메라의 Near Clip Plane보다 약간 뒤)
        transform.localPosition = new Vector3(0, 0, quadZOffset); 
    }
}
