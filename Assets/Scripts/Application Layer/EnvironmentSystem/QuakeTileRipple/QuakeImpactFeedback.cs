using PresentationLayer.VFX;
using UnityEngine;

/// <summary>
/// 지진 물결이 퍼지기 시작하는 순간부터 화면 끝에 닿은 뒤까지의 "손맛" 연출(EnvironmentSystem이 돌린다). 게임 판정은 건드리지 않는다.
///
/// [히트스톱] 터지는 순간 게임 시간을 hitStopSeconds(실제 초) 동안 hitStopTimeScale로 늦춘다. 시간 배율이 1일 때만 걸고,
///   끝날 때는 배율이 아직 히트스톱 값일 때만 1로 되돌린다 - 그 사이 일시정지(0)·슬로우 모션이 배율을 바꿨으면 그쪽 값을 존중한다.
///   일시정지 중(0)에 시간이 다 되면 풀릴 때까지 기다렸다가, 풀린 배율이 히트스톱 값(창이 열기 전 값을 되돌린 경우)이면 그때 1로 되돌린다.
/// [섬광] 같은 순간 화면을 주황빛으로 번쩍인다(PostProcessSettingsApplier.PlayQuakeFlash, 실제 시간).
/// [여진] 앞머리가 피해 범위 끝에 닿고 aftershockDelay 뒤 카메라를 한 번 더 찍고 흔들며 진동을 낸다(게임 시간).
/// [충격파 왜곡] 앞머리(게임 시간 × 앞머리 속도)를 따라 화면이 고리 모양으로 일그러진다. 화면을 덮는 쿼드 하나를
///   Objects 다음 소팅 레이어(FlyingItem) 맨 앞 순서에 그려, 캡처된 화면(Objects까지)만 왜곡한다. 그리는 동안만 화면 캡처 사용자로 등록한다.
/// </summary>
public sealed class QuakeImpactFeedback
{
    // 충격파 쿼드를 그리는 소팅 레이어·순서. 화면 캡처(_CameraSortingLayerTexture)가 Objects까지라 바로 다음 레이어의 맨 앞에 둔다.
    private const string ShockwaveSortingLayer = "FlyingItem";
    private const int ShockwaveSortingOrder = -30000;
    // 쿼드를 카메라 화면보다 이만큼(월드) 넓게 깐다(흔들림·확대로 가장자리가 비지 않게).
    private const float ShockwaveViewMargin = 2f;
    // 고리가 퍼지기 시작할 때 차오르는 시간(초)과, 피해 범위 끝에서 잦아드는 구간(범위 대비 비율).
    private const float ShockwaveFadeInSeconds = 0.06f;
    private const float ShockwaveFadeOutStart = 0.65f;
    // 앞머리가 피해 범위 끝으로 갈수록 왜곡 세기가 이 비율까지 줄어든다.
    private const float ShockwaveFarStrength = 0.55f;
    // 여진 카메라 찍기가 잦아드는 시간(초).
    private const float AftershockKickSeconds = 0.25f;
    // 흔들림이 앞머리를 따라갈 때, 앞머리가 피해 범위 끝에 닿은 뒤 더 이어지는 시간(초).
    private const float FollowFrontTailSeconds = 0.15f;

    private static readonly int CenterId = Shader.PropertyToID("_Center");
    private static readonly int RadiusId = Shader.PropertyToID("_Radius");
    private static readonly int WidthId = Shader.PropertyToID("_Width");
    private static readonly int StrengthId = Shader.PropertyToID("_Strength");
    private static readonly int FadeId = Shader.PropertyToID("_Fade");

    private QuakeTileRippleSettings settings;
    private Transform parent;

    private bool bPlaying;
    private float elapsed;
    private Vector2 center;
    private float strikeRadius;
    private bool bAftershockDone;

    private bool bHitStopActive;
    private float hitStopRemaining;

    private GameObject shockwaveObject;
    private MeshRenderer shockwaveRenderer;
    private MaterialPropertyBlock shockwaveBlock;
    private bool bShockwaveVisible;

    public void Initialize(QuakeTileRippleSettings _settings, Transform _parent)
    {
        settings = _settings;
        parent = _parent;
    }

