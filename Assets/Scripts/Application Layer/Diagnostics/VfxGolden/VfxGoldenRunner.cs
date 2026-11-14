// ─────────────────────────────────────────────────────────────────────────────
// VFX 골든 캡처(결과 불변 검증) 도구. 에디터 전용이라 빌드에는 컴파일되지 않습니다.
//
// [무엇을 하는가]
// VfxGoldenCases에 등록된 케이스(이펙트 하나씩)를 빈 전용 씬에서 고정 dt(1/60초)·고정 난수로 재생하고,
// 프레임마다 아래 세 가지 해시를 기록합니다. 최적화 전에 "기록"하고, 최적화 뒤에 "비교"합니다.
//   - render : 캡처 카메라 RT(ARGBHalf, 월드 텍셀 1개 = 2x2 픽셀)의 바이트. 판정 기준입니다.
//   - mesh   : 켜진 렌더러가 쓰는 메쉬의 인덱스·정점 바이트. 같은 색 구간 병합처럼 정점만 바뀌고
//              그림은 같은 최적화도 있으므로 참고값(경고)입니다.
//   - state  : 켜진 렌더러의 정렬·트랜스폼·스프라이트·머티리얼. 다른 물체와의 앞뒤 순서는 render로는
//              잡히지 않으므로 이것도 같아야 합니다(경고로 표시하고 사람이 판단).
//
// [쓰는 법] Tools > VFX 검증 > 기준 기록 / 비교 (VfxGoldenMenu.cs)
// 결과는 Library/VfxGolden/<라벨>/ 에 쌓입니다(깃에 들어가지 않습니다).
//
// [결정성 전제]
// - Time.captureDeltaTime으로 deltaTime·time을 고정합니다.
// - 일부 이펙트는 Time.frameCount로 시드를 만듭니다. 그래서 케이스마다 시작 프레임 번호를 고정합니다.
//   시작 프레임은 "필터와 무관하게" 전체 목록 순서로 정하므로, 일부만 돌려도 같은 프레임에 시작합니다.
//   케이스는 반드시 목록 끝에 추가하세요. 중간에 끼우면 뒤 케이스의 시작 프레임이 밀려 기준을 다시 기록해야 합니다.
// - 케이스 시작 직전에 UnityEngine.Random.InitState(케이스 시드)를 부르고, 파티클 자동 시드는 끕니다.
// - 정렬 키(레이어·순서·깊이)가 완전히 같은 렌더러끼리는 그리기 순서가 실행마다 바뀔 수 있습니다(게임에서도 동률).
//   그래서 기준 기록 뒤 "기준 보강(merge)"을 두어 번 돌려, 정점·상태는 같은데 그림만 다른 프레임의 그림을
//   허용 그림으로 덧붙입니다(기준 파일의 render 칸이 "해시|해시"가 됨). 비교는 허용 그림 중 하나와 같으면 통과입니다.
//   동률이 확인된 케이스(허용 그림이 하나라도 있는 케이스)는 조합이 많아 다 모을 수 없으므로, 그림 입력(정점·렌더러 상태·
//   파티클 하나하나)이 전부 같은 프레임은 그림이 달라도 통과로 봅니다(state 해시에 파티클 위치·회전·크기·색·남은 수명 포함).
// ─────────────────────────────────────────────────────────────────────────────
#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Diagnostics.VfxGolden
{
    [DefaultExecutionOrder(-32000)]
    public partial class VfxGoldenRunner : MonoBehaviour
    {
        // SessionState 키 (에디터 메뉴가 쓰고 러너가 읽음)
        public const string KeyMode = "VfxGolden.Mode";            // "record" / "compare" / "merge" / "perf"
        public const string KeyLabel = "VfxGolden.Label";          // 이번 실행 결과 폴더 이름
        public const string KeyBaseline = "VfxGolden.Baseline";    // 비교 대상 라벨
        public const string KeyFilter = "VfxGolden.Filter";        // 쉼표로 구분한 케이스 이름 일부(비우면 전체)
        public const string KeySaveImages = "VfxGolden.SaveImages";
        public const string KeyRestoreStartScene = "VfxGolden.RestoreStartScene";
        public const string ScenePath = "Assets/Scripts/Editor/VfxGolden/VfxGoldenScene.unity";

        private const float FixedDelta = 1.0f / 60.0f;
        private const int FirstCaseFrame = 60;
        private const int GapFrames = 6;
        private const int TexelsPerUnit = 32;
        private const int PixelsPerTexel = 2;
        private const int MaxRtSize = 2048;
        private const int MaxImagesPerCase = 3;
        private static readonly Color ClearColor = new Color(0.3f, 0.45f, 0.6f, 0.5f);

        // 최적화 전 코드에서 실행마다 그림만 달라지는(정점·상태는 같은) 것이 확인된 케이스. 동률이 드물게 나와
        // 기준 보강 두어 번으로는 허용 그림이 안 잡힐 수 있으므로 이름으로 고정한다(비교 규칙은 CompareWithBaseline 참고).
        private static readonly string[] KnownTieCases =
        {
            "Skill_ConstellationLaser_Loop5",
            "Skill_ConstellationLaser_StellaLink",
            "Skill_OverheatAura_Heating",
            "Skill_OverheatAura_BlueFlame",
            "Skill_OverheatAura_BlueFlameMoving",
            "Skill_OverheatAura_StageSwap",
            "Skill_StellaBurst_R2_2_Repeat",
            "TownLoot_RepairSpark_Hammering",
            "TownLoot_ItemAura_Advanced",
            "TownLoot_ItemAura_Perfect",
            "TownLoot_ItemAura_PerfectFlying",
        };

        // //내부 의존성
        private readonly List<Renderer> rendererBuffer = new List<Renderer>(256);
        private ParticleSystem.Particle[] particleBuffer = new ParticleSystem.Particle[256];
        private readonly List<MeshFilter> meshFilterBuffer = new List<MeshFilter>(64);
        private readonly StringBuilder lineBuilder = new StringBuilder(64 * 1024);
        private readonly StringBuilder reportBuilder = new StringBuilder(16 * 1024);
        private readonly VfxGoldenContext context = new VfxGoldenContext();
        private readonly WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();

        private Camera captureCamera;
        private RenderTexture captureTexture;
        private Texture2D readbackTexture;

        private string mode;
        private string label;
        private string baselineLabel;
        private string[] filters;
        private bool bSaveImages;
        private string outputFolder;

        private int passCount;
        private int failCount;
        private int warnCount;
        private int errorCount;

        // //부트스트랩

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            string requestedMode = SessionState.GetString(KeyMode, string.Empty);
            if (string.IsNullOrEmpty(requestedMode)) return;

            Time.captureDeltaTime = FixedDelta;

            GameObject go = new GameObject("[VfxGoldenRunner]");
            DontDestroyOnLoad(go);
            go.AddComponent<VfxGoldenRunner>();
        }

        // //실행

        private IEnumerator Run()
        {
            if (ScenePath != UnityEngine.SceneManagement.SceneManager.GetActiveScene().path)
            {
                // 요청이 남은 채 평소처럼 플레이한 경우다. 게임을 끊지 않고 조용히 물러난다.
                Debug.LogWarning("[VfxGolden] 전용 씬이 아니라서 골든 캡처를 건너뜁니다. 메뉴(Tools > VFX 검증)로 실행하세요.");
                SessionState.EraseString(KeyMode);
                Time.captureDeltaTime = 0.0f;
                Destroy(gameObject);
                yield break;
            }

            if (FirstCaseFrame - 10 <= Time.frameCount)
            {
                Finish("[VfxGolden] 플레이 시작 프레임 번호가 " + Time.frameCount + "입니다. frameCount가 0부터 시작하지 않으면 결정성을 보장할 수 없어 중단합니다.");
                yield break;
            }

            CreateCaptureRig();

            if ("perf" == mode)
            {
                yield return RunPerf();
                Finish(null);
                yield break;
            }

            List<VfxGoldenCase> cases = VfxGoldenCases.Create();
            int startFrame = FirstCaseFrame;

            for (int c = 0; c < cases.Count; c++)
            {
                VfxGoldenCase golden = cases[c];
                int caseStart = startFrame;
                startFrame += golden.Frames + GapFrames;

                if (false == MatchesFilter(golden.Name)) continue;

                while (Time.frameCount < caseStart) yield return null;
                if (Time.frameCount != caseStart)
                {
                    AppendReport("ERROR", golden.Name, "시작 프레임을 놓쳤습니다(" + Time.frameCount + " != " + caseStart + ").");
                    errorCount++;
                    continue;
                }

                yield return RunCase(golden, (uint)(c + 1));
            }

            WriteReport();
            Finish(null);
        }

        private IEnumerator RunCase(VfxGoldenCase _case, uint _caseIndex)
        {
            uint caseSeed = StableHash(_case.Name) ^ (_caseIndex * 2246822519u);
            GameObject root = new GameObject("[Case] " + _case.Name);
            root.transform.SetParent(transform, false);

            ConfigureCamera(_case);
            context.Setup(root.transform, captureCamera, caseSeed);
            UnityEngine.Random.InitState((int)caseSeed);

            lineBuilder.Clear();
            lineBuilder.Append("# frame render mesh state\n");

            bool bFailed = false;
            try
            {
                _case.Setup(context);
            }
            catch (Exception _e)
            {
                AppendReport("ERROR", _case.Name, "Setup 예외: " + _e.Message);
                Debug.LogException(_e);
                errorCount++;
                bFailed = true;
            }

            if (false == bFailed)
            {
                for (int frame = 0; frame < _case.Frames; frame++)
                {
                    if (0 < frame)
                    {
                        // 코루틴은 Update 뒤에 재개되므로 여기서 부르는 OnFrame은 이펙트 LateUpdate보다 먼저 돈다.
                        yield return null;
                        try
                        {
                            _case.OnFrame(context, frame);
                        }
                        catch (Exception _e)
                        {
                            AppendReport("ERROR", _case.Name, "OnFrame(" + frame + ") 예외: " + _e.Message);
                            Debug.LogException(_e);
                            errorCount++;
                            bFailed = true;
                            break;
                        }
                    }

                    yield return endOfFrame;

                    ulong renderHash = CaptureRenderHash(_case.Name, frame, false);
                    ulong meshHash = ComputeMeshHash(root.transform);
                    ulong stateHash = ComputeStateHash(root.transform);
                    for (int e = 0; e < context.ExtraRoots.Count; e++)
                    {
                        GameObject extra = context.ExtraRoots[e];
                        if (null == extra) continue;
                        meshHash = Mix(meshHash, ComputeMeshHash(extra.transform));
                        stateHash = Mix(stateHash, ComputeStateHash(extra.transform));
                    }
                    lineBuilder.Append(frame).Append(' ')
                        .Append(renderHash.ToString("x16")).Append(' ')
                        .Append(meshHash.ToString("x16")).Append(' ')
                        .Append(stateHash.ToString("x16")).Append('\n');
                }
            }

            Destroy(root);
            for (int e = 0; e < context.ExtraRoots.Count; e++)
            {
                if (null != context.ExtraRoots[e]) Destroy(context.ExtraRoots[e]);
            }

            if (false == bFailed)
            {
                string casePath = Path.Combine(outputFolder, SafeFileName(_case.Name) + ".txt");
                File.WriteAllText(casePath, lineBuilder.ToString());
                if ("compare" == mode || "merge" == mode) CompareWithBaseline(_case, lineBuilder.ToString());
                else AppendReport("REC", _case.Name, _case.Frames + "프레임 기록");
            }
        }

        // //비교

        private void CompareWithBaseline(VfxGoldenCase _case, string _current)
        {
            string baselinePath = Path.Combine(GetRootFolder(), baselineLabel, SafeFileName(_case.Name) + ".txt");
            if (false == File.Exists(baselinePath))
            {
                AppendReport("ERROR", _case.Name, "기준 파일이 없습니다: " + baselinePath);
                errorCount++;
                return;
            }

            // 끝의 빈 줄을 양쪽 모두 버리고 센다(파일은 줄마다 \n으로 끝난다).
            string[] baseLines = File.ReadAllText(baselinePath).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string[] currentLines = _current.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            int renderDiff = 0, meshDiff = 0, stateDiff = 0, merged = 0, tieFrames = 0;
            int firstRender = -1, firstMesh = -1, firstState = -1;
            int count = Mathf.Min(baseLines.Length, currentLines.Length);
            bool bLengthMismatch = baseLines.Length != currentLines.Length;

            // 기준에 허용 그림이 하나라도 있으면 정렬 동률이 확인된 케이스다. 이런 케이스는 동률 조합이 많아 허용 그림을 다 모을 수 없으므로,
            // 그림 입력(메쉬 정점·렌더러 상태·파티클 하나하나)이 모두 같은 프레임은 그리기 순서 차이로 보고 통과시킨다.
            // 머티리얼 프로퍼티 블록 값은 상태 해시에 없으므로 이 규칙은 동률이 확인된 케이스에만 쓴다.
            bool bKnownTie = 0 <= Array.IndexOf(KnownTieCases, _case.Name);
            for (int i = 1; i < baseLines.Length && false == bKnownTie; i++)
            {
                if (0 <= baseLines[i].IndexOf('|')) bKnownTie = true;
            }

            for (int i = 1; i < count; i++)
            {
                string[] a = baseLines[i].Split(' ');
                string[] b = currentLines[i].Split(' ');
                if (4 > a.Length || 4 > b.Length) continue;
                int frame = int.Parse(a[0]);
                bool bRenderSame = IsAllowedRender(a[1], b[1]);
                bool bMeshSame = a[2] == b[2];
                bool bStateSame = a[3] == b[3];

                // merge: 정점·상태가 같은데 그림만 다르면 정렬 동률(같은 정렬 키 렌더러의 그리기 순서가 실행마다 바뀜)이다.
                // 이 그림도 같은 입력에서 나올 수 있는 결과이므로 기준에 허용 그림으로 덧붙인다.
                if ("merge" == mode && false == bRenderSame && bMeshSame && bStateSame && false == bLengthMismatch)
                {
                    a[1] = a[1] + "|" + b[1];
                    baseLines[i] = string.Join(" ", a);
                    merged++;
                    continue;
                }

                if (false == bRenderSame && bKnownTie && bMeshSame && bStateSame)
                {
                    tieFrames++;
                    continue;
                }

                if (false == bRenderSame) { renderDiff++; if (0 > firstRender) firstRender = frame; }
                if (false == bMeshSame) { meshDiff++; if (0 > firstMesh) firstMesh = frame; }
                if (false == bStateSame) { stateDiff++; if (0 > firstState) firstState = frame; }
            }

            if (0 < merged)
            {
                File.WriteAllText(baselinePath, string.Join("\n", baseLines) + "\n");
                AppendReport("MERGE", _case.Name, "정렬 동률로 보이는 그림 " + merged + "프레임을 기준에 허용 그림으로 추가");
            }

            if (0 < renderDiff || bLengthMismatch)
            {
                failCount++;
                AppendReport("FAIL", _case.Name, "그림이 다릅니다: " + renderDiff + "프레임(처음 " + firstRender + ")"
                    + (bLengthMismatch ? ", 프레임 수가 다름" : string.Empty)
                    + DescribeWarnings(meshDiff, firstMesh, stateDiff, firstState));
            }
            else if (0 < meshDiff || 0 < stateDiff)
            {
                warnCount++;
                AppendReport("WARN", _case.Name, "그림은 같음" + DescribeWarnings(meshDiff, firstMesh, stateDiff, firstState));
            }
            else
            {
                passCount++;
                AppendReport("PASS", _case.Name, 0 < tieFrames
                    ? "그림 입력은 전부 같음(그리기 순서 동률로 그림만 다른 프레임 " + tieFrames + ")"
                    : _case.Frames + "프레임 전부 같음");
            }
        }

        /// <summary>기준의 render 칸은 "해시|해시|..."(merge로 덧붙인 정렬 동률 허용 그림)일 수 있습니다.</summary>
        private static bool IsAllowedRender(string _baseField, string _current)
        {
            if (_baseField == _current) return true;
            if (0 > _baseField.IndexOf('|')) return false;
            string[] allowed = _baseField.Split('|');
            for (int i = 0; i < allowed.Length; i++)
            {
                if (allowed[i] == _current) return true;
            }
            return false;
        }

        private static string DescribeWarnings(int _meshDiff, int _firstMesh, int _stateDiff, int _firstState)
        {
            string text = string.Empty;
            if (0 < _meshDiff) text += ", 정점 다름 " + _meshDiff + "프레임(처음 " + _firstMesh + ")";
            if (0 < _stateDiff) text += ", 렌더러 상태 다름 " + _stateDiff + "프레임(처음 " + _firstState + ")";
            return text;
        }

        // //캡처

        private void CreateCaptureRig()
        {
            GameObject cameraObject = new GameObject("[VfxGolden] Camera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.tag = "MainCamera";
            captureCamera = cameraObject.AddComponent<Camera>();
            captureCamera.orthographic = true;
            captureCamera.clearFlags = CameraClearFlags.SolidColor;
            captureCamera.backgroundColor = ClearColor;
            captureCamera.nearClipPlane = -100.0f;
            captureCamera.farClipPlane = 100.0f;
            captureCamera.allowHDR = true;
            captureCamera.allowMSAA = false;

            UniversalAdditionalCameraData cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.None;

            GameObject lightObject = new GameObject("[VfxGolden] Global Light 2D");
            lightObject.transform.SetParent(transform, false);
            Light2D light = lightObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1.0f;
            light.color = Color.white;
            ApplyLightToAllSortingLayers(light);
        }

        private static void ApplyLightToAllSortingLayers(Light2D _light)
        {
            SortingLayer[] layers = SortingLayer.layers;
            int[] ids = new int[layers.Length];
            for (int i = 0; i < layers.Length; i++) ids[i] = layers[i].id;

            FieldInfo field = typeof(Light2D).GetField("m_ApplyToSortingLayers", BindingFlags.Instance | BindingFlags.NonPublic);
            if (null != field) field.SetValue(_light, ids);
            else Debug.LogWarning("[VfxGolden] Light2D.m_ApplyToSortingLayers를 찾지 못했습니다. 조명을 받는 셰이더가 어둡게 찍힐 수 있습니다.");
        }

        private void ConfigureCamera(VfxGoldenCase _case)
        {
            Vector2 size = _case.ViewSize;
            int width = Mathf.Clamp(Mathf.RoundToInt(size.x * TexelsPerUnit * PixelsPerTexel), 16, MaxRtSize);
            int height = Mathf.Clamp(Mathf.RoundToInt(size.y * TexelsPerUnit * PixelsPerTexel), 16, MaxRtSize);

            if (null == captureTexture || captureTexture.width != width || captureTexture.height != height)
            {
                if (null != captureTexture)
                {
                    captureCamera.targetTexture = null;
                    captureTexture.Release();
                    Destroy(captureTexture);
                }
                if (null != readbackTexture) Destroy(readbackTexture);

                captureTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf) { name = "VfxGoldenRT", antiAliasing = 1 };
                captureTexture.Create();
                readbackTexture = new Texture2D(width, height, TextureFormat.RGBAHalf, false, true);
            }

            captureCamera.targetTexture = captureTexture;
            captureCamera.orthographicSize = size.y * 0.5f;
            captureCamera.aspect = size.x / size.y;
            captureCamera.transform.position = new Vector3(_case.ViewCenter.x, _case.ViewCenter.y, -10.0f);
            captureCamera.transform.rotation = Quaternion.identity;
        }

        private ulong CaptureRenderHash(string _caseName, int _frame, bool _bForceImage)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = captureTexture;
            readbackTexture.ReadPixels(new Rect(0, 0, captureTexture.width, captureTexture.height), 0, 0, false);
            RenderTexture.active = previous;

            NativeArray<ulong> words = readbackTexture.GetRawTextureData<ulong>();
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < words.Length; i++)
            {
                hash ^= words[i];
                hash *= 1099511628211UL;
                hash ^= hash >> 29;
            }

            if (bSaveImages && (_bForceImage || 0 == _frame % 15))
            {
                SaveImage(_caseName, _frame);
            }
            return hash;
        }

        private void SaveImage(string _caseName, int _frame)
        {
            string folder = Path.Combine(outputFolder, "images");
            Directory.CreateDirectory(folder);
            Color[] pixels = readbackTexture.GetPixels();
            Texture2D png = new Texture2D(readbackTexture.width, readbackTexture.height, TextureFormat.RGBA32, false);
            png.SetPixels(pixels);
            png.Apply(false);
            File.WriteAllBytes(Path.Combine(folder, SafeFileName(_caseName) + "_" + _frame.ToString("D4") + ".png"), png.EncodeToPNG());
            Destroy(png);

            // 8비트 PNG로는 안 보이는 HDR·미세 차이를 조사할 수 있게 원본(RGBAHalf) 바이트도 남긴다. 머리 8바이트는 가로·세로.
            byte[] raw = readbackTexture.GetRawTextureData();
            byte[] withHeader = new byte[raw.Length + 8];
            BitConverter.GetBytes(readbackTexture.width).CopyTo(withHeader, 0);
            BitConverter.GetBytes(readbackTexture.height).CopyTo(withHeader, 4);
            Buffer.BlockCopy(raw, 0, withHeader, 8, raw.Length);
            File.WriteAllBytes(Path.Combine(folder, SafeFileName(_caseName) + "_" + _frame.ToString("D4") + ".rawh"), withHeader);
        }

        private ulong ComputeMeshHash(Transform _root)
        {
            ulong hash = 14695981039346656037UL;
            meshFilterBuffer.Clear();
            _root.GetComponentsInChildren(true, meshFilterBuffer);

            for (int i = 0; i < meshFilterBuffer.Count; i++)
            {
                MeshFilter filter = meshFilterBuffer[i];
                Renderer renderer = filter.GetComponent<Renderer>();
                if (null == renderer || false == renderer.enabled || false == filter.gameObject.activeInHierarchy) continue;

                Mesh mesh = filter.sharedMesh;
                hash = Mix(hash, (ulong)i);
                if (null == mesh) { hash = Mix(hash, 0xDEADUL); continue; }
                if (false == mesh.isReadable) { hash = Mix(hash, StableHash(mesh.name)); continue; }

                using (Mesh.MeshDataArray dataArray = Mesh.AcquireReadOnlyMeshData(mesh))
                {
                    Mesh.MeshData data = dataArray[0];
                    int maxIndex = -1;
                    bool b32 = IndexFormat.UInt32 == data.indexFormat;
                    NativeArray<ushort> indices16 = b32 ? default : data.GetIndexData<ushort>();
                    NativeArray<uint> indices32 = b32 ? data.GetIndexData<uint>() : default;

                    hash = Mix(hash, (ulong)data.subMeshCount);
                    for (int s = 0; s < data.subMeshCount; s++)
                    {
                        SubMeshDescriptor desc = data.GetSubMesh(s);
                        hash = Mix(hash, (ulong)desc.indexCount);
                        hash = Mix(hash, (ulong)desc.topology);
                        for (int k = 0; k < desc.indexCount; k++)
                        {
                            int index = desc.baseVertex + (b32 ? (int)indices32[desc.indexStart + k] : indices16[desc.indexStart + k]);
                            hash = Mix(hash, (ulong)index);
                            if (index > maxIndex) maxIndex = index;
                        }
                    }

                    int vertexUsed = maxIndex + 1;
                    for (int stream = 0; stream < data.vertexBufferCount; stream++)
                    {
                        NativeArray<byte> bytes = data.GetVertexData<byte>(stream);
                        int stride = data.GetVertexBufferStride(stream);
                        int length = Mathf.Min(bytes.Length, vertexUsed * stride);
                        for (int b = 0; b < length; b++)
                        {
                            hash ^= bytes[b];
                            hash *= 1099511628211UL;
                        }
                    }
                }
            }
            return hash;
        }

        private ulong ComputeStateHash(Transform _root)
        {
            ulong hash = 14695981039346656037UL;
            rendererBuffer.Clear();
            _root.GetComponentsInChildren(true, rendererBuffer);

            for (int i = 0; i < rendererBuffer.Count; i++)
            {
                Renderer renderer = rendererBuffer[i];
                bool bVisible = renderer.enabled && renderer.gameObject.activeInHierarchy;
                hash = Mix(hash, bVisible ? 1UL : 0UL);
                if (false == bVisible) continue;

                hash = Mix(hash, (ulong)(uint)renderer.sortingLayerID);
                hash = Mix(hash, (ulong)(uint)renderer.sortingOrder);

                Matrix4x4 m = renderer.localToWorldMatrix;
                for (int k = 0; k < 16; k++) hash = Mix(hash, (ulong)BitConverter.SingleToInt32Bits(m[k]));

                Material material = renderer.sharedMaterial;
                if (null != material)
                {
                    hash = Mix(hash, StableHash(material.name));
                    hash = Mix(hash, null != material.shader ? StableHash(material.shader.name) : 0UL);
                }

                if (renderer is SpriteRenderer sprite)
                {
                    hash = Mix(hash, null != sprite.sprite ? StableHash(sprite.sprite.name) : 0UL);
                    Color color = sprite.color;
                    hash = Mix(hash, (ulong)BitConverter.SingleToInt32Bits(color.r));
                    hash = Mix(hash, (ulong)BitConverter.SingleToInt32Bits(color.g));
                    hash = Mix(hash, (ulong)BitConverter.SingleToInt32Bits(color.b));
                    hash = Mix(hash, (ulong)BitConverter.SingleToInt32Bits(color.a));
                    hash = Mix(hash, (sprite.flipX ? 1UL : 0UL) | (sprite.flipY ? 2UL : 0UL));
                }
                else if (renderer is ParticleSystemRenderer)
                {
                    ParticleSystem ps = renderer.GetComponent<ParticleSystem>();
                    if (null != ps) hash = MixParticles(hash, ps);
                }
            }
            return hash;
        }

        // 파티클 하나하나의 위치·회전·현재 크기·현재 색·남은 수명을 섞는다(그리기 입력 전부. 남은 수명이 텍스처 시트 칸과 수명 곡선을 정한다).
        private ulong MixParticles(ulong _hash, ParticleSystem _ps)
        {
            int count = _ps.particleCount;
            ulong hash = Mix(_hash, (ulong)count);
            if (0 == count) return hash;

            if (particleBuffer.Length < count) particleBuffer = new ParticleSystem.Particle[Mathf.Max(count, particleBuffer.Length * 2)];
            int read = _ps.GetParticles(particleBuffer, count);
            for (int i = 0; i < read; i++)
            {
                ParticleSystem.Particle p = particleBuffer[i];
                Vector3 position = p.position;
                Vector3 rotation = p.rotation3D;
                Vector3 size = p.GetCurrentSize3D(_ps);
                Color32 color = p.GetCurrentColor(_ps);
                hash = Mix(hash, (ulong)BitConverter.SingleToInt32Bits(position.x));
                hash = Mix(hash, (ulong)BitConverter.SingleToInt32Bits(position.y));
                hash = Mix(hash, (ulong)BitConverter.SingleToInt32Bits(position.z));
                hash = Mix(hash, (ulong)BitConverter.SingleToInt32Bits(rotation.x));
                hash = Mix(hash, (ulong)BitConverter.SingleToInt32Bits(rotation.y));
                hash = Mix(hash, (ulong)BitConverter.SingleToInt32Bits(rotation.z));
                hash = Mix(hash, (ulong)BitConverter.SingleToInt32Bits(size.x));
                hash = Mix(hash, (ulong)BitConverter.SingleToInt32Bits(size.y));
                hash = Mix(hash, (ulong)BitConverter.SingleToInt32Bits(size.z));
                hash = Mix(hash, ((ulong)color.r << 24) | ((ulong)color.g << 16) | ((ulong)color.b << 8) | color.a);
                hash = Mix(hash, (ulong)BitConverter.SingleToInt32Bits(p.remainingLifetime));
            }
            return hash;
        }

        // //보조

        private bool MatchesFilter(string _name)
        {
            if (null == filters || 0 == filters.Length) return true;
            for (int i = 0; i < filters.Length; i++)
            {
                if (0 <= _name.IndexOf(filters[i], StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private void AppendReport(string _kind, string _caseName, string _message)
        {
            reportBuilder.Append('[').Append(_kind).Append("] ").Append(_caseName).Append(" : ").Append(_message).Append('\n');
        }

        private void WriteReport()
        {
            string header = "[VfxGolden] " + mode + " '" + label + "'"
                + ("compare" == mode || "merge" == mode ? " vs '" + baselineLabel + "' : PASS " + passCount + ", WARN " + warnCount + ", FAIL " + failCount : string.Empty)
                + ", ERROR " + errorCount + "\n";
            string text = header + reportBuilder;
            File.WriteAllText(Path.Combine(outputFolder, "_report.txt"), text);

            if (0 < failCount || 0 < errorCount) Debug.LogError(text);
            else if (0 < warnCount) Debug.LogWarning(text);
            else Debug.Log(text);
        }

        private void Finish(string _error)
        {
            if (null != _error) Debug.LogError(_error);
            SessionState.EraseString(KeyMode);
            Time.captureDeltaTime = 0.0f;
            EditorApplication.ExitPlaymode();
        }

        public static string GetRootFolder()
        {
            return Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library", "VfxGolden");
        }

        private static string SafeFileName(string _name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder sb = new StringBuilder(_name.Length);
            for (int i = 0; i < _name.Length; i++) sb.Append(Array.IndexOf(invalid, _name[i]) >= 0 ? '_' : _name[i]);
            return sb.ToString();
        }

        private static ulong Mix(ulong _hash, ulong _value)
        {
            _hash ^= _value;
            _hash *= 1099511628211UL;
            return _hash ^ (_hash >> 31);
        }

        private static uint StableHash(string _text)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < _text.Length; i++)
            {
                hash ^= _text[i];
                hash *= 16777619u;
            }
            return hash;
        }

        // //유니티 이벤트

        /// <summary>
        /// 실행 순서 -32000이라 다른 스크립트 Update보다 먼저 돈다. 에디터 코드가 프레임 사이에 UnityEngine.Random을
        /// 소비하는 일이 있어(실측: 같은 케이스가 실행마다 달라짐), 매 프레임 케이스 시드와 프레임 번호로 다시 고정한다.
        /// </summary>
        private void Update()
        {
            if (null == context.Root) return;
            UnityEngine.Random.InitState(unchecked((int)(context.CaseSeed + (uint)Time.frameCount * 7919u)));
        }

        private void Start()
        {
            mode = SessionState.GetString(KeyMode, "record");
            label = SessionState.GetString(KeyLabel, "base");
            baselineLabel = SessionState.GetString(KeyBaseline, "base");
            bSaveImages = SessionState.GetBool(KeySaveImages, false);
            string filterText = SessionState.GetString(KeyFilter, string.Empty);
            filters = string.IsNullOrWhiteSpace(filterText) ? null : filterText.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (null != filters) for (int i = 0; i < filters.Length; i++) filters[i] = filters[i].Trim();

            outputFolder = Path.Combine(GetRootFolder(), label);
            Directory.CreateDirectory(outputFolder);
            Debug.Log("[VfxGolden] 시작: mode=" + mode + ", label=" + label + ", frameCount=" + Time.frameCount);

            StartCoroutine(Run());
        }

        private void OnDestroy()
        {
            if (null != captureTexture)
            {
                captureTexture.Release();
                Destroy(captureTexture);
            }
            if (null != readbackTexture) Destroy(readbackTexture);
        }
    }
}

#endif
