using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 진입 연출 1~3단계(디스토션 링 → 암전 유지 → 페이드 아웃) 수치. 기획서 v2.2 9-1, 부록 A-1
/// "링 확산 약 0.45초 + 암전 유지 약 0.5초 + 페이드 아웃 약 0.3초, 디스토션부터 페이드 아웃 끝까지 1.25초 이내".
/// </summary>
[Serializable]
public sealed class RoguelikeScreenTransitionSettings
{
    [Tooltip("링이 화면 정중앙에서 화면 끝까지 퍼지는 시간(초). 링이 지나간 안쪽부터 검게 채워진다.")]
    public float ringSeconds = 0.45f;
    [Tooltip("링(일그러지는 띠)의 두께. 화면 대각선 반 길이 대비 비율.")]
    [Range(0.02f, 0.5f)] public float ringWidth = 0.16f;
    [Tooltip("링이 지나가는 자리에서 화면이 바깥으로 밀리는 최대 폭(왜곡 강도). 화면 대각선 반 길이 대비 비율.")]
    [Range(0f, 0.2f)] public float ringStrength = 0.05f;
    [Tooltip("검은 화면이 꽉 찬 뒤 유지하는 시간(초). 그 아래에서 지형을 깐다 - 지형 생성·씬 로드가 더 걸리면 그만큼 길어진다.")]
    public float blackHoldSeconds = 0.5f;
    [Tooltip("검은 화면이 걷히는 시간(초). 걷히기 시작하는 순간 입장 연출(바닥 타일 파도)이 함께 시작된다.")]
    public float fadeOutSeconds = 0.3f;
}

/// <summary>
/// 로그라이크 진입 화면 전환(기획서 v2.2 9-1 1~3단계). 지금 화면(메인메뉴·결과 화면 - UI까지)을 그대로 떠서
/// 화면 정중앙에서 링(고리) 형태 디스토션이 바깥으로 '퐁' 퍼지고, 링이 지나간 안쪽부터 검게 채운다.
/// 검은 화면은 씬이 바뀌어도 남는다(메인메뉴 → 로그라이크 씬 로드 동안 덮어 둔다). 로비가 준비되면 StartFadeFromBlack으로 걷는다.
///
/// 셰이더 없이 화면 사진을 격자 메쉬로 그려 링 자리 꼭짓점을 민다 - 빌드에 따로 넣을 에셋이 없다.
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
    // 화면이 검게 꽉 찬 순간(실제 시간). 암전 유지 시간은 이때부터 센다 - 메인메뉴에서 오면 씬 로드 시간도 유지 시간에 든다.
    private bool bFullBlack;
    private float fullBlackSince;
    private Coroutine fadeRoutine;

    /// <summary>지금 화면이 검게(또는 디스토션으로) 덮여 있다.</summary>
    public static bool IsCovering => null != instance && instance.bCovering;

    /// <summary>화면이 검게 꽉 찬 뒤 지난 시간(초, 실제 시간). 꽉 차 있지 않으면 0.</summary>
    public float SecondsSinceFullyBlack => bFullBlack ? Time.realtimeSinceStartup - fullBlackSince : 0f;

    public static RoguelikeScreenTransition Instance
    {
        get
        {
            if (null == instance) instance = Create();
            return instance;
        }
    }

    /// <summary>1단계: 지금 화면을 떠서 정중앙에서 링 디스토션을 퍼뜨리고, 링이 지나간 안쪽부터 검게 채운다. 끝나면 화면 전체가 검은색이다.</summary>
    public IEnumerator DistortToBlack(RoguelikeScreenTransitionSettings _settings)
    {
        if (null == _settings) _settings = new RoguelikeScreenTransitionSettings();

        StopFade();
        bCovering = true;
        bFullBlack = false;
        canvas.enabled = true;

        // 지금 프레임이 다 그려진 뒤(UI 포함) 화면을 뜬다.
        yield return new WaitForEndOfFrame();
        ReleaseCapture();
        capturedTexture = ScreenCapture.CaptureScreenshotAsTexture();

        screenImage.texture = capturedTexture;
        screenImage.enabled = true;
        screenImage.SetRing(0f, _settings.ringWidth, _settings.ringStrength);
        SetBlackAlpha(0f);

        // 링 안쪽 가장자리가 화면 모서리를 지나야 화면이 다 검다.
        float reach = 1f + _settings.ringWidth * 0.5f;
        float duration = Mathf.Max(0.01f, _settings.ringSeconds);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;

            // 충격파처럼 처음에 확 퍼지고 끝에서 잦아든다.
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);
            screenImage.SetRing(reach * eased, _settings.ringWidth, _settings.ringStrength);
        }

        CoverBlack();
    }

    /// <summary>디스토션 없이 바로 검게 덮는다(이미 꽉 차 있으면 그대로 - 암전 유지 시간을 다시 세지 않는다).</summary>
    public void CoverBlack()
    {
        StopFade();
        bCovering = true;
        canvas.enabled = true;
        SetBlackAlpha(1f);

        if (false == bFullBlack)
        {
            bFullBlack = true;
            fullBlackSince = Time.realtimeSinceStartup;
        }

        screenImage.enabled = false;
        screenImage.texture = null;
        ReleaseCapture();
    }

    /// <summary>
    /// 3단계: 검은 화면을 _seconds 동안 걷는다. 기다리지 않고 바로 돌아온다 - 걷히기 시작하는 순간 입장 연출이 함께 시작되어야 한다.
    /// 덮여 있지 않으면 아무것도 하지 않는다. 걷히는 동안 Clear·CoverBlack·DistortToBlack이 불리면 거기서 멈춘다.
    /// </summary>
    public void StartFadeFromBlack(float _seconds)
    {
        if (false == bCovering) return;

        CoverBlack();
        fadeRoutine = StartCoroutine(FadeFromBlackRoutine(_seconds));
    }

    private IEnumerator FadeFromBlackRoutine(float _seconds)
    {
        bFullBlack = false;

        float duration = Mathf.Max(0.01f, _seconds);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            SetBlackAlpha(1f - Mathf.Clamp01(elapsed / duration));
        }

        fadeRoutine = null;
        Clear();
    }

    private void StopFade()
    {
        if (null == fadeRoutine) return;

        StopCoroutine(fadeRoutine);
        fadeRoutine = null;
    }

    /// <summary>덮여 있으면 바로 치운다. 아직 한 번도 쓰지 않았으면 아무것도 만들지 않는다(본편 경로에서 불러도 비용이 없다).</summary>
    public static void ClearIfCovering()
    {
        if (null != instance && instance.bCovering) instance.Clear();
    }

    /// <summary>덮개를 바로 치운다(흐름이 끊길 때).</summary>
    public void Clear()
    {
        StopFade();
        bCovering = false;
        bFullBlack = false;
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
