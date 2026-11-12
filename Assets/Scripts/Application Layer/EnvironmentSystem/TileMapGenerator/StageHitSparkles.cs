using UnityEngine;

/// <summary>
/// 나무 타격 별가루. 그 씬에 파티클 시스템 하나만 두고, 나무를 때릴 때마다 나무 윗부분에서 작은 별가루 알갱이 몇 개가
/// 튀어 오르다 천천히 떨어지며 사라지게 한다. 기존 피격 이펙트는 그대로 두고 덧붙이기만 한다.
/// 광역 타격으로 한 프레임에 많은 나무가 맞아도 정해진 수만큼만 튀게 한다.
/// 렌더링만 바꾸고 게임 동작에는 영향이 없다. 해당 씬에 붙여 두므로 씬이 내려가면 같이 사라지고,
/// 다시 켤 때는 이전 것을 지우고 새로 만든다. 꺼진 곳에서는 아무것도 만들지 않는다(TryEmit은 아무 일도 하지 않는다).
/// </summary>
public class StageHitSparkles : MonoBehaviour
{
    private const float PixelsPerUnit = 32f;

    private static StageHitSparkles current;

    // //내부 의존성
    private HitSparkleSettings settings;
    private ParticleSystem sparkleSystem;
    private int lastEmitFrame = -1;
    private int burstsThisFrame;

    // // 퍼블릭 제어 메서드
    public static void Apply(HitSparkleSettings _settings, string _sceneName)
    {
        Remove();

        if (null == _settings || false == _settings.enabled || null == _settings.material)
        {
            return;
        }

        GameObject root = new GameObject("[StageHitSparkles]");
        UnityEngine.SceneManagement.Scene targetScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(_sceneName);
        if (targetScene.IsValid() && targetScene.isLoaded && root.scene != targetScene)
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, targetScene);
        }

        current = root.AddComponent<StageHitSparkles>();
        current.Setup(_settings);
    }

    public static void Remove()
    {
        if (null != current)
        {
            // 로그라이크 지형 전환으로 바뀔 때는 이미 떠 있는 알갱이가 수명대로 사라지게 둔다(StageAmbienceFade).
            if (false == StageAmbienceFade.TryRetire(current))
            {
                Destroy(current.gameObject);
            }
            current = null;
        }
    }

    /// <summary>
    /// 나무를 때린 자리(나무 윗부분 기준)에서 별가루를 튀게 한다. 꺼진 곳에서는 아무 일도 하지 않는다.
    /// </summary>
    public static void TryEmit(Vector3 _topPosition)
    {
        if (null != current)
        {
            current.EmitBurst(_topPosition);
        }
    }

    private void Setup(HitSparkleSettings _settings)
    {
        settings = _settings;

        sparkleSystem = gameObject.AddComponent<ParticleSystem>();
        sparkleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = sparkleSystem.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 10f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = Mathf.Max(16, _settings.maxParticles);
        main.gravityModifier = Mathf.Max(0f, _settings.gravity) / Mathf.Max(0.0001f, Physics.gravity.magnitude);
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        ParticleSystem.EmissionModule emission = sparkleSystem.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = sparkleSystem.shape;
        shape.enabled = false;

        // 튄 뒤 빠르게 느려져 떠 있다가 떨어진다.
        ParticleSystem.LimitVelocityOverLifetimeModule limit = sparkleSystem.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 0f;
        limit.dampen = _settings.dampen;

        // 바로 나타났다가 끝에서 옅어진다.
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = sparkleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fade);

        ParticleSystemRenderer particleRenderer = GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.sharedMaterial = _settings.material;
        particleRenderer.sortingLayerName = _settings.sortingLayerName;
        particleRenderer.sortingOrder = _settings.sortingOrder;
        particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        particleRenderer.minParticleSize = 0f;
        particleRenderer.maxParticleSize = 1f;

        sparkleSystem.Play();
    }

    private void EmitBurst(Vector3 _topPosition)
    {
        int frame = Time.frameCount;
        if (frame != lastEmitFrame)
        {
            lastEmitFrame = frame;
            burstsThisFrame = 0;
        }

        if (burstsThisFrame >= Mathf.Max(1, settings.maxBurstsPerFrame))
        {
            return;
        }
        burstsThisFrame++;

        int count = Random.Range(settings.burstCountRange.x, Mathf.Max(settings.burstCountRange.x, settings.burstCountRange.y) + 1);
        Vector2 origin = (Vector2)_topPosition + settings.emitOffset;
        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();

        for (int k = 0; k < count; k++)
        {
            Vector2 jitter = new Vector2(Random.Range(-1f, 1f) * settings.emitSpread.x, Random.Range(-1f, 1f) * settings.emitSpread.y);
            Vector2 direction = (Random.insideUnitCircle.normalized + Vector2.up * settings.upwardBias).normalized;
            float speed = Random.Range(settings.speedRange.x, settings.speedRange.y);
            int sizePixels = Random.Range(settings.sizePixelsRange.x, Mathf.Max(settings.sizePixelsRange.x, settings.sizePixelsRange.y) + 1);

            emitParams.position = new Vector3(origin.x + jitter.x, origin.y + jitter.y, 0f);
            emitParams.velocity = new Vector3(direction.x * speed, direction.y * speed, 0f);
            emitParams.startLifetime = Random.Range(settings.lifetimeRange.x, settings.lifetimeRange.y);
            emitParams.startSize = Mathf.Max(1, sizePixels) / PixelsPerUnit;
            emitParams.startColor = Color.Lerp(settings.colorA, settings.colorB, Random.value);
            sparkleSystem.Emit(emitParams, 1);
        }
    }

    private void OnDestroy()
    {
        if (current == this)
        {
            current = null;
        }
    }
}