    /// <summary>앞머리를 따라가는 흔들림 시간(초). 꺼져 있으면 cameraShakeSeconds 그대로.</summary>
    public static float ShakeSeconds(QuakeTileRippleSettings _settings, float _strikeRadius)
    {
        if (false == _settings.cameraShakeFollowFront || _strikeRadius <= 0f) return _settings.cameraShakeSeconds;
        return Mathf.Max(_settings.cameraShakeSeconds, _strikeRadius / Mathf.Max(0.1f, _settings.frontSpeed) + FollowFrontTailSeconds);
    }

    /// <summary>물결이 퍼지기 시작하는 프레임에 부른다. _strikeRadius는 피해 범위(바닥 평면 거리, 0이면 물결이 닿는 거리).</summary>
    public void Play(Vector3 _center, float _strikeRadius)
    {
        if (null == settings) return;

        bPlaying = true;
        elapsed = 0f;
        center = _center;
        strikeRadius = _strikeRadius > 0f ? _strikeRadius : Mathf.Max(0.1f, settings.reachRadius);
        bAftershockDone = false;

        BeginHitStop();
        PostProcessSettingsApplier.Instance?.PlayQuakeFlash(settings.flashExposure, settings.flashTint, settings.flashInSeconds, settings.flashOutSeconds);
    }

    /// <summary>매 프레임 부른다(일시정지 중에도). _deltaTime은 게임 시간, _unscaledDeltaTime은 실제 시간.</summary>
    public void Tick(float _deltaTime, float _unscaledDeltaTime)
    {
        TickHitStop(_unscaledDeltaTime);
        if (false == bPlaying) return;

        elapsed += _deltaTime;
        float sweepSeconds = strikeRadius / Mathf.Max(0.1f, settings.frontSpeed);

        if (false == bAftershockDone && elapsed >= sweepSeconds + settings.aftershockDelay)
        {
            bAftershockDone = true;
            PlayAftershock();
        }

        UpdateShockwave();

        if (bAftershockDone && false == bShockwaveVisible) bPlaying = false;
    }

    /// <summary>도중에 끝낸다(새 맵·꺼짐). 히트스톱이 걸려 있으면 되돌리고 왜곡 쿼드를 숨긴다.</summary>
    public void Stop()
    {
        bPlaying = false;
        if (bHitStopActive)
        {
            bHitStopActive = false;
            if (Mathf.Approximately(Time.timeScale, settings.hitStopTimeScale)) Time.timeScale = 1f;
        }
        SetShockwaveVisible(false);
    }

    /// <summary>쿼드 오브젝트까지 지운다(EnvironmentSystem 해제).</summary>
    public void Release()
    {
        Stop();
        if (null != shockwaveObject)
        {
            MeshFilter filter = shockwaveObject.GetComponent<MeshFilter>();
            if (null != filter && null != filter.sharedMesh) Object.Destroy(filter.sharedMesh);
            Object.Destroy(shockwaveObject);
        }
        shockwaveObject = null;
        shockwaveRenderer = null;
    }

    private void BeginHitStop()
    {
        if (settings.hitStopSeconds <= 0f || settings.hitStopTimeScale >= 1f) return;
        // 일시정지·슬로우 모션 중이면 걸지 않는다(되돌릴 때 그쪽 배율을 덮어쓰게 된다).
        if (false == Mathf.Approximately(Time.timeScale, 1f)) return;

        Time.timeScale = settings.hitStopTimeScale;
        bHitStopActive = true;
        hitStopRemaining = settings.hitStopSeconds;
    }

    private void TickHitStop(float _unscaledDeltaTime)
    {
        if (false == bHitStopActive) return;

        hitStopRemaining -= _unscaledDeltaTime;
        if (hitStopRemaining > 0f) return;

        float scale = Time.timeScale;
        if (Mathf.Approximately(scale, settings.hitStopTimeScale))
        {
            Time.timeScale = 1f;
            bHitStopActive = false;
        }
        else if (scale > 0f)
        {
            // 다른 곳(일시정지 해제 등)이 이미 배율을 정했다.
            bHitStopActive = false;
        }
        // 0(일시정지 중)이면 풀릴 때까지 기다린다.
    }

