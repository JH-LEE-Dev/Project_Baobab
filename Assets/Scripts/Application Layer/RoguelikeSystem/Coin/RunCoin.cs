using System;
using UnityEngine;

/// <summary>
/// 바닥에 흩뿌려지는 로그라이크 코인 한 개. 움직임만 담당하고 값(골드)은 RunCoinController가 들고 있다.
/// 외형(스프라이트·반짝임·흡수 궤적)은 이펙트 담당이 프리팹과 이 클래스의 연출 훅을 다듬는다.
/// </summary>
public class RunCoin : MonoBehaviour
{
    private enum EState
    {
        Idle,
        Scattering,
        Grounded,
        Absorbing,
    }

    [SerializeField] private SpriteRenderer[] renderers;
    [Tooltip("흩뿌려질 때 걸리는 시간(초).")]
    [SerializeField] private float scatterDuration = 0.35f;
    [Tooltip("흩뿌려질 때 포물선 높이.")]
    [SerializeField] private float scatterHeight = 0.6f;
    [Tooltip("흡수될 때 처음 속도와 가속도.")]
    [SerializeField] private float absorbStartSpeed = 4f;
    [SerializeField] private float absorbAcceleration = 40f;
    [SerializeField] private float absorbArriveDistance = 0.15f;

    private EState state = EState.Idle;
    private Vector3 scatterFrom;
    private Vector3 scatterTo;
    private float timer;
    private float absorbSpeed;
    private Transform absorbTarget;
    private Action<RunCoin> arrivedCallback;

    public long Value { get; private set; }

    public void Launch(Vector3 _from, Vector3 _to, long _value, float _alpha)
    {
        Value = _value;
        scatterFrom = _from;
        scatterTo = _to;
        timer = 0f;
        state = EState.Scattering;
        transform.position = _from;
        SetAlpha(_alpha);
    }

    public void SetAlpha(float _alpha)
    {
        if (null == renderers) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (null == renderers[i]) continue;
            Color c = renderers[i].color;
            c.a = _alpha;
            renderers[i].color = c;
        }
    }

    public void BeginAbsorb(Transform _target, Action<RunCoin> _onArrived)
    {
        absorbTarget = _target;
        arrivedCallback = _onArrived;
        absorbSpeed = absorbStartSpeed;

        // 흩뿌려지는 중이었다면 그 자리에서 바로 흡수로 넘어간다.
        state = EState.Absorbing;
    }

    public void ResetCoin()
    {
        state = EState.Idle;
        absorbTarget = null;
        arrivedCallback = null;
        Value = 0;
    }

    private void Update()
    {
        switch (state)
        {
            case EState.Scattering:
                UpdateScatter();
                break;
            case EState.Absorbing:
                UpdateAbsorb();
                break;
        }
    }

    private void UpdateScatter()
    {
        timer += Time.deltaTime;
        float t = scatterDuration > 0f ? Mathf.Clamp01(timer / scatterDuration) : 1f;

        Vector3 pos = Vector3.Lerp(scatterFrom, scatterTo, t);
        pos.y += Mathf.Sin(t * Mathf.PI) * scatterHeight;
        transform.position = pos;

        if (t >= 1f) state = EState.Grounded;
    }

    private void UpdateAbsorb()
    {
        if (null == absorbTarget)
        {
            Arrive();
            return;
        }

        absorbSpeed += absorbAcceleration * Time.deltaTime;

        Vector3 targetPos = absorbTarget.position;
        Vector3 pos = Vector3.MoveTowards(transform.position, targetPos, absorbSpeed * Time.deltaTime);
        transform.position = pos;

        if ((targetPos - pos).sqrMagnitude <= absorbArriveDistance * absorbArriveDistance)
        {
            Arrive();
        }
    }

    private void Arrive()
    {
        state = EState.Idle;
        Action<RunCoin> callback = arrivedCallback;
        arrivedCallback = null;
        callback?.Invoke(this);
    }
}
