using UnityEngine;

public class CameraQuadController : MonoBehaviour
{
    public Camera finalCamera; // Final Output Camera를 할당하세요.
    [SerializeField] private float pixelsPerUnit = 32f;
    private const float quadZOffset = 10f;

    public void Ready()
    {
        // 1. 가시 영역의 순수 픽셀 크기 계산
        float hPx = finalCamera.orthographicSize * 2 * pixelsPerUnit;
        float wPx = hPx * finalCamera.aspect;

        // 2. 가로/세로 각각 정수로 올림 처리 후 정확히 4픽셀씩만 추가
        float finalH = Mathf.Ceil(hPx) + 8;
        float finalW = Mathf.Ceil(wPx) + 8;

        // 3. 최종 Scale 적용 (이제 20.125와 같이 의도한 숫자가 정확히 나옵니다)
        transform.localScale = new Vector3(finalW / pixelsPerUnit, finalH / pixelsPerUnit, 1f);
        transform.localPosition = new Vector3(0, 0, quadZOffset); 
    }
}
