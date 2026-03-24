using UnityEngine;

// 시네머신(기본값 0)보다 훨씬 나중에 실행되도록 보장 (2000 이상)
[DefaultExecutionOrder(2000)]
public class SubpixelSnapper : MonoBehaviour
{
    // 외부 의존성
    [SerializeField] private Camera pixelCamera;      // 저해상도 RT를 렌더링하는 카메라
    [SerializeField] private Transform quadTransform; // 출력용 쿼드 (Final Camera의 자식)

    // 내부 설정
    [SerializeField] private float pixelsPerUnit = 32f;
    private const float quadZOffset = 10f;

    /// <summary>
    /// 시네머신이 위치 결정을 끝낸 직후(DefaultExecutionOrder 2000) 실행됨
    /// </summary>
    private void LateUpdate()
    {
        if (pixelCamera == null || quadTransform == null)
        {
            return;
        }

        // 1. 시네머신이 댐핑을 적용해 계산한 '이번 프레임의 최종 부드러운 좌표'
        Vector3 rawPosition = pixelCamera.transform.position;

        // 2. 픽셀 그리드에 딱 맞는 '스냅된 좌표' 계산
        // Mathf.Round 대신 Floor를 사용하여 소수점 경계에서의 지터링(Jittering) 방지
        float snapX = Mathf.Floor(rawPosition.x * pixelsPerUnit) / pixelsPerUnit;
        float snapY = Mathf.Floor(rawPosition.y * pixelsPerUnit) / pixelsPerUnit;
        
        // 3. 저해상도 카메라는 픽셀 격자에 맞춰 렌더링하도록 강제 고정
        pixelCamera.transform.position = new Vector3(snapX, snapY, rawPosition.z);

        // 4. 실제 위치와 스냅된 위치 사이의 '소수점 오차' 계산
        float offsetX = rawPosition.x - snapX;
        float offsetY = rawPosition.y - snapY;

        // 5. [개선] 오차값을 실제 '화면 픽셀' 단위로 스냅하여 이글거림 해결
        // 월드 단위 1당 실제 화면의 픽셀 수 계산 (Screen Height / OrthoSize * 2)
        float worldToScreenPPU = Screen.height / (pixelCamera.orthographicSize * 2f);
        
        float finalOffsetX = Mathf.Round(offsetX * worldToScreenPPU) / worldToScreenPPU;
        float finalOffsetY = Mathf.Round(offsetY * worldToScreenPPU) / worldToScreenPPU;

        // 6. 출력용 쿼드를 화면 픽셀에 정렬된 오차만큼 밀어줌
        quadTransform.localPosition = new Vector3(-finalOffsetX, -finalOffsetY, quadZOffset);
    }
}
