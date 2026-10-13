using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>진입 연출 1~2단계(디스토션 → 암전)와 암전이 걷히는 페이드인 수치. 기획서 6장 "디스토션 강도와 지속 시간"(TBD)의 자리다.</summary>
[Serializable]
public sealed class RoguelikeScreenTransitionSettings
{
    [Tooltip("디스토션이 최대 강도까지 차오르는 시간(초).")]
    public float distortSeconds = 0.7f;
    [Tooltip("디스토션 최대 강도: 화면이 출렁이는 폭(화면 크기 비율).")]
    [Range(0f, 0.2f)] public float distortStrength = 0.05f;
    [Tooltip("출렁임 물결 수(화면 세로 기준).")]
    public float distortWaves = 3f;
    [Tooltip("출렁임 속도(초당 위상).")]
    public float distortSpeed = 9f;
    [Tooltip("디스토션 끝에서 화면이 검게 뒤덮이는 시간(초). 디스토션 마지막 구간과 겹친다.")]
    public float blackoutSeconds = 0.35f;
    [Tooltip("암전 뒤 화면이 드러나는 시간(초).")]
    public float fadeInSeconds = 0.5f;
    [Tooltip("검은 화면을 최소 이만큼(초) 유지한 뒤 걷는다. 그 아래에서 지형 생성·HUD 내림 모션이 끝나 화면이 정돈된 뒤 드러나게 한다.")]
    public float minBlackHoldSeconds = 0.8f;
}

/// <summary>
/// 로그라이크 진입 화면 전환(기획서 4-1 1~2단계). 지금 화면(메인메뉴·결과 화면 - UI까지)을 그대로 떠서 출렁이게 하고(디스토션),
/// 그 위를 검은색으로 덮는다(암전). 검은 화면은 씬이 바뀌어도 남는다(메인메뉴 → 로그라이크 씬 로드 동안 덮어 둔다).
/// 로비가 준비되면 FadeFromBlack으로 걷는다.
///
/// 셰이더 없이 화면 사진을 격자 메쉬로 그려 꼭짓점을 흔든다 - 빌드에 따로 넣을 에셋이 없다.
/// 시간은 실제 시간으로 흐른다(일시정지·씬 로드와 무관).
/// </summary>
public sealed class RoguelikeScreenTransition : MonoBehaviour
{
    private const int SortingOrder = 32000;

    private static RoguelikeScreenTransition instance;

    private Canvas canvas;
    private DistortedScreenImage screenImage;
    private Image blackImage;
    private Texture2D capturedTexture;
    private bool bCovering;

    /// <summary>지금 화면이 검게(또는 디스토션으로) 덮여 있다.</summary>
    public static bool IsCovering => null != instance && instance.bCovering;

    public static RoguelikeScreenTransition Instance
    {
        get
        {
            if (null == instance) instance = Create();
            return instance;
        }
    }

    /// <summary>1~2단계: 지금 화면을 떠서 출렁이게 하고 검게 덮는다. 끝나면 화면 전체가 검은색이다.</summary>
    public IEnumerator DistortToBlack(RoguelikeScreenTransitionSettings _settings)
    {
        if (null == _settings) _settings = new RoguelikeScreenTransitionSettings();

        bCovering = true;
        canvas.enabled = true;

        // 지금 프레임이 다 그려진 뒤(UI 포함) 화면을 뜬다.
        yield return new WaitForEndOfFrame();
        ReleaseCapture();
        capturedTexture = ScreenCapture.CaptureScreenshotAsTexture();

        screenImage.texture = capturedTexture;
        screenImage.enabled = true;
        screenImage.SetWave(0f, _settings.distortWaves, 0f);
        SetBlackAlpha(0f);

        float distort = Mathf.Max(0.01f, _settings.distortSeconds);
        float black = Mathf.Clamp(_settings.blackoutSeconds, 0.01f, distort);
        float blackStart = distort - black;
        float elapsed = 0f;

        while (elapsed < distort)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / distort);
            screenImage.SetWave(_settings.distortStrength * t * t, _settings.distortWaves, elapsed * _settings.distortSpeed);
            SetBlackAlpha(Mathf.Clamp01((elapsed - blackStart) / black));
        }

        CoverBlack();
    }

    /// <summary>디스토션 없이 바로 검게 덮는다(이미 덮여 있으면 그대로).</summary>
    public void CoverBlack()
    {
        bCovering = true;
        canvas.enabled = true;
        SetBlackAlpha(1f);

        screenImage.enabled = false;
        screenImage.texture = null;
        ReleaseCapture();
    }

    /// <summary>검은 화면을 걷는다. 덮여 있지 않으면 바로 끝난다.</summary>
    public IEnumerator FadeFromBlack(float _seconds)
    {
        if (false == bCovering)
        {
            yield break;
        }

        CoverBlack();

        float duration = Mathf.Max(0.01f, _seconds);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            SetBlackAlpha(1f - Mathf.Clamp01(elapsed / duration));
        }

        Clear();
    }

    /// <summary>덮여 있으면 바로 치운다. 아직 한 번도 쓰지 않았으면 아무것도 만들지 않는다(본편 경로에서 불러도 비용이 없다).</summary>
    public static void ClearIfCovering()
    {
        if (null != instance && instance.bCovering) instance.Clear();
    }

    /// <summary>덮개를 바로 치운다(흐름이 끊길 때).</summary>
    public void Clear()
    {
        bCovering = false;
        SetBlackAlpha(0f);
        screenImage.enabled = false;
        screenImage.texture = null;
        ReleaseCapture();
        canvas.enabled = false;
    }

    // // 내부

    private static RoguelikeScreenTransition Create()
    {
        GameObject root = new GameObject("RoguelikeScreenTransition");
        DontDestroyOnLoad(root);

        RoguelikeScreenTransition transition = root.AddComponent<RoguelikeScreenTransition>();

        transition.canvas = root.AddComponent<Canvas>();
        transition.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        transition.canvas.sortingOrder = SortingOrder;
        transition.canvas.enabled = false;
        // 덮여 있는 동안 아래 UI(메인메뉴 버튼 등)가 눌리지 않게 막는다.
        root.AddComponent<GraphicRaycaster>();

        transition.screenImage = CreateFullScreen<DistortedScreenImage>(root.transform, "Screen");
        transition.screenImage.color = Color.white;
        transition.screenImage.raycastTarget = true;
        transition.screenImage.enabled = false;

        transition.blackImage = CreateFullScreen<Image>(root.transform, "Black");
        transition.blackImage.color = new Color(0f, 0f, 0f, 0f);
        transition.blackImage.raycastTarget = true;

        return transition;
    }

    private static T CreateFullScreen<T>(Transform _parent, string _name) where T : Graphic
    {
        GameObject go = new GameObject(_name, typeof(RectTransform));
        go.transform.SetParent(_parent, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return go.AddComponent<T>();
    }

    private void SetBlackAlpha(float _alpha)
    {
        if (null == blackImage) return;
        blackImage.color = new Color(0f, 0f, 0f, _alpha);
        blackImage.enabled = _alpha > 0f;
    }

    private void ReleaseCapture()
    {
        if (null == capturedTexture) return;
        Destroy(capturedTexture);
        capturedTexture = null;
    }

    private void OnDestroy()
    {
        ReleaseCapture();
        if (instance == this) instance = null;
    }
}
