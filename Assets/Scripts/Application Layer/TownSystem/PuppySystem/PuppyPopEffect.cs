using UnityEngine;

/// <summary>
/// 강아지/밥그릇을 상호작용했을 때의 "흰색 + 뽀잉" 연출.
/// 이 컴포넌트가 붙은 Transform(발밑 기준점)을 세로로 눌렀다 튀게 하고, 등록된 렌더러를 하얗게 번쩍인다.
/// 값과 곡선은 전리품 기둥의 성장 연출(LootDisplayObject.StartGrowthPop)과 같은 방식이다.
/// 번쩍임은 Custom-Sprite-Default 재질의 _FlashAmount를 MaterialPropertyBlock으로 올린다.
/// </summary>
public class PuppyPopEffect : MonoBehaviour
{
    private static readonly int FlashAmountID = Shader.PropertyToID("_FlashAmount");

    [SerializeField, Tooltip("완전히 흰색으로 유지하는 시간(초). 이후 flashDuration까지 원래 색으로 풀린다.")]
    private float flashHold = 0.05f;
    [SerializeField, Tooltip("흰색으로 번쩍였다가 원래 색으로 돌아오기까지의 시간(초).")]
    private float flashDuration = 0.3f;
    [SerializeField, Tooltip("뽀잉(눌렸다 튀며 출렁임) 전체 시간(초).")]
    private float popDuration = 0.45f;
    [SerializeField, Range(0f, 0.5f), Tooltip("처음 눌리는 세로 비율. 0.25면 세로가 75%까지 눌렸다가 튄다.")]
    private float popAmplitude = 0.25f;
    [SerializeField, Range(0f, 1f), Tooltip("세로 변화에 맞춰 가로가 반대로 변하는 비율(부피감).")]
    private float popWidthRatio = 0.6f;
    [SerializeField, Tooltip("초당 출렁임 횟수.")]
    private float popFrequency = 4f;
    [SerializeField, Tooltip("출렁임이 잦아드는 빠르기. 클수록 빨리 멈춘다.")]
    private float popDamping = 6f;

    [Header("Hop (짖을 때 콩 튀어오름, 번쩍임 없음)")]
    [SerializeField, Tooltip("튀어올랐다 내려오는 전체 시간(초).")]
    private float hopDuration = 0.22f;
    [SerializeField, Tooltip("튀어오르는 높이(월드).")]
    private float hopHeight = 0.08f;
    [SerializeField, Range(0f, 0.5f), Tooltip("튀어오를 때 세로로 늘어나는 비율.")]
    private float hopStretch = 0.12f;

    private SpriteRenderer[] flashRenderers;
    private MaterialPropertyBlock mpb;

    private Vector3 baseLocalScale = Vector3.one;
    private Vector3 baseLocalPosition = Vector3.zero;
    private float elapsed = 0f;
    private bool bPlaying = false;
    private bool bHop = false;

    public void Initialize(SpriteRenderer[] _flashRenderers)
    {
        flashRenderers = _flashRenderers;
        baseLocalScale = transform.localScale;
        baseLocalPosition = transform.localPosition;
        mpb = new MaterialPropertyBlock();
    }

    /// <summary>상호작용 연출: 흰색으로 번쩍이며 눌렸다 튄다.</summary>
    public void Play()
    {
        Restart(false);
    }

    /// <summary>짖을 때의 연출: 번쩍임 없이 살짝 늘어나며 콩 튀어오른다.</summary>
    public void PlayHop()
    {
        Restart(true);
    }

    private void Restart(bool _bHop)
    {
        if (true == bPlaying)
        {
            transform.localScale = baseLocalScale;
            transform.localPosition = baseLocalPosition;
            SetFlash(0f);
        }

        bHop = _bHop;
        elapsed = 0f;
        bPlaying = true;
    }

    public void Stop()
    {
        if (false == bPlaying) return;

        bPlaying = false;
        transform.localScale = baseLocalScale;
        transform.localPosition = baseLocalPosition;
        SetFlash(0f);
    }

    private void SetFlash(float _amount)
    {
        if (null == flashRenderers || null == mpb) return;

        for (int i = 0; i < flashRenderers.Length; i++)
        {
            SpriteRenderer _renderer = flashRenderers[i];
            if (null == _renderer) continue;

            _renderer.GetPropertyBlock(mpb);
            mpb.SetFloat(FlashAmountID, _amount);
            _renderer.SetPropertyBlock(mpb);
        }
    }

    private void LateUpdate()
    {
        if (false == bPlaying) return;

        if (true == bHop)
        {
            UpdateHop();
            return;
        }

        float _t = elapsed;
        if (Mathf.Max(popDuration, flashDuration) <= _t)
        {
            Stop();
            return;
        }

        float _flash = _t <= flashHold ? 1f : 1f - Mathf.Clamp01((_t - flashHold) / Mathf.Max(0.0001f, flashDuration - flashHold));
        SetFlash(_flash * _flash);

        // 세로로 눌린 채 시작해 감쇠 진동하며 1로 돌아온다. 끝에서 정확히 1이 되도록 남은 시간 비율을 곱한다.
        float _a = 0f;
        if (_t < popDuration)
        {
            float _remain = 1f - _t / popDuration;
            _a = -popAmplitude * Mathf.Exp(-popDamping * _t) * Mathf.Cos(2f * Mathf.PI * popFrequency * _t) * _remain;
        }

        transform.localScale = Vector3.Scale(baseLocalScale, new Vector3(1f - _a * popWidthRatio, 1f + _a, 1f));

        elapsed += Time.deltaTime;
    }

    private void UpdateHop()
    {
        if (elapsed >= hopDuration)
        {
            Stop();
            return;
        }

        float _wave = Mathf.Sin(Mathf.PI * elapsed / hopDuration);
        transform.localPosition = baseLocalPosition + new Vector3(0f, hopHeight * _wave, 0f);
        transform.localScale = Vector3.Scale(baseLocalScale, new Vector3(1f - hopStretch * 0.5f * _wave, 1f + hopStretch * _wave, 1f));

        elapsed += Time.deltaTime;
    }

    private void OnDisable()
    {
        Stop();
    }
}