    private void PlayAftershock()
    {
        if (settings.aftershockShakeIntensity <= 0f && settings.aftershockKickDistance <= 0f) return;

        CameraMoveController.Instance?.ShakeCameraKick(Vector3.down, settings.aftershockKickDistance, AftershockKickSeconds,
            settings.aftershockShakeIntensity, settings.aftershockShakeSeconds);
        Rumble.Play(EHapticEvent.QuakeAftershock);
    }

    private void UpdateShockwave()
    {
        float radius = elapsed * Mathf.Max(0.1f, settings.frontSpeed);
        Camera camera = Camera.main;
        bool bWanted = null != settings.shockwaveMaterial && settings.shockwaveStrength > 0f && null != camera && camera.orthographic
                       && radius < strikeRadius + settings.shockwaveWidth * 2f;
        if (false == bWanted)
        {
            SetShockwaveVisible(false);
            return;
        }

        if (false == EnsureShockwaveObject()) return;

        float ratio = Mathf.Clamp01(radius / strikeRadius);
        float fade = Mathf.Clamp01(elapsed / ShockwaveFadeInSeconds) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ShockwaveFadeOutStart, 1f, ratio)));
        if (fade <= 0f)
        {
            SetShockwaveVisible(false);
            return;
        }

        // 카메라 화면(+여유)을 덮는다. 카메라가 캐릭터를 따라 움직여도 매 프레임 다시 맞춘다.
        float halfHeight = camera.orthographicSize + ShockwaveViewMargin;
        float halfWidth = camera.orthographicSize * camera.aspect + ShockwaveViewMargin;
        Vector3 view = camera.transform.position;
        Transform quad = shockwaveObject.transform;
        quad.position = new Vector3(view.x, view.y, 0f);
        quad.localScale = new Vector3(halfWidth * 2f, halfHeight * 2f, 1f);

        shockwaveBlock.SetVector(CenterId, new Vector4(center.x, center.y, 0f, 0f));
        shockwaveBlock.SetFloat(RadiusId, radius);
        shockwaveBlock.SetFloat(WidthId, settings.shockwaveWidth);
        shockwaveBlock.SetFloat(StrengthId, settings.shockwaveStrength * Mathf.Lerp(1f, ShockwaveFarStrength, ratio));
        shockwaveBlock.SetFloat(FadeId, fade);
        shockwaveRenderer.SetPropertyBlock(shockwaveBlock);

        SetShockwaveVisible(true);
    }

    private bool EnsureShockwaveObject()
    {
        if (null != shockwaveObject)
        {
            if (shockwaveRenderer.sharedMaterial != settings.shockwaveMaterial) shockwaveRenderer.sharedMaterial = settings.shockwaveMaterial;
            return true;
        }

        shockwaveObject = new GameObject("QuakeShockwave");
        // 월드 좌표로 둔다(부모 이동·회전과 무관하게 카메라 화면에 맞춘다).
        if (null != parent) shockwaveObject.transform.SetParent(parent, false);
        shockwaveObject.SetActive(false);

        MeshFilter filter = shockwaveObject.AddComponent<MeshFilter>();
        filter.sharedMesh = BuildQuadMesh();
        shockwaveRenderer = shockwaveObject.AddComponent<MeshRenderer>();
        shockwaveRenderer.sharedMaterial = settings.shockwaveMaterial;
        shockwaveRenderer.sortingLayerName = ShockwaveSortingLayer;
        shockwaveRenderer.sortingOrder = ShockwaveSortingOrder;
        shockwaveRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shockwaveRenderer.receiveShadows = false;
        shockwaveBlock = new MaterialPropertyBlock();
        bShockwaveVisible = false;
        return true;
    }

    private void SetShockwaveVisible(bool _bVisible)
    {
        if (_bVisible == bShockwaveVisible) return;
        if (_bVisible && null == shockwaveObject) return;

        bShockwaveVisible = _bVisible;
        if (null != shockwaveObject) shockwaveObject.SetActive(_bVisible);

        if (_bVisible) SortingLayerTextureGate.Acquire();
        else SortingLayerTextureGate.Release();
    }

    private static Mesh BuildQuadMesh()
    {
        Mesh mesh = new Mesh { name = "QuakeShockwaveQuad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
        };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(1f, 1f, 1f));
        return mesh;
    }
}
