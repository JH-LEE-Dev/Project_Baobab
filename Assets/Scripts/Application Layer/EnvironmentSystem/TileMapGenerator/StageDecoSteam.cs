using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 데코 증기. 그 씬에 파티클 시스템 하나만 두고, 잠깐마다 화면 안 블룸 데코(불씨 바위 등) 한 곳에서
/// 김 덩어리 몇 개를 짧은 틈을 두고 연달아 뿜는다("푸쉬쉭"). 덩어리 하나 = 파티클 하나(정사각형 칸)이고,
/// 떠오르며 커지다 점점이 흩어지는 구름 모양은 머티리얼(DecoSteamPuff 셰이더)이 수명 비율을 받아 픽셀 단위로 그린다.
/// 자리는 화면 안 무작위 위치 몇 곳만 찍어 고르므로 데코 칸 목록을 만들거나 훑지 않는다. 화면에 데코가 안 보이면 그 차례는 건너뛴다.
/// 나무가 서 있어 데코를 지운 칸은 타일이 없으므로 고르지 않는다.
/// 뿜을 때 그 데코의 불빛이 잠깐 밝아지도록 데코 자리와 세기를 셰이더 전역값(_DecoSteamFlash, 4칸)으로 넘긴다
/// (블룸 데코 숨쉬기 머티리얼이 읽는다. glowFlashDuration이 0이면 넘기지 않는다).
/// 렌더링만 바꾸고 게임 동작에는 영향이 없다. 해당 씬에 붙여 두므로 씬이 내려가면 같이 사라지고,
/// 다시 켤 때는 이전 것을 지우고 새로 만든다. 꺼진 곳·로그라이크에서는 아무것도 만들지 않는다.
/// </summary>
public class StageDecoSteam : MonoBehaviour
{
    private const float PixelsPerUnit = 32f;

    // DecoSteamPuff 셰이더가 받는 정점 스트림: 위치, 색, UV + 수명 비율 + 고정 난수(TEXCOORD0), 크기 가로·세로(TEXCOORD1).
    internal static readonly List<ParticleSystemVertexStream> PuffVertexStreams = new List<ParticleSystemVertexStream>
    {
        ParticleSystemVertexStream.Position,
        ParticleSystemVertexStream.Color,
        ParticleSystemVertexStream.UV,
        ParticleSystemVertexStream.AgePercent,
        ParticleSystemVertexStream.StableRandomX,
        ParticleSystemVertexStream.SizeXY,
    };

    private struct Burst
    {
        public Vector3 origin;
        public int remaining;
        public float timer;
    }

    // 증기 뿜는 데코 불빛(BloomDecoBreath.hlsl의 _DecoSteamFlash와 같은 칸 수)
    private const int FlashSlotCount = 4;
    // 밝아지는 데 걸리는 시간 비율(나머지 동안 천천히 돌아온다)
    private const float FlashRiseRatio = 0.15f;
    private static readonly int DecoSteamFlashID = Shader.PropertyToID("_DecoSteamFlash");
    private static readonly Vector4[] EmptyFlashSlots = new Vector4[FlashSlotCount];

    private static StageDecoSteam current;

    // //내부 의존성
    private DecoSteamSettings settings;
    private Tilemap decoTilemap;
    private ParticleSystem steamSystem;
    private readonly List<Burst> bursts = new List<Burst>(4);
    private float nextBurstTimer;
    // 칸마다 xy = 데코 가운데(월드), z = 세기(0~1). 지난 시간은 따로 둔다.
    private readonly Vector4[] flashSlots = new Vector4[FlashSlotCount];
    private readonly float[] flashElapsed = new float[FlashSlotCount];
    private bool bFlashActive;

