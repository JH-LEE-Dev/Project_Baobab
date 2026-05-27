using System.Collections;
using UnityEngine;
using DG.Tweening;

public class OffroadContainerVComponent : MonoBehaviour
{
    // 외부 의존성
    [SerializeField] private float roofHeight = 1.0f;

    // 내부 의존성
    private SpriteRenderer spriteRenderer;
    private CustomSortable customSortable;
    private Animator anim;
    private Transform parentTransform;
    private float currentHeight = 0f;
    private Vector3 originalScale;
    private Quaternion originalRot;

    public readonly int bOpenHash = Animator.StringToHash("bOpen");

    public void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        customSortable = GetComponent<CustomSortable>();
        
        parentTransform = transform.parent != null ? transform.parent : transform;
        
        customSortable.Initialize(transform);
        customSortable.AddSpriteRenderer(spriteRenderer);
        anim = GetComponent<Animator>();

        originalScale = parentTransform.localScale;
        originalRot = parentTransform.localRotation;
    }

    public void LateUpdate()
    {
        // CustomSortable에게 현재 공중에 떠 있는 높이(arc + 지붕높이)를 전달하여 정렬 보정
        customSortable.SetHeight(currentHeight);
        customSortable.ManualLateUpdate();
    }

    public IEnumerator JumpSequence(Vector3 _targetPos, float _jumpHeight, float _duration, float _springFreq, float _springDamping)
    {
        Vector3 startPos = parentTransform.position;
        Vector3 initialScale = parentTransform.localScale;

        // 1. 포물선 점프 단계
        float jumpElapsed = 0f;
        while (jumpElapsed < _duration)
        {
            float t = jumpElapsed / _duration;

            // 수평 및 수직(포물선) 이동을 Transform이 직접 수행
            // Lerp를 통해 시작 지점(바닥)에서 목표 지점(지붕)으로 이동
            Vector3 groundLerpPos = Vector3.Lerp(startPos, _targetPos, t);
            float arc = Mathf.Sin(t * Mathf.PI) * _jumpHeight;
            parentTransform.position = groundLerpPos + new Vector3(0, arc, 0);

            // CustomSortable을 위한 Height 계산: 
            // 현재 지면으로부터 떠 있는 총 높이 = (지붕으로 올라가는 높이) + (점프 곡선 높이)
            float ascendingHeight = t * roofHeight;
            currentHeight = ascendingHeight + arc;

            // 공중에서의 쫀득한 스케일
            float stretch = Mathf.Sin(t * Mathf.PI) * 0.2f;
            parentTransform.localScale = initialScale + new Vector3(-stretch, stretch, 0);

            jumpElapsed += Time.deltaTime;
            yield return null;
        }

        // 2. 안착 단계 (연출 제거)
        parentTransform.position = _targetPos;
        currentHeight = roofHeight;
        parentTransform.localScale = initialScale;
    }

    public void Open()
    {
        if (anim.GetBool(bOpenHash)) return;

        parentTransform.DOKill(true);

        Sequence seq = DOTween.Sequence();

        // 1. 납작해짐 (Anticipation - 0.15초)
        seq.Append(parentTransform.DOScale(new Vector3(originalScale.x * 1.3f, originalScale.y * 0.5f, originalScale.z), 0.15f).SetEase(Ease.OutQuad));

        // 2. 튀어오르며 열리기 시작하는 지점
        seq.AppendCallback(() =>
        {
            anim.SetBool(bOpenHash, true);

            // 뒤뚱거림 (Z축 펀치 로테이션)
            parentTransform.DOPunchRotation(new Vector3(0f, 0f, 15f), 0.8f, 8, 1f);
        });

        // 3. 위로 뽀잉 솟구침 (0.15초)
        seq.Append(parentTransform.DOScale(new Vector3(originalScale.x * 0.8f, originalScale.y * 1.25f, originalScale.z), 0.15f).SetEase(Ease.OutQuad));

        // 4. 아래로 살짝 찌그러짐 (0.12초)
        seq.Append(parentTransform.DOScale(new Vector3(originalScale.x * 1.1f, originalScale.y * 0.9f, originalScale.z), 0.12f).SetEase(Ease.InOutQuad));

        // 5. 원래 크기로 복귀 (0.1초)
        seq.Append(parentTransform.DOScale(originalScale, 0.1f).SetEase(Ease.OutElastic));

        // 최종 원복 보장
        seq.OnComplete(() =>
        {
            parentTransform.localScale = originalScale;
            parentTransform.localRotation = originalRot;
        });
    }

    public void Close()
    {
        anim.SetBool(bOpenHash, false);
    }
}
