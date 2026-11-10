using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 포자 안개를 그 씬에 하나 띄운다. 흰 스프라이트 한 장에 SporeHaze 머티리얼을 끼우고, 카메라가 그릴 때마다
/// 그 카메라 화면을 덮도록 옮긴다(안개 모양은 셰이더가 월드 좌표로 계산하므로 땅에 붙어 흘러간다).
/// 렌더링만 바꾸고 게임 동작에는 영향이 없다. 해당 씬에 붙여 두므로 씬이 내려가면 같이 사라지고,
/// 다시 켤 때는 이전 것을 지우고 새로 만든다. 꺼진 곳에서는 아무것도 만들지 않는다.
/// </summary>
public class StageSporeHaze : MonoBehaviour
{
    // 카메라 화면보다 넉넉히 덮는 배율(카메라 흔들림·한 프레임 늦은 이동에도 가장자리가 드러나지 않게)
    private const float ViewMargin = 1.25f;

    private static StageSporeHaze current;
    private static Sprite whiteSprite;

    // //내부 의존성
    private SpriteRenderer spriteRenderer;

    // // 퍼블릭 제어 메서드
    public static void Apply(SporeHazeSettings _settings, string _sceneName)
    {
        Remove();

        if (null == _settings || false == _settings.enabled || null == _settings.material)
        {
            return;
        }

        GameObject root = new GameObject("[StageSporeHaze]");
        UnityEngine.SceneManagement.Scene targetScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(_sceneName);
        if (targetScene.IsValid() && targetScene.isLoaded && root.scene != targetScene)
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, targetScene);
        }

        current = root.AddComponent<StageSporeHaze>();
        current.Setup(_settings);
    }

    public static void Remove()
    {
        if (null != current)
        {
            Destroy(current.gameObject);
            current = null;
        }
    }

    private void Setup(SporeHazeSettings _settings)
    {
        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetWhiteSprite();
        spriteRenderer.sharedMaterial = _settings.material;
        spriteRenderer.sortingLayerName = _settings.sortingLayerName;
        spriteRenderer.sortingOrder = _settings.sortingOrder;
        spriteRenderer.shadowCastingMode = ShadowCastingMode.Off;
        spriteRenderer.receiveShadows = false;

        Camera mainCamera = Camera.main;
        if (null != mainCamera)
        {
            FitToCamera(mainCamera);
        }
    }

    // 1x1 월드 크기의 흰 스프라이트(셰이더가 색을 정하므로 그림은 필요 없다). 한 번 만들어 계속 쓴다.
    private static Sprite GetWhiteSprite()
    {
        if (null == whiteSprite)
        {
            Texture2D texture = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
            whiteSprite.name = "SporeHazeWhite";
        }

        return whiteSprite;
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
    }

    private void HandleBeginCameraRendering(ScriptableRenderContext _context, Camera _camera)
    {
        if (CameraType.Game != _camera.cameraType || false == _camera.orthographic)
        {
            return;
        }

        FitToCamera(_camera);
    }

    private void FitToCamera(Camera _camera)
    {
        float height = _camera.orthographicSize * 2f * ViewMargin;
        float width = height * _camera.aspect;
        Vector3 cameraPosition = _camera.transform.position;
        transform.SetPositionAndRotation(new Vector3(cameraPosition.x, cameraPosition.y, 0f), Quaternion.identity);
        transform.localScale = new Vector3(width, height, 1f);
    }

    private void OnDestroy()
    {
        if (current == this)
        {
            current = null;
        }
    }
}
