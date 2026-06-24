using UnityEngine;

public class StaticObj : MonoBehaviour
{
    // // 외부 의존성
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private float hdrIntensity = 1f;

    // // 내부 의존성 및 상태 필드
    private CustomSortable customSortable;

    // // 퍼블릭 초기화 및 제어 메서드

    private static readonly int HDRIntensityID = Shader.PropertyToID("_HDRIntensity");

    public void Initialize()
    {
        customSortable = GetComponent<CustomSortable>();
        if (customSortable != null)
        {
            customSortable.Initialize(transform);
            customSortable.AddSpriteRenderer(sr);
        }

        var mpb = new MaterialPropertyBlock();
        sr.GetPropertyBlock(mpb);
        mpb.SetFloat(HDRIntensityID, hdrIntensity);
        sr.SetPropertyBlock(mpb);
    }

    public void SetSortingOrder()
    {
        if (customSortable != null)
            customSortable.ManualLateUpdate();
    }
}
