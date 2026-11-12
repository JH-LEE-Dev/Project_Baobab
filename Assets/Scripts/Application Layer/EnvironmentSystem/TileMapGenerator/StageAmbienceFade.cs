using UnityEngine;

/// <summary>
/// 스테이지 분위기 연출 전체를 서서히 끄고 켠다(렌더링 전용, 로그라이크 지형 전환에서만 쓴다).
/// 본편은 이 클래스를 쓰지 않으므로 보이는 정도가 늘 1이고, 셰이더 전역값(_StageAmbienceFadeOut)도 늘 0이라 연출이 그대로다.
///
/// 로그라이크 지형 전환 순서(RoguelikeSystem.Flow)
/// 1. 새 지형을 깔기 전에 BeginCapture: 맵 생성기가 연출을 바꾸는 일(전역값·머티리얼·지도·파티클)을 바로 하지 않고 모아 둔다.
///    그동안 화면에는 직전 지형의 연출이 그대로 남는다.
/// 2. 깔고 나서 EndCapture.
/// 3. 직전 연출을 서서히 끈다(SetVisibility 1 → 0).
/// 4. 다 꺼진 때 RunPendingSwap: 모아 둔 일을 한꺼번에 해 새 지형의 연출로 바꾼다. 이때 지워지는 파티클은 남은 알갱이가
///    수명대로 사라질 때까지 둔다(TryRetire).
/// 5. 새 연출을 켠다. 파도를 따라 섬 중심에서 번져 켜지게 원(SetReveal)을 넓히고, 화면을 다 덮으면 원을 걷는다(ClearReveal).
/// 씬이 바뀌면(Discard) 모아 둔 일을 버리고 보이는 정도를 1로 되돌린다.
/// </summary>
public static class StageAmbienceFade
{
    private static readonly int FadeOutID = Shader.PropertyToID("_StageAmbienceFadeOut");
    private static readonly int RevealID = Shader.PropertyToID("_StageAmbienceReveal");

    private static System.Action pendingSwap;

    /// <summary>연출이 보이는 정도(1 = 그대로, 0 = 꺼짐). 파티클 연출은 새로 띄우는 양에 곱한다.</summary>
    public static float Visibility { get; private set; } = 1f;

    /// <summary>맵 생성기가 연출 바꾸기를 모아 두는 중인지.</summary>
    public static bool IsCapturing { get; private set; }

    /// <summary>모아 둔 연출 바꾸기를 하는 중인지(이때 지워지는 파티클은 남은 알갱이가 사라질 때까지 둔다).</summary>
    public static bool IsSwapping { get; private set; }

    public static bool HasPendingSwap => null != pendingSwap;

    // 새 연출이 켜진 원(StageAmbienceFade.hlsl의 _StageAmbienceReveal과 같다). 폭이 0이면 원을 쓰지 않는다.
    private static Vector2 revealCenter;
    private static float revealRadius;
    private static float revealBand;

    /// <summary>새 연출이 원을 따라 번져 켜지는 중인지(SetReveal ~ ClearReveal).</summary>
    public static bool IsRevealing => revealBand > 0f;

    /// <summary>
    /// 화면 전체로 친 보이는 정도. 원으로 켜는 중이면 _reach(화면 끝까지의 바닥 평면 거리) 대비 원이 덮은 넓이 비율을 곱한다.
    /// 화면 하나에 한 값만 걸 수 있는 연출(색 보정 볼륨)이 쓴다.
    /// </summary>
    public static float ScreenVisibility { get; private set; } = 1f;

    // // 퍼블릭 제어 메서드
    public static void SetVisibility(float _visibility)
    {
        Visibility = Mathf.Clamp01(_visibility);
        Shader.SetGlobalFloat(FadeOutID, 1f - Visibility);
        if (revealBand <= 0f)
        {
            ScreenVisibility = Visibility;
        }
    }

