using UnityEngine;

public class CameraQuadController : MonoBehaviour
{
    public Camera finalCamera; // Final Output Camera를 할당하세요.
    [SerializeField] private float pixelsPerUnit = 32f;
    private const float quadZOffset = 10f;

    public void Ready()
    {
        // 1. 기본 카메라 뷰 크기 계산 (패딩 전)
        float baseHeight = finalCamera.orthographicSize * 2f;
        float baseWidth = baseHeight * finalCamera.aspect;

        // 2. 안전 여백 계산 (상하좌우 각각 2픽셀씩, 총 4픽셀 분량 추가)
        float pixelSize = 1f / pixelsPerUnit;
        float padding = pixelSize * 2f;

        // 3. 패딩이 적용된 실제 쿼드 크기
        float paddedWidth = baseWidth + (padding * 2f);
        float paddedHeight = baseHeight + (padding * 2f);

        // 4. Quad 크기 조정
        transform.localScale = new Vector3(paddedWidth, paddedHeight, 1f);

        // 5. UV 조정을 통해 텍스처 늘어남 방지 (1:1 픽셀 매칭 유지)
        Vector2 tiling = new Vector2(paddedWidth / baseWidth, paddedHeight / baseHeight);
        Vector2 offset = new Vector2(-(padding / baseWidth), -(padding / baseHeight));

        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.mainTextureScale = tiling;
            renderer.material.mainTextureOffset = offset;
        }

        // 카메라 바로 앞에 위치
        transform.localPosition = new Vector3(0, 0, quadZOffset); 
    }
}
