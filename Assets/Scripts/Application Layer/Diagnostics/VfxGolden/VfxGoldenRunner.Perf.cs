// ─────────────────────────────────────────────────────────────────────────────
// VFX 골든 캡처 도구의 성능 측정 모드. 에디터 전용이라 빌드에는 컴파일되지 않습니다.
//
// [무엇을 재는가]
// VfxPerfScenarios의 시나리오마다 같은 케이스를 여러 개 겹쳐 띄우고(최악 조건: 전부 화면 안),
// 케이스 수명 동안 프레임마다 ProfilerRecorder 값을 모읍니다. 같은 시나리오를 Repeats번 반복합니다.
//   - work        : 아래 구간의 합(판정에 쓰는 값)
//   - Update/Late : 스크립트 Update·LateUpdate(절차적 VFX 대부분이 LateUpdate에서 메쉬를 만든다)
//   - Coroutine   : 코루틴 재개 시간
//   - Particle    : 파티클 준비(BeginUpdateAll)·마무리(EndUpdateAll, 워커 대기 포함)
//   - Renderers   : 렌더러 갱신(UpdateAllRenderers)
//   - Camera      : 캡처 카메라 URP 렌더(메인 스레드 제출 비용)
//   - busy        : PlayerLoop - 대기 마커(참고값. 에디터에선 다른 대기가 섞여 안 맞을 수 있음)
//   - GC          : 프레임당 GC 할당 바이트(에디터 몫이 섞이므로 빈 시나리오 바닥값 대비로 읽음)
// 결과는 Library/VfxGolden/<라벨>/_perf.txt. 비교 기준 라벨을 주면 같은 시나리오끼리 비율을 붙입니다.
//
// [주의] 에디터(Mono)·백그라운드 상태의 값이라 절대값보다 "같은 조건의 전후 비율"로 읽으세요.
// 렌더 캡처(ReadPixels)는 하지 않지만 캡처 카메라는 매 프레임 그립니다(실제 렌더 제출 비용 포함).
// ─────────────────────────────────────────────────────────────────────────────
#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace Diagnostics.VfxGolden
{
    /// <summary>성능 측정 시나리오: 같은 케이스를 Count개 동시에 띄웁니다.</summary>
    public class VfxPerfScenario
    {
        public readonly string Name;
        public readonly Func<VfxGoldenCase> Factory;
        public readonly int Count;

        public VfxPerfScenario(string _name, Func<VfxGoldenCase> _factory, int _count)
        {
            Name = _name;
            Factory = _factory;
            Count = _count;
        }
    }

    public partial class VfxGoldenRunner
    {
        private const int PerfRepeats = 3;
        private const int PerfWarmupFrames = 2;

        // 측정할 프로파일러 마커(PerfCategories와 같은 순서).
        // busy = PlayerLoop - 대기 마커(목표 FPS·present·이전 프레임 표시 대기). 에디터에서는 PlayerLoop 대부분이 대기다.
        // work = 이펙트가 쓰는 메인 스레드 구간의 합(스크립트·코루틴·파티클 준비/마무리·렌더러 갱신·캡처 카메라 렌더).
        private static readonly string[] PerfMarkers =
        {
            "PlayerLoop",
            "WaitForTargetFPS",
            "Gfx.WaitForPresentOnGfxThread",
            "Gfx.WaitForRenderThread",
            "TimeUpdate.WaitForLastPresentationAndUpdateTime",
            "Update.ScriptRunBehaviourUpdate",
            "PreLateUpdate.ScriptRunBehaviourLateUpdate",
            "Update.ScriptRunDelayedDynamicFrameRate",
            "PreLateUpdate.ParticleSystemBeginUpdateAll",
            "PostLateUpdate.ParticleSystemEndUpdateAll",
            "PostLateUpdate.UpdateAllRenderers",
            "Inl_UniversalRenderPipeline.RenderSingleCameraInternal: [VfxGolden] Camera",
            "GC Allocated In Frame",
        };
        private static readonly ProfilerCategory[] PerfCategories =
        {
            ProfilerCategory.Internal,
            ProfilerCategory.Internal,
            ProfilerCategory.Render,
            ProfilerCategory.Render,
            ProfilerCategory.Internal,
            ProfilerCategory.Scripts,
            ProfilerCategory.Scripts,
            ProfilerCategory.Scripts,
            ProfilerCategory.Internal,
            ProfilerCategory.Internal,
            ProfilerCategory.Internal,
            ProfilerCategory.Scripts,
            ProfilerCategory.Memory,
        };
        private const int MarkerLoop = 0;
        private const int MarkerWaitFirst = 1;
        private const int MarkerWaitLast = 4;
        private const int MarkerUpdate = 5;
        private const int MarkerLate = 6;
        private const int MarkerCoroutine = 7;
        private const int MarkerParticleBegin = 8;
        private const int MarkerParticleEnd = 9;
        private const int MarkerRenderers = 10;
        private const int MarkerCamera = 11;
        private const int MarkerGc = 12;
        private const int MarkerWorkFirst = 5;
        private const int MarkerWorkLast = 11;

        private IEnumerator RunPerf()
        {
            List<VfxPerfScenario> scenarios = VfxPerfScenarios.Create();
            StringBuilder perf = new StringBuilder(8 * 1024);
            perf.Append("# name count frames work_mean_ms work_p95_ms busy_mean_ms update_ms late_ms coroutine_ms particle_ms renderers_ms camera_ms gc_bytes_per_frame\n");

            Dictionary<string, double> baseline = LoadPerfBaseline();
            StringBuilder summary = new StringBuilder(8 * 1024);

            ProfilerRecorder[] recorders = new ProfilerRecorder[PerfMarkers.Length];
            StringBuilder invalid = new StringBuilder();
            for (int m = 0; m < PerfMarkers.Length; m++)
            {
                recorders[m] = ProfilerRecorder.StartNew(PerfCategories[m], PerfMarkers[m], 1);
                if (false == recorders[m].Valid) invalid.Append(' ').Append(PerfMarkers[m]);
            }
            if (0 < invalid.Length)
            {
                summary.Append("(유효하지 않은 마커, 0으로 계산:").Append(invalid).Append(")\n");
            }

            List<double> workSamples = new List<double>(1024);
            double[] sums = new double[PerfMarkers.Length];
            double floorWork = 0.0, floorGc = 0.0;
            bool bHasFloor = false;

            for (int s = 0; s < scenarios.Count; s++)
            {
                VfxPerfScenario scenario = scenarios[s];
                bool bFloor = VfxPerfScenarios.FloorName == scenario.Name;
                if (false == bFloor && false == MatchesFilter(scenario.Name)) continue;

                workSamples.Clear();
                Array.Clear(sums, 0, sums.Length);
                int sampleCount = 0;
                int frames = 0;
                bool bFailed = false;

                for (int r = 0; r < PerfRepeats && false == bFailed; r++)
                {
                    GameObject root = new GameObject("[Perf] " + scenario.Name);
                    root.transform.SetParent(transform, false);

                    VfxGoldenCase[] instances = new VfxGoldenCase[scenario.Count];
                    for (int i = 0; i < instances.Length; i++) instances[i] = scenario.Factory();
                    frames = instances[0].Frames;
                    ConfigureCamera(instances[0]);
                    context.Setup(root.transform, captureCamera, StableHash(scenario.Name) + (uint)r);
                    UnityEngine.Random.InitState((int)context.CaseSeed);

                    try
                    {
                        for (int i = 0; i < instances.Length; i++) instances[i].Setup(context);
                    }
                    catch (Exception _e)
                    {
                        Debug.LogException(_e);
                        AppendReport("ERROR", scenario.Name, "Setup 예외: " + _e.Message);
                        errorCount++;
                        bFailed = true;
                    }

                    for (int frame = 1; frame < frames && false == bFailed; frame++)
                    {
                        yield return null;
                        for (int i = 0; i < instances.Length; i++) instances[i].OnFrame(context, frame);

                        // 직전 프레임의 값이 지금 읽힌다. 시작 직후 몇 프레임은 Setup·생성 비용이라 버린다.
                        if (PerfWarmupFrames < frame)
                        {
                            double work = 0.0;
                            for (int w = MarkerWorkFirst; w <= MarkerWorkLast; w++) work += recorders[w].LastValue;
                            workSamples.Add(work * 1e-6);
                            for (int m = 0; m < recorders.Length; m++) sums[m] += recorders[m].LastValue;
                            sampleCount++;
                        }
                    }

                    Destroy(root);
                    for (int e = 0; e < context.ExtraRoots.Count; e++)
                    {
                        if (null != context.ExtraRoots[e]) Destroy(context.ExtraRoots[e]);
                    }
                    yield return null;
                    yield return null;
                }

                if (bFailed || 0 == sampleCount) continue;

                workSamples.Sort();
                double workMean = 0.0;
                for (int i = 0; i < workSamples.Count; i++) workMean += workSamples[i];
                workMean /= workSamples.Count;
                double workP95 = workSamples[Mathf.Min(workSamples.Count - 1, (int)(workSamples.Count * 0.95))];
                double busySum = sums[MarkerLoop];
                for (int w = MarkerWaitFirst; w <= MarkerWaitLast; w++) busySum -= sums[w];
                double busyMean = busySum * 1e-6 / sampleCount;
                double updateMean = sums[MarkerUpdate] * 1e-6 / sampleCount;
                double lateMean = sums[MarkerLate] * 1e-6 / sampleCount;
                double coroutineMean = sums[MarkerCoroutine] * 1e-6 / sampleCount;
                double particleMean = (sums[MarkerParticleBegin] + sums[MarkerParticleEnd]) * 1e-6 / sampleCount;
                double renderersMean = sums[MarkerRenderers] * 1e-6 / sampleCount;
                double cameraMean = sums[MarkerCamera] * 1e-6 / sampleCount;
                double gcMean = sums[MarkerGc] / sampleCount;

                if (bFloor)
                {
                    floorWork = workMean;
                    floorGc = gcMean;
                    bHasFloor = true;
                }

                perf.Append(scenario.Name).Append(' ').Append(scenario.Count).Append(' ').Append(frames).Append(' ')
                    .Append(F(workMean)).Append(' ').Append(F(workP95)).Append(' ').Append(F(busyMean)).Append(' ')
                    .Append(F(updateMean)).Append(' ').Append(F(lateMean)).Append(' ').Append(F(coroutineMean)).Append(' ')
                    .Append(F(particleMean)).Append(' ').Append(F(renderersMean)).Append(' ').Append(F(cameraMean)).Append(' ')
                    .Append(gcMean.ToString("0", CultureInfo.InvariantCulture)).Append('\n');

                summary.Append(scenario.Name).Append(" x").Append(scenario.Count)
                    .Append(" : work ").Append(F(workMean)).Append("ms");
                if (bHasFloor && false == bFloor)
                {
                    summary.Append(" (바닥 대비 ").Append((workMean - floorWork).ToString("+0.000;-0.000", CultureInfo.InvariantCulture)).Append(")");
                }
                summary.Append(" p95 ").Append(F(workP95))
                    .Append(" | Update ").Append(F(updateMean))
                    .Append(" Late ").Append(F(lateMean))
                    .Append(" Coroutine ").Append(F(coroutineMean))
                    .Append(" Particle ").Append(F(particleMean))
                    .Append(" Renderers ").Append(F(renderersMean))
                    .Append(" Camera ").Append(F(cameraMean))
                    .Append(" | busy ").Append(F(busyMean))
                    .Append(" | GC ").Append(gcMean.ToString("0", CultureInfo.InvariantCulture)).Append("B/f");
                if (bHasFloor && false == bFloor)
                {
                    summary.Append(" (바닥 대비 ").Append((gcMean - floorGc).ToString("+0;-0", CultureInfo.InvariantCulture)).Append(")");
                }
                if (baseline.TryGetValue(scenario.Name + "/work", out double baseWork) && 0.0 < baseWork)
                {
                    summary.Append("  | 기준 대비 work ").Append((workMean / baseWork).ToString("0.00", CultureInfo.InvariantCulture)).Append("배");
                    if (baseline.TryGetValue(scenario.Name + "/late", out double baseLate) && 0.0 < baseLate)
                    {
                        summary.Append(", Late ").Append((lateMean / baseLate).ToString("0.00", CultureInfo.InvariantCulture)).Append("배");
                    }
                }
                summary.Append('\n');
            }

            for (int m = 0; m < recorders.Length; m++) recorders[m].Dispose();

            File.WriteAllText(Path.Combine(outputFolder, "_perf.txt"), perf.ToString());
            string text = "[VfxGolden] perf '" + label + "'" + (0 < baseline.Count ? " vs '" + baselineLabel + "'" : string.Empty)
                + ", ERROR " + errorCount + "\n" + summary + reportBuilder;
            File.WriteAllText(Path.Combine(outputFolder, "_report.txt"), text);
            Debug.Log(text);
        }

        private Dictionary<string, double> LoadPerfBaseline()
        {
            Dictionary<string, double> result = new Dictionary<string, double>(64);
            if (string.IsNullOrEmpty(baselineLabel) || baselineLabel == label) return result;

            string path = Path.Combine(GetRootFolder(), baselineLabel, "_perf.txt");
            if (false == File.Exists(path)) return result;

            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                if (0 == lines[i].Length || '#' == lines[i][0]) continue;
                string[] parts = lines[i].Split(' ');
                if (13 > parts.Length) continue;
                result[parts[0] + "/work"] = double.Parse(parts[3], CultureInfo.InvariantCulture);
                result[parts[0] + "/late"] = double.Parse(parts[7], CultureInfo.InvariantCulture);
            }
            return result;
        }

        private static string F(double _value)
        {
            return _value.ToString("0.000", CultureInfo.InvariantCulture);
        }
    }
}

#endif