    // // 퍼블릭 제어 메서드
    public static void Apply(DecoSteamSettings _settings, Tilemap _decoTilemap, string _sceneName)
    {
        Remove();

        if (null == _settings || false == _settings.enabled || null == _settings.material || null == _decoTilemap
            || GameModeContext.IsRoguelike)
        {
            return;
        }

        GameObject root = new GameObject("[StageDecoSteam]");
        UnityEngine.SceneManagement.Scene targetScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(_sceneName);
        if (targetScene.IsValid() && targetScene.isLoaded && root.scene != targetScene)
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, targetScene);
        }

        current = root.AddComponent<StageDecoSteam>();
        current.Setup(_settings, _decoTilemap);
    }

    public static void Remove()
    {
        if (null != current)
        {
            Destroy(current.gameObject);
            current = null;
        }

        // 남은 불빛이 다음 스테이지로 넘어가지 않게 비운다.
        Shader.SetGlobalVectorArray(DecoSteamFlashID, EmptyFlashSlots);
    }

    private void Setup(DecoSteamSettings _settings, Tilemap _decoTilemap)
    {
        settings = _settings;
        decoTilemap = _decoTilemap;
        nextBurstTimer = RandomInterval();
        for (int i = 0; i < FlashSlotCount; i++)
        {
            flashElapsed[i] = float.MaxValue;
        }

        steamSystem = gameObject.AddComponent<ParticleSystem>();
        steamSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = steamSystem.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 10f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = Mathf.Max(8, _settings.maxParticles);
        main.startSize3D = true;
        main.gravityModifier = 0f;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        // 뿜은 뒤 공기 저항으로 느려지며 떠오른다(속도 제한은 두지 않고 저항만 쓴다).
        ParticleSystem.LimitVelocityOverLifetimeModule limitVelocity = steamSystem.limitVelocityOverLifetime;
        limitVelocity.enabled = _settings.drag > 0f;
        limitVelocity.limit = 1000f;
        limitVelocity.dampen = 0f;
        limitVelocity.drag = Mathf.Max(0f, _settings.drag);
        limitVelocity.multiplyDragByParticleSize = false;
        limitVelocity.multiplyDragByParticleVelocity = false;

        ParticleSystem.EmissionModule emission = steamSystem.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = steamSystem.shape;
        shape.enabled = false;

        // 끝무렵 전체가 살짝 옅어진다(커지고 흩어지는 건 셰이더가 그린다).
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = steamSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0.5f, 1f) });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fade);

        ParticleSystemRenderer particleRenderer = GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.sharedMaterial = _settings.material;
        particleRenderer.SetActiveVertexStreams(PuffVertexStreams);
        particleRenderer.sortingLayerName = _settings.sortingLayerName;
        particleRenderer.sortingOrder = _settings.sortingOrder;
        particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        particleRenderer.minParticleSize = 0f;
        particleRenderer.maxParticleSize = 1f;

        steamSystem.Play();
    }

    private void Update()
    {
        // Remove 뒤 실제로 지워지는 프레임 끝까지 옛 것이 한 번 더 돌 수 있다. 그때 불빛 전역값을 다시 쓰면
        // 새로 만든 쪽은 불빛이 없어 덮어쓰지 않으므로 옛 불빛이 새 맵에 남는다. 지금 것만 돈다.
        if (current != this || null == decoTilemap)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
        {
            return;
        }

        nextBurstTimer -= deltaTime;
        if (nextBurstTimer <= 0f)
        {
            nextBurstTimer = RandomInterval();
            if (TryPickOrigin(out Vector3 origin, out Vector3 decoCenter))
            {
                StartFlash(decoCenter);
                bursts.Add(new Burst
                {
                    origin = origin,
                    remaining = Mathf.Max(1, Random.Range(settings.puffsPerBurst.x, Mathf.Max(settings.puffsPerBurst.x, settings.puffsPerBurst.y) + 1)),
                    timer = 0f,
                });
            }
        }

        for (int i = bursts.Count - 1; i >= 0; i--)
        {
            Burst burst = bursts[i];
            burst.timer -= deltaTime;
            if (burst.timer <= 0f)
            {
                EmitPuff(burst.origin);
                burst.remaining--;
                burst.timer = settings.puffGap;
            }

            if (burst.remaining <= 0)
            {
                bursts.RemoveAt(i);
            }
            else
            {
                bursts[i] = burst;
            }
        }

        UpdateFlash(deltaTime);
    }

    // 뿜는 데코 불빛을 켠다. 빈 칸(없으면 가장 오래된 칸)을 쓴다.
    private void StartFlash(Vector3 _decoCenter)
    {
        if (settings.glowFlashDuration <= 0f)
        {
            return;
        }

        int slot = 0;
        for (int i = 0; i < FlashSlotCount; i++)
        {
            if (flashElapsed[i] >= settings.glowFlashDuration)
            {
                slot = i;
                break;
            }

            if (flashElapsed[i] > flashElapsed[slot])
            {
                slot = i;
            }
        }

        flashSlots[slot] = new Vector4(_decoCenter.x, _decoCenter.y, 0f, 0f);
        flashElapsed[slot] = 0f;
        bFlashActive = true;
    }

    // 켜진 불빛만 세기를 갱신해 전역값으로 넘긴다. 다 꺼진 프레임에 한 번 0으로 넘기고 그 뒤로는 아무것도 하지 않는다.
    private void UpdateFlash(float _deltaTime)
    {
        if (false == bFlashActive)
        {
            return;
        }

        float duration = Mathf.Max(0.01f, settings.glowFlashDuration);
        bool bAnyActive = false;
        for (int i = 0; i < FlashSlotCount; i++)
        {
            if (flashElapsed[i] >= duration)
            {
                flashSlots[i].z = 0f;
                continue;
            }

            flashElapsed[i] += _deltaTime;
            float t = Mathf.Clamp01(flashElapsed[i] / duration);
            float strength = t < FlashRiseRatio ? t / FlashRiseRatio : 1f - (t - FlashRiseRatio) / (1f - FlashRiseRatio);
            flashSlots[i].z = Mathf.Clamp01(strength);
            bAnyActive |= flashSlots[i].z > 0f;
        }

        Shader.SetGlobalVectorArray(DecoSteamFlashID, flashSlots);
        bFlashActive = bAnyActive;
    }

    private float RandomInterval()
    {
        return Mathf.Max(0.1f, Random.Range(settings.intervalRange.x, Mathf.Max(settings.intervalRange.x, settings.intervalRange.y)));
    }

    // 화면 안 무작위 자리를 몇 번 찍어 데코가 놓인 칸을 고른다. 뿜는 자리는 그 칸 가운데 + originOffset이다.
    private bool TryPickOrigin(out Vector3 _origin, out Vector3 _decoCenter)
    {
        _origin = Vector3.zero;
        _decoCenter = Vector3.zero;

        Camera mainCamera = Camera.main;
        if (null == mainCamera || false == mainCamera.orthographic)
        {
            return false;
        }

        float halfHeight = mainCamera.orthographicSize;
        float halfWidth = halfHeight * mainCamera.aspect;
        Vector3 center = mainCamera.transform.position;
        int attempts = Mathf.Max(1, settings.pickAttempts);

        for (int k = 0; k < attempts; k++)
        {
            Vector3 point = new Vector3(center.x + Random.Range(-halfWidth, halfWidth), center.y + Random.Range(-halfHeight, halfHeight), 0f);
            Vector3Int cell = decoTilemap.WorldToCell(point);
            if (false == decoTilemap.HasTile(cell))
            {
                continue;
            }

            Vector3 cellCenter = decoTilemap.GetCellCenterWorld(cell);
            _decoCenter = cellCenter;
            _origin = new Vector3(cellCenter.x + settings.originOffset.x, cellCenter.y + settings.originOffset.y, 0f);
            return true;
        }

        return false;
    }

    // 김 덩어리 하나. 칸 가운데가 파티클 위치이므로 덩어리 밑이 origin 근처에 오도록 칸 크기의 30%만큼 올려 놓는다.
    private void EmitPuff(Vector3 _origin)
    {
        int sizePixels = Mathf.Max(4, Random.Range(settings.sizePixelsRange.x, Mathf.Max(settings.sizePixelsRange.x, settings.sizePixelsRange.y) + 1));
        float size = sizePixels / PixelsPerUnit;

        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
        emitParams.position = _origin + new Vector3(
            Random.Range(-settings.originSpread.x, settings.originSpread.x),
            Random.Range(-settings.originSpread.y, settings.originSpread.y) + size * 0.3f,
            0f);
        emitParams.velocity = new Vector3(
            Random.Range(-settings.sideDrift, settings.sideDrift),
            Random.Range(settings.upSpeedRange.x, Mathf.Max(settings.upSpeedRange.x, settings.upSpeedRange.y)),
            0f);
        emitParams.startLifetime = Mathf.Max(0.05f, Random.Range(settings.lifetimeRange.x, Mathf.Max(settings.lifetimeRange.x, settings.lifetimeRange.y)));
        emitParams.startSize3D = new Vector3(size, size, 1f);
        emitParams.startColor = Color.Lerp(settings.colorA, settings.colorB, Random.value);
        steamSystem.Emit(emitParams, 1);
    }

    private void OnDestroy()
    {
        if (current == this)
        {
            current = null;
        }
    }
}
