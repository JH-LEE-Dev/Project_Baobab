using UnityEngine;

/// <summary>
/// 흐르는 구름 그림자. 화면을 덮는 판 하나(StageCloudShadow 셰이더)를 정해진 정렬 자리에 두고, 그 앞에 그려진 땅·물·나무에
/// 그림자 색을 곱해 큰 구름 그림자 덩어리가 천천히 지나가게 한다. 구름 모양은 월드 좌표로 계산하므로 판은 카메라만 따라다닌다.
/// 렌더링만 바꾸고 게임 동작에는 영향이 없다. 해당 씬에 붙여 두므로 씬이 내려가면 같이 사라지고,
/// 다시 켤 때는 이전 것을 지우고 새로 만든다. 꺼진 곳·로그라이크에서는 아무것도 만들지 않는다.
/// </summary>
public class StageCloudShadows : MonoBehaviour
{
    private static readonly int CloudAID = Shader.PropertyToID("_CloudA");
    private static readonly int CloudBID = Shader.PropertyToID("_CloudB");
    private static readonly int CloudColorID = Shader.PropertyToID("_CloudColor");

    private static StageCloudShadows current;

    // //내부 의존성
    private CloudShadowSettings settings;
    private Material materialInstance;
    private Mesh quadMesh;

    // // 퍼블릭 제어 메서드
    public static void Apply(CloudShadowSettings _settings, string _sceneName)
    {
        Remove();

        // 화면을 따라다니는 판이라 플레이 중에만 만든다(에디터 맵 미리보기에서는 만들지 않는다).
        if (null == _settings || false == _settings.enabled || null == _settings.material || GameModeContext.IsRoguelike
            || false == Application.isPlaying)
        {
            return;
        }

        GameObject root = new GameObject("[StageCloudShadows]");
        UnityEngine.SceneManagement.Scene targetScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(_sceneName);
        if (targetScene.IsValid() && targetScene.isLoaded && root.scene != targetScene)
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, targetScene);
        }

        current = root.AddComponent<StageCloudShadows>();
        current.Setup(_settings);
    }

    public static void Remove()
    {
        if (null != current)
        {
            current.enabled = false;
            if (Application.isPlaying)
            {
                Destroy(current.gameObject);
            }
            else
            {
                DestroyImmediate(current.gameObject);
            }
            current = null;
        }
    }

    private void Setup(CloudShadowSettings _settings)
    {
        settings = _settings;

        Vector2 dir = _settings.direction.sqrMagnitude > 0.0001f ? _settings.direction.normalized : Vector2.right;
        materialInstance = new Material(_settings.material);
        materialInstance.SetVector(CloudAID, new Vector4(dir.x, dir.y, _settings.speed, Mathf.Max(0.5f, _settings.scale)));
        materialInstance.SetVector(CloudBID, new Vector4(Mathf.Clamp01(_settings.coverage), Mathf.Max(0.005f, _settings.softness), Mathf.Max(1, _settings.bands), Mathf.Clamp01(_settings.ditherWidth)));
        materialInstance.SetColor(CloudColorID, _settings.shadowColor);

        quadMesh = new Mesh();
        quadMesh.name = "CloudShadowQuad";
        quadMesh.hideFlags = HideFlags.DontSave;
        quadMesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
        quadMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        quadMesh.bounds = new Bounds(Vector3.zero, new Vector3(1f, 1f, 0.01f));

        gameObject.AddComponent<MeshFilter>().sharedMesh = quadMesh;
        MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = materialInstance;
        meshRenderer.sortingLayerName = _settings.sortingLayerName;
        meshRenderer.sortingOrder = _settings.sortingOrder;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        FollowCamera();
    }

    private void LateUpdate()
    {
        FollowCamera();
    }

    // 판을 카메라 화면(가장자리 여유 포함)에 맞춘다. 구름 모양은 월드 좌표라 판이 움직여도 그림자는 제자리다.
    private void FollowCamera()
    {
        Camera cam = Camera.main;
        if (null == cam || false == cam.orthographic)
        {
            return;
        }

        float height = (cam.orthographicSize + settings.viewMargin) * 2f;
        float width = (cam.orthographicSize * cam.aspect + settings.viewMargin) * 2f;
        Vector3 c = cam.transform.position;
        transform.SetPositionAndRotation(new Vector3(c.x, c.y, 0f), Quaternion.identity);
        transform.localScale = new Vector3(width, height, 1f);
    }

    private void OnDestroy()
    {
        if (null != materialInstance)
        {
            Destroy(materialInstance);
        }
        if (null != quadMesh)
        {
            Destroy(quadMesh);
        }
        if (current == this)
        {
            current = null;
        }
    }
}
