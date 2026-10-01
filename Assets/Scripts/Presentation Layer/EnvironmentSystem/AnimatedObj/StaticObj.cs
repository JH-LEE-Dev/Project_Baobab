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
    // 주의: static 필드 초기화(정적 생성자)로 만들면 안 된다. 타입이 MonoBehaviour 생성 중에 처음 쓰이면 그 시점에 정적 생성자가 돌고,
    // Unity는 그 문맥에서 MaterialPropertyBlock 생성을 금지해(CreateImpl 예외) 타입 초기화가 통째로 실패한다. Awake/Initialize에서 지연 생성한다.
    private static MaterialPropertyBlock sharedMpb;
    private static MaterialPropertyBlock SharedMpb => sharedMpb ??= new MaterialPropertyBlock();

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
