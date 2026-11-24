#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 월드 HUD 연출 플레이 검증 보조(WorldHUD_Plan.md 10-5). 플레이 중 "PP Main Camera"를 RenderTexture로 그려 게임 프레임마다 한 장씩 모으고, 정해 둔 프레임에 동작을 실행한다.
/// 에디터가 포커스를 잃으면 게임 뷰가 다시 그려지지 않으므로 EditorApplication.update에서 QueuePlayerLoopUpdate로 프레임을 민다.
/// 저장은 게임 프레임 안(Application.onBeforeRender, LateUpdate 뒤)에서 메모리로만 모으고 PNG 인코딩은 Stop에서 한꺼번에 해서, 프레임 시간이 길어져 HUD 시계(프레임당 최대 1/20초)가 건너뛰는 일을 줄인다.
/// 사용: Start(폴더, 접두사, 프레임 수, 가로, 세로) 뒤 Schedule(프레임 오프셋, 동작)으로 동작을 건다. 끝나면 Stop이 PNG(접두사_NNN.png)와 프레임 로그(접두사_log.txt: 번호|게임 프레임|unscaledDeltaTime)를 쓴다.
/// </summary>
public static class RoguelikeWorldHudCapture
{
    private sealed class Step
    {
        public int frame;
        public Action action;
    }

    private static readonly List<Step> steps = new List<Step>(16);
    private static readonly List<byte[]> frames = new List<byte[]>(128);
    private static readonly StringBuilder logText = new StringBuilder(2048);
    private static string outputDir;
    private static string prefix;
    private static int framesLeft;
    private static int startFrame;
    private static int lastFrame = -1;
    private static int width = 1920;
    private static int height = 1080;
    private static bool bRegistered;
    private static Camera captureCamera;
    private static bool bScreenMode;
    private static MonoBehaviour screenHost;
    private static Coroutine screenRoutine;
    private static RenderTexture renderTexture;
    private static Texture2D readTexture;

    /// <summary>지금부터 게임 프레임마다 _frames장을 모아 Stop에서 _dir/_prefix_NNN.png로 쓴다(_frames가 0이면 동작 실행만). _width x _height는 렌더 크기(1920x1080 또는 640x360).</summary>
    public static void Start(string _dir, string _prefix, int _frames, int _width, int _height, bool _bScreen = false)
    {
        bScreenMode = _bScreen;
        outputDir = _dir;
        prefix = _prefix;
        framesLeft = _frames;
        startFrame = Time.frameCount;
        lastFrame = -1;
        steps.Clear();
        frames.Clear();
        logText.Length = 0;
        Directory.CreateDirectory(_dir);
        SetSize(_width, _height);

        // 화면 모드: 에디터 어셈블리 MonoBehaviour는 AddComponent가 안 되므로, 살아 있는 런타임 MonoBehaviour(RoguelikeSystem)에서 코루틴을 돌려 프레임 끝마다 캡처한다.
        if (null != screenRoutine)
        {
            if (null != screenHost) screenHost.StopCoroutine(screenRoutine);
            screenRoutine = null;
            screenHost = null;
        }

        if (true == _bScreen)
        {
            screenHost = UnityEngine.Object.FindAnyObjectByType<RoguelikeSystem>();
            if (null != screenHost) screenRoutine = screenHost.StartCoroutine(ScreenLoop());
        }

        if (false == bRegistered)
        {
            bRegistered = true;
            EditorApplication.update += OnUpdate;
            Application.onBeforeRender += OnBeforeRender;
        }
    }

    /// <summary>Start 이후 _frameOffset번째 프레임에 _action을 실행한다.</summary>
    public static void Schedule(int _frameOffset, Action _action)
    {
        steps.Add(new Step { frame = _frameOffset, action = _action });
    }

    /// <summary>지금 프레임에서 _frames프레임 뒤에 _action을 실행한다(대기 중 상태 확인 반복용).</summary>
    public static void ScheduleFromNow(int _frames, Action _action)
    {
        steps.Add(new Step { frame = Time.frameCount - startFrame + _frames, action = _action });
    }

    public static bool IsBusy => 0 < framesLeft || 0 < steps.Count;

    /// <summary>모은 프레임을 PNG로 쓰고 등록을 푼다.</summary>
    public static void Stop()
    {
        framesLeft = 0;
        steps.Clear();

        if (true == bRegistered)
        {
            bRegistered = false;
            EditorApplication.update -= OnUpdate;
            Application.onBeforeRender -= OnBeforeRender;
        }

        if (0 < frames.Count && null != outputDir)
        {
            Texture2D encode = new Texture2D(width, height, TextureFormat.RGB24, false);

            for (int i = 0; frames.Count > i; i++)
            {
                encode.LoadRawTextureData(frames[i]);
                File.WriteAllBytes(Path.Combine(outputDir, prefix + "_" + i.ToString("000") + ".png"), encode.EncodeToPNG());
            }

            UnityEngine.Object.DestroyImmediate(encode);
            File.WriteAllText(Path.Combine(outputDir, prefix + "_log.txt"), logText.ToString());
        }

        frames.Clear();
        ReleaseTextures();

        if (null != screenRoutine)
        {
            if (null != screenHost) screenHost.StopCoroutine(screenRoutine);
            screenRoutine = null;
            screenHost = null;
        }

        bScreenMode = false;
    }

