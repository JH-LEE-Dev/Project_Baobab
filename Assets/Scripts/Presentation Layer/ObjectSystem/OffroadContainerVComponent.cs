using System.Collections;
using UnityEngine;

public class OffroadContainerVComponent : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private CustomSortable customSortable;
    
    // 차량 지붕 높이 설정 (32*32 2:1 타일 2개 높이 = 약 32픽셀 = 1.0 unit 가정)
    [SerializeField] private float roofHeight = 1.0f;
    private float currentHeight = 0f;

    public void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        customSortable = GetComponent<CustomSortable>();
        customSortable.Initialize(transform);
        customSortable.AddSpriteRenderer(spriteRenderer);
    }

    public void LateUpdate()
    {
        // CustomSortable에게 현재 공중에 떠 있는 높이(arc + 지붕높이)를 전달하여 정렬 보정
        customSortable.SetHeight(currentHeight);
        customSortable.ManualLateUpdate();
    }

    public IEnumerator JumpSequence(Vector3 _targetPos, float _jumpHeight, float _duration, float _springFreq, float _springDamping)
    {
        Vector3 startPos = transform.position;
        Vector3 initialScale = transform.localScale;
        
        // 1. 포물선 점프 단계
        float jumpElapsed = 0f;
        while (jumpElapsed < _duration)
        {
            float t = jumpElapsed / _duration;
            
            // 수평 및 수직(포물선) 이동을 Transform이 직접 수행
            // Lerp를 통해 시작 지점(바닥)에서 목표 지점(지붕)으로 이동
            Vector3 groundLerpPos = Vector3.Lerp(startPos, _targetPos, t);
            float arc = Mathf.Sin(t * Mathf.PI) * _jumpHeight;
            transform.position = groundLerpPos + new Vector3(0, arc, 0);
            
            // CustomSortable을 위한 Height 계산: 
            // 현재 지면으로부터 떠 있는 총 높이 = (지붕으로 올라가는 높이) + (점프 곡선 높이)
            float ascendingHeight = t * roofHeight;
            currentHeight = ascendingHeight + arc;
            
            // 공중에서의 쫀득한 스케일
            float stretch = Mathf.Sin(t * Mathf.PI) * 0.2f;
            transform.localScale = initialScale + new Vector3(-stretch, stretch, 0);

            jumpElapsed += Time.deltaTime;
            yield return null;
        }

        // 2. 안착 단계 (연출 제거)
        transform.position = _targetPos;
        currentHeight = roofHeight;
        transform.localScale = initialScale;
    }
}
