using UnityEngine;

[ExecuteAlways]
public class IsometricShadowController : MonoBehaviour, IShadowDataProvider
{
    [SerializeField] private float dayCycleSpeed;

    [SerializeField] private float minHeightScale ;

    [SerializeField] private float maxHeightScale ;
    [SerializeField] private Material _shadowMaterial;
    [SerializeField] private Color _shadowColor = new Color(0, 0, 0, 0.5f);

    float IShadowDataProvider.dayCycleSpeed => dayCycleSpeed;

    float IShadowDataProvider.minHeightScale => minHeightScale;

    float IShadowDataProvider.maxHeightScale => maxHeightScale;

    private void Update()
    {
        if (_shadowMaterial == null) return;

        // 재질에 전역 색상만 설정함.
        // 변형 로직은 모두 Shadow.cs의 Transform으로 이동 완료.
        _shadowMaterial.SetColor("_BaseColor", _shadowColor);
    }
}
