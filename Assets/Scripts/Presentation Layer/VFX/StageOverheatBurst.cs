using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 과열이 가득 차는 순간의 열기 물결. 캐릭터 발밑에서 불 고리가 바닥을 따라 퍼져 나가고,
/// 고리가 닿는 나무는 통째로 달아올랐다가 식는다.
/// 이 컴포넌트는 물결이 도는 동안 전역값 하나(_OverheatBurstWave: 진원·앞머리 거리·세기)만 매 프레임 채운다.
/// 불 고리(Custom/VFX/OverheatBurstRing)와 나무(OverheatBurstWave.hlsl)가 같은 값을 읽어 그리므로 서로 어긋나지 않는다.
/// 렌더링만 하고 게임 동작에는 영향이 없다(시간 배율도 건드리지 않는다). 처음 쓸 때 던전 씬에 만들어 두므로
/// 씬이 내려가면 같이 사라지고, 사라질 때 전역값을 0으로 돌린다. 로그라이크에서는 아무것도 하지 않는다.
/// </summary>
public class StageOverheatBurst : MonoBehaviour
{
    private const string MaterialResourcePath = "Stage4/M_OverheatBurstRing";
    private const string DungeonSceneName = "DungeonScene";

    // 물결이 사라지는 순간의 앞머리 거리(바닥 평면 거리). 기준 화면(가로 20)의 모서리까지 넘긴다.
    private const float ReachRadius = 18f;
    // 물결이 퍼지기 시작해 다 흩어질 때까지의 시간(초). 이 시간 내내 앞머리는 멈추지 않고 나아간다.
    private const float LifeSeconds = 1.15f;
    // 앞머리 움직임 중 처음에 터져 나가는 몫. 나머지 몫은 끝까지 같은 빠르기로 나아가서
    // 처음엔 빠르게 터져 나가고 끝에서도 처음 빠르기의 1/8쯤으로 계속 밀려 나간다(제자리에 서지 않는다).
    private const float BurstShare = 0.7f;
    // 이 비율(0~1)만큼 시간이 지나면 세기가 줄기 시작해 끝에서 0이 된다. 퍼지는 동안 고리가 조각나며 흩어진다.
    // 기준 화면에서는 고리가 시간의 절반쯤에 화면 밖으로 나가므로, 일찍 흩어지기 시작해야 화면 안에서 조각나는 모습이 보인다.
    private const float FadeStartRatio = 0.12f;

    // 바닥 이펙트 레이어(그림자 위, 나무·캐릭터 아래)
    private const string SortingLayerName = "ShockWave";
    private const int SortingOrder = 0;

    private static readonly int BurstWaveID = Shader.PropertyToID("_OverheatBurstWave");

    private static StageOverheatBurst current;
    // 머티리얼을 찾지 못했으면 다시 찾지 않는다.
    private static bool bMaterialMissing;

    // //내부 의존성
    private MeshRenderer ringRenderer;
    private Mesh ringMesh;
    private Vector2 center;
    private float elapsed;
    private bool bPlaying;

    // // 퍼블릭 제어 메서드
    /// <summary>진원(캐릭터 발밑)에서 열기 물결을 한 번 퍼뜨린다. 도는 중에 다시 부르면 새 진원에서 다시 시작한다.</summary>
    public static void Play(Vector3 _center)
    {
        if (GameModeContext.IsRoguelike || bMaterialMissing)
        {
            return;
        }

        if (null == current && false == Create())
        {
            return;
        }

        current.Begin(_center);
    }

    private static bool Create()
    {
        Material material = Resources.Load<Material>(MaterialResourcePath);
        if (null == material)
        {
            bMaterialMissing = true;
            Debug.LogError("[StageOverheatBurst] 머티리얼을 찾지 못했습니다: Resources/" + MaterialResourcePath);
            return false;
        }

        GameObject root = new GameObject("[StageOverheatBurst]");
        UnityEngine.SceneManagement.Scene targetScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(DungeonSceneName);
        if (targetScene.IsValid() && targetScene.isLoaded && root.scene != targetScene)
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, targetScene);
        }

        current = root.AddComponent<StageOverheatBurst>();
        current.Setup(material);
        return true;
    }

    private void Setup(Material _material)
    {
        // 물결이 닿는 범위를 덮는 쿼드 하나(앞머리 일그러짐까지 여유를 둔다). 아이소 화면에서는 세로가 절반으로 눌려 보이므로 높이는 절반이다.
        float halfWidth = ReachRadius + 1f;
        float halfHeight = halfWidth * 0.5f;

        ringMesh = new Mesh();
        ringMesh.name = "StageOverheatBurstRing";
        ringMesh.SetVertices(new[]
        {
            new Vector3(-halfWidth, -halfHeight, 0f),
            new Vector3(-halfWidth, halfHeight, 0f),
            new Vector3(halfWidth, halfHeight, 0f),
            new Vector3(halfWidth, -halfHeight, 0f),
        });
        ringMesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);

        gameObject.AddComponent<MeshFilter>().sharedMesh = ringMesh;

        ringRenderer = gameObject.AddComponent<MeshRenderer>();
        ringRenderer.sharedMaterial = _material;
        ringRenderer.sortingLayerName = SortingLayerName;
        ringRenderer.sortingOrder = SortingOrder;
        ringRenderer.shadowCastingMode = ShadowCastingMode.Off;
        ringRenderer.receiveShadows = false;
        ringRenderer.lightProbeUsage = LightProbeUsage.Off;
        ringRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        ringRenderer.enabled = false;
    }

    private void Begin(Vector3 _center)
    {
        center = _center;
        elapsed = 0f;
        bPlaying = true;

        transform.position = new Vector3(_center.x, _center.y, 0f);
        ringRenderer.enabled = true;
        ApplyWave();
    }

    private void Update()
    {
        if (false == bPlaying)
        {
            return;
        }

        elapsed += Time.deltaTime;
        if (elapsed >= LifeSeconds)
        {
            End();
            return;
        }

        ApplyWave();
    }

    private void ApplyWave()
    {
        float life = Mathf.Clamp01(elapsed / LifeSeconds);

        // 앞머리: 터지듯 빠르게 나가다 느려지지만, 끝까지 일정한 빠르기는 남겨 멈추지 않는다.
        float burst = 1f - (1f - life) * (1f - life) * (1f - life);
        float radius = ReachRadius * (BurstShare * burst + (1f - BurstShare) * life);

        // 세기: 앞머리가 나아가는 동안 줄어든다(다 퍼진 뒤 제자리에서 꺼지지 않는다). 고리 셰이더는 이 값으로 고리를 조각내 흩는다.
        float strength = 1f - Mathf.Clamp01((life - FadeStartRatio) / (1f - FadeStartRatio));

        Shader.SetGlobalVector(BurstWaveID, new Vector4(center.x, center.y, radius, strength));
    }

    private void End()
    {
        bPlaying = false;
        if (null != ringRenderer)
        {
            ringRenderer.enabled = false;
        }

        Shader.SetGlobalVector(BurstWaveID, Vector4.zero);
    }

    private void OnDestroy()
    {
        if (current == this)
        {
            current = null;
        }

        // 도는 중에 씬이 내려가도 나무가 달아오른 채로 남지 않게 한다.
        Shader.SetGlobalVector(BurstWaveID, Vector4.zero);

        if (null != ringMesh)
        {
            Destroy(ringMesh);
            ringMesh = null;
        }
    }
}
