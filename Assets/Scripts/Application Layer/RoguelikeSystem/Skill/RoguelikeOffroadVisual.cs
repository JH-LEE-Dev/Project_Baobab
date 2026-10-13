using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 오프로드 카드의 차량 외형(기획서 3-3 "본편 오프로드 비주얼을 그대로 사용한다. 필드 끝 면 타일 위에 랜덤하게 플래시되며
/// 소환되고, 끝 면에서 정면 방향으로 돌진"). 본편 차량(OffRoadVehicle.prefab)의 차체·내부·바퀴·그림자 그림과 바퀴 애니메이터를 쓴다.
///   예열: 진행 방향을 바라보고, 본편 반짝임(_FlashAmount)과 같은 하얀 플래시가 깜빡이며 시동음(OffroadNonEdit)이 난다. 바퀴는 멈춰 있다.
///   돌진: 바퀴가 돈다. 이동은 RoguelikeSkillController가 한다.
/// 정렬은 본편 CustomSortable과 같은 규칙(-y × 100)으로 매 프레임 맞춘다.
/// </summary>
public class RoguelikeOffroadVisual : MonoBehaviour
{
    private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

    [SerializeField] private Transform flipRoot;
    [Tooltip("차량 그림이 오른쪽을 보고 있는지. 진행 방향이 반대면 좌우를 뒤집는다.")]
    [SerializeField] private bool bSpriteFacesRight = true;
    [SerializeField] private SpriteRenderer[] flashRenderers;
    [SerializeField] private Animator wheelAnimator;
    [SerializeField] private SortingGroup sortingGroup;
    [Tooltip("예열 동안 플래시가 깜빡이는 횟수.")]
    [SerializeField] private int warmupFlashCount = 2;
    [Tooltip("돌진 중 바퀴 애니메이션 속도(본편은 차량 속도 × 0.2).")]
    [SerializeField] private float dashWheelSpeed = 3f;

    private MaterialPropertyBlock propertyBlock;
    private float warmupStartTime;
    private float warmupDuration;
    private bool bWarming;

    /// <summary>필드 끝에 소환된다. _seconds 동안 플래시가 깜빡인 뒤 BeginDash로 돌진한다.</summary>
    public void BeginWarmup(Vector3 _from, Vector3 _to, float _seconds)
    {
        transform.position = _from;
        Face(_to - _from);

        warmupStartTime = Time.time;
        warmupDuration = Mathf.Max(0.01f, _seconds);
        bWarming = true;

        if (null != wheelAnimator) wheelAnimator.speed = 0f;
        UpdateSorting();
        Sound.Play(SoundID.OffroadNonEdit, _from);
    }

    public void BeginDash()
    {
        bWarming = false;
        SetFlash(0f);
        if (null != wheelAnimator) wheelAnimator.speed = dashWheelSpeed;
    }

    private void Face(Vector3 _direction)
    {
        if (null == flipRoot || Mathf.Abs(_direction.x) < 0.0001f) return;

        bool bRight = _direction.x > 0f;
        Vector3 scale = flipRoot.localScale;
        scale.x = Mathf.Abs(scale.x) * (bRight == bSpriteFacesRight ? 1f : -1f);
        flipRoot.localScale = scale;
    }

    private void LateUpdate()
    {
        UpdateSorting();

        if (false == bWarming) return;

        // 깜빡임마다 1에서 0으로 빠르게 빠지는 플래시(본편 ShinyRoutine과 같은 하얀 번쩍임).
        float t = Mathf.Clamp01((Time.time - warmupStartTime) / warmupDuration);
        float phase = t * Mathf.Max(1, warmupFlashCount);
        float local = phase - Mathf.Floor(phase);
        SetFlash(t >= 1f ? 0f : 1f - local * local);
    }

    private void UpdateSorting()
    {
        if (null != sortingGroup) sortingGroup.sortingOrder = -Mathf.RoundToInt(transform.position.y * 100f);
    }

    private void SetFlash(float _amount)
    {
        if (null == flashRenderers) return;
        if (null == propertyBlock) propertyBlock = new MaterialPropertyBlock();

        for (int i = 0; i < flashRenderers.Length; i++)
        {
            SpriteRenderer renderer = flashRenderers[i];
            if (null == renderer) continue;

            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(FlashAmountId, _amount);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void OnDisable()
    {
        bWarming = false;
        SetFlash(0f);
    }
}