    /// <summary>지금 화면을 한 장 바로 저장한다(_width x _height).</summary>
    public static void Snapshot(string _path, int _width, int _height)
    {
        SetSize(_width, _height);
        if (false == Render()) return;

        File.WriteAllBytes(_path, readTexture.EncodeToPNG());
    }

    private static void SetSize(int _width, int _height)
    {
        if (_width == width && _height == height) return;

        width = _width;
        height = _height;
        ReleaseTextures();
    }

    private static void ReleaseTextures()
    {
        if (null != renderTexture)
        {
            renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(renderTexture);
            renderTexture = null;
        }

        if (null != readTexture)
        {
            UnityEngine.Object.DestroyImmediate(readTexture);
            readTexture = null;
        }
    }

    private static void OnUpdate()
    {
        if (false == EditorApplication.isPlaying)
        {
            Stop();
            return;
        }

        EditorApplication.QueuePlayerLoopUpdate();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    /// <summary>게임 프레임마다(LateUpdate 뒤, 렌더 직전) 한 번 불린다. 연출이 프레임마다 정확히 한 장씩 모이도록 여기서 동작 실행과 캡처를 한다.</summary>
    private static void OnBeforeRender()
    {
        if (false == EditorApplication.isPlaying) return;

        int frame = Time.frameCount;
        if (frame == lastFrame) return;
        lastFrame = frame;

        int offset = frame - startFrame;

        for (int i = 0; steps.Count > i; i++)
        {
            if (offset < steps[i].frame) continue;

            Action action = steps[i].action;
            steps.RemoveAt(i);
            i--;
            action();
        }

        if (0 < framesLeft && false == bScreenMode && true == Render())
        {
            frames.Add(readTexture.GetRawTextureData());
            logText.Append(frames.Count - 1).Append('|').Append(frame).Append('|').Append(Time.unscaledDeltaTime.ToString("0.0000")).Append('\n');
            framesLeft--;
        }

        if (0 >= framesLeft && 0 >= steps.Count) Stop();
    }

    private static IEnumerator ScreenLoop()
    {
        WaitForEndOfFrame wait = new WaitForEndOfFrame();

        while (true)
        {
            yield return wait;
            CaptureScreenFrame();
        }
    }

    /// <summary>
    /// 화면 모드(_bScreen)에서 프레임 끝(WaitForEndOfFrame)마다 불린다. 게임 뷰(오버레이 UI까지 합쳐진 화면)를 ScreenCapture로 받아 3칸마다 한 점씩(640x360 = 1920x1080의 1/3 픽셀 퍼펙트) 뽑아 모은다.
    /// </summary>
    public static void CaptureScreenFrame()
    {
        if (false == bScreenMode || 0 >= framesLeft) return;

        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
        if (null == shot) return;

        int step = shot.width / width;
        if (1 > step) step = 1;

        Color32[] source = shot.GetPixels32();
        byte[] raw = new byte[width * height * 3];
        for (int y = 0; height > y; y++)
        {
            for (int x = 0; width > x; x++)
            {
                Color32 c = source[(y * step + step / 2) * shot.width + x * step + step / 2];
                int o = (y * width + x) * 3;
                raw[o] = c.r;
                raw[o + 1] = c.g;
                raw[o + 2] = c.b;
            }
        }

        UnityEngine.Object.DestroyImmediate(shot);
        frames.Add(raw);
        logText.Append(frames.Count - 1).Append('|').Append(Time.frameCount).Append('|').Append(Time.unscaledDeltaTime.ToString("0.0000")).Append('\n');
        framesLeft--;
    }

    /// <summary>카메라를 renderTexture로 그려 readTexture에 읽어 온다.</summary>
    private static bool Render()
    {
        if (null == captureCamera)
        {
            Camera[] cameras = Camera.allCameras;
            for (int i = 0; cameras.Length > i; i++)
            {
                if ("PP Main Camera" == cameras[i].name) captureCamera = cameras[i];
            }

            if (null == captureCamera) return false;
        }

        if (null == renderTexture)
        {
            renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            renderTexture.filterMode = FilterMode.Point;
            readTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
        }

        RenderTexture previous = captureCamera.targetTexture;
        captureCamera.targetTexture = renderTexture;
        captureCamera.Render();
        captureCamera.targetTexture = previous;

        RenderTexture.active = renderTexture;
        readTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        RenderTexture.active = null;
        return true;
    }
}
#endif