    /// <summary>
    /// 새 연출을 _center에서 바닥 평면 거리 _radius 안에서만 보이게 한다(가장자리 _band 폭은 옅어진다).
    /// _reach는 화면 끝까지의 거리로, 화면 전체 보이는 정도(ScreenVisibility)를 셈한다.
    /// </summary>
    public static void SetReveal(Vector2 _center, float _radius, float _band, float _reach)
    {
        revealCenter = _center;
        revealRadius = Mathf.Max(0f, _radius);
        revealBand = Mathf.Max(0.01f, _band);
        Shader.SetGlobalVector(RevealID, new Vector4(revealCenter.x, revealCenter.y, revealRadius, revealBand));

        float covered = Mathf.Clamp01(revealRadius / Mathf.Max(0.01f, _reach));
        ScreenVisibility = Visibility * covered * covered;
    }

    /// <summary>원을 걷는다(새 연출이 어디서나 보인다).</summary>
    public static void ClearReveal()
    {
        revealBand = 0f;
        revealRadius = 0f;
        Shader.SetGlobalVector(RevealID, Vector4.zero);
        ScreenVisibility = Visibility;
    }

    /// <summary>
    /// 파티클 연출이 _position에 새 알갱이를 띄워도 되는지. 원으로 켜는 중이면 원 안(가장자리 폭의 가운데까지)만 띄운다.
    /// 원을 쓰지 않으면(본편은 늘) 늘 true다.
    /// </summary>
    public static bool ShowsAt(Vector3 _position)
    {
        if (revealBand <= 0f)
        {
            return true;
        }

        Vector2 d = new Vector2(_position.x - revealCenter.x, (_position.y - revealCenter.y) * 2f);
        return d.magnitude <= revealRadius - revealBand * 0.5f;
    }

    /// <summary>
    /// 연출 바꾸기를 모으기 시작한다. 전에 모아 두고 아직 하지 않은 일이 있으면 먼저 해 둔다(순서가 뒤바뀌지 않게).
    /// </summary>
    public static void BeginCapture()
    {
        RunPendingSwap();
        IsCapturing = true;
    }

    public static void EndCapture()
    {
        IsCapturing = false;
    }

    /// <summary>모으는 중이면 _swap을 모아 두고(true), 아니면 아무것도 하지 않는다(false, 부른 쪽이 바로 한다).</summary>
    public static bool TryDefer(System.Action _swap)
    {
        if (false == IsCapturing || null == _swap)
        {
            return false;
        }

        pendingSwap += _swap;
        return true;
    }

    /// <summary>모아 둔 연출 바꾸기를 모은 순서대로 한다.</summary>
    public static void RunPendingSwap()
    {
        System.Action swap = pendingSwap;
        pendingSwap = null;
        if (null == swap)
        {
            return;
        }

        IsSwapping = true;
        try
        {
            swap();
        }
        finally
        {
            IsSwapping = false;
        }
    }

    /// <summary>모아 둔 일을 버리고 연출을 그대로 보이게 되돌린다(씬이 바뀔 때).</summary>
    public static void Discard()
    {
        pendingSwap = null;
        IsCapturing = false;
        IsSwapping = false;
        ClearReveal();
        SetVisibility(1f);
    }

    /// <summary>
    /// 연출 바꾸기 중이고 _owner 아래 파티클이 아직 남아 있으면, 더 띄우지 않게 _owner를 끄고 남은 알갱이가 다 사라지면
    /// 오브젝트를 지우게 맡긴다(true). 아니면 false(부른 쪽이 바로 지운다).
    /// </summary>
    public static bool TryRetire(MonoBehaviour _owner)
    {
        if (false == IsSwapping || null == _owner || false == Application.isPlaying)
        {
            return false;
        }

        ParticleSystem[] systems = _owner.GetComponentsInChildren<ParticleSystem>();
        bool bAlive = false;
        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i].particleCount > 0)
            {
                bAlive = true;
                break;
            }
        }

        if (false == bAlive)
        {
            return false;
        }

        _owner.enabled = false;
        _owner.gameObject.AddComponent<StageAmbienceRetiredParticles>().Setup(systems);
        return true;
    }
}
