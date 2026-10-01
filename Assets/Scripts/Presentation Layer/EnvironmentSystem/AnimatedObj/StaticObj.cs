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

    private bool bInitialize = false;
    // Get→Set은 블록 내용을 복사해 쓰므로 인스턴스마다 새로 만들지 않고 하나를 공유한다(메인 스레드 단일 실행).
    private static readonly MaterialPropertyBlock SharedMpb = new MaterialPropertyBlock();

    public void Initialize()
    {
        customSortable = GetComponent<CustomSortable>();
        if (customSortable != null)
        {
            customSortable.Initialize(transform);
            customSortable.AddSpriteRenderer(sr);
        }

        MaterialPropertyBlock mpb = SharedMpb;
        sr.GetPropertyBlock(mpb);
        mpb.SetFloat(HDRIntensityID, hdrIntensity);
        sr.SetPropertyBlock(mpb);

        bInitialize = true;
    }

    public void Awake()
    {
        if (bInitialize == false)
        {
            customSortable = GetComponent<CustomSortable>();
            if (customSortable != null)
            {
                customSortable.Initialize(transform);
                customSortable.AddSpriteRenderer(sr);
            }

            MaterialPropertyBlock mpb = SharedMpb;
            sr.GetPropertyBlock(mpb);
            mpb.SetFloat(HDRIntensityID, hdrIntensity);
            sr.SetPropertyBlock(mpb);

            bInitialize = true;
            
            if (customSortable != null)
            {
                customSortable.ManualLateUpdate();
            }
        }
    }

    public void SetSortingOrder()
    {
        if (customSortable != null)
            customSortable.ManualLateUpdate();
    }
}
