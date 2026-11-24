#if UNITY_EDITOR
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 월드 HUD 페인터 픽셀 검증(WorldHUD_Plan.md 6장 3단계). 파이썬 기준 레이어(world_hud_v6_ref.py가 verify/ref/cases.txt + PNG를 만든다)의 케이스를
/// (웨이브 · 처치 숫자 케이스는 11장 배치 변경으로 v6 기준에서 빠졌고 RunMotion의 정적 케이스가 대신한다)
/// C# 페인터로 같은 값으로 그려 verify/cs/에 640x360 투명 PNG로 저장한다. 비교는 verify/compare_layers.py가 한다(다른 픽셀 0개가 기준).
///   LumberBoy/UI/Verify Roguelike World HUD Painter
/// </summary>
public static class RoguelikeWorldHudVerifier
{
    private const string VerifyDir = "Docs/Roguelike/_Work/WorldHUD/verify/";

    [MenuItem("LumberBoy/UI/Verify Roguelike World HUD Painter")]
    public static void Run()
    {
        RoguelikeWorldHudBuilder.EnsureSheetImportSettings();
        RoguelikeWorldHudSheets sheets = RoguelikeWorldHudBuilder.LoadSheets();
        if (null == sheets) return;

        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", VerifyDir));
        string casesPath = Path.Combine(root, "ref", "cases.txt");
        if (false == File.Exists(casesPath))
        {
            Debug.LogError("[RoguelikeWorldHudVerifier] cases.txt가 없습니다(world_hud_v6_ref.py 실행 필요): " + casesPath);
            return;
        }

        string outDir = Path.Combine(root, "cs");
        Directory.CreateDirectory(outDir);

        RoguelikeWorldHudPainter painter = new RoguelikeWorldHudPainter(sheets);
        string[] lines = File.ReadAllLines(casesPath);
        int count = 0;

        for (int i = 0; lines.Length > i; i++)
        {
            if (0 == lines[i].Length) continue;

            string[] f = lines[i].Split('|');
            string kind = f[0];
            string id = f[1];
            Color32[] buffer;
            RoguelikeWorldHudRect rect;

            switch (kind)
            {
                case "thr":
                    painter.PaintThresholdBand(ParseInt(f[2]), ParseInt(f[3]), 0 != ParseInt(f[4]));
                    buffer = painter.ThresholdBandBuffer;
                    rect = RoguelikeWorldHudGeometry.ThresholdBandRect;
                    break;
                case "sta":
                {
                    double st = ParseDouble(f[2]);
                    double mx = ParseDouble(f[3]);
                    double trail = ParseDouble(f[4]);
                    bool bLoss = 0 != ParseInt(f[5]);
                    int fill = RoguelikeWorldHudPainter.ToStaminaPixels(st, mx);
                    int tr = 0d != trail ? RoguelikeWorldHudPainter.ToStaminaPixels(trail) : 0;
                    RoguelikeWorldHudTone tone = true == bLoss ? RoguelikeWorldHudPalette.Hot : RoguelikeWorldHudPalette.Stamina;
                    painter.PaintStaminaBand(fill, tr, in tone, 0, 0, false);
                    buffer = painter.StaminaBandBuffer;
                    rect = RoguelikeWorldHudGeometry.StaminaBandRect;
                    break;
                }
                case "left":
                    painter.PaintThresholdLabel(ParseInt(f[2]), ParseInt(f[3]), 0 != ParseInt(f[4]) ? RoguelikeWorldHudDigitStyle.Warn : RoguelikeWorldHudDigitStyle.White);
                    buffer = painter.ThresholdLabelBuffer;
                    rect = RoguelikeWorldHudGeometry.ThresholdLabelRect;
                    break;
                case "right":
                {
                    ParseDash(f[5], out int dashCount, out RoguelikeWorldHudDashCell cell0, out RoguelikeWorldHudDashCell cell1);
                    painter.PaintStaminaLabel(ParseInt(f[2]), ParseInt(f[3]), 0 != ParseInt(f[4]) ? RoguelikeWorldHudDigitStyle.Warn : RoguelikeWorldHudDigitStyle.White, dashCount, in cell0, in cell1);
                    buffer = painter.StaminaLabelBuffer;
                    rect = RoguelikeWorldHudGeometry.StaminaLabelRect;
                    break;
                }
                default:
                    Debug.LogError("[RoguelikeWorldHudVerifier] 알 수 없는 종류: " + kind);
                    continue;
            }

            SaveCanvas(Path.Combine(outDir, id + ".png"), buffer, in rect, 0, 0);
            count++;
        }

        Debug.Log("[RoguelikeWorldHudVerifier] 저장 완료: " + count + "개 -> " + outDir);
    }

    /// <summary>
    /// 연출 픽셀 검증(WorldHUD_Plan.md 10-5 · 11장): verify/ref_v8/timeline.txt(world_hud_v8_ref.py가 만든 시간표)를 RoguelikeWorldHudPresenter로 같은 값 · 같은 사건으로 30fps 재생해
    /// 프레임마다 요소 6개 레이어(입자 제외, 숫자는 위치 보정 반영)를 verify/cs_v8/에 640x360 투명 PNG로 저장하고, 시간표의 정적 숫자 케이스(웨이브 · 언리밋 · 남은 벌목 수)도 같이 저장한다. 비교는 compare_layers.py ref_v8 cs_v8.
    ///   LumberBoy/UI/Verify Roguelike World HUD Motion
    /// </summary>
    [MenuItem("LumberBoy/UI/Verify Roguelike World HUD Motion")]
    public static void RunMotion()
    {
        RoguelikeWorldHudBuilder.EnsureSheetImportSettings();
        RoguelikeWorldHudSheets sheets = RoguelikeWorldHudBuilder.LoadSheets();
        if (null == sheets) return;

        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", VerifyDir));
        string timelinePath = Path.Combine(root, "ref_v8", "timeline.txt");
        if (false == File.Exists(timelinePath))
        {
            Debug.LogError("[RoguelikeWorldHudVerifier] timeline.txt가 없습니다(world_hud_v8_ref.py 실행 필요): " + timelinePath);
            return;
        }

        string outDir = Path.Combine(root, "cs_v8");
        Directory.CreateDirectory(outDir);

        string[] lines = File.ReadAllLines(timelinePath);
        int threshold = 1;
        int target = 0;
        int staminaMax = 100;
        int fps = 30;
        int frameCount = 0;
        double showTime = 0d;
        double hideTime = double.MaxValue;

        for (int i = 0; lines.Length > i; i++)
        {
            string[] f = lines[i].Split('|');
            if ("meta" == f[0])
            {
                threshold = ParseInt(f[1]);
                target = ParseInt(f[2]);
                staminaMax = ParseInt(f[3]);
                fps = ParseInt(f[4]);
                frameCount = ParseInt(f[5]);
            }
            else if ("show" == f[0]) showTime = ParseDouble(f[1]);
            else if ("hide" == f[0]) hideTime = ParseDouble(f[1]);
        }

        int[] treeValues = new int[frameCount];
        int[] killValues = new int[frameCount];
        int[] staminaValues = new int[frameCount];
        for (int i = 0; lines.Length > i; i++)
        {
            string[] f = lines[i].Split('|');
            if ("f" != f[0]) continue;

            int frame = ParseInt(f[1]);
            treeValues[frame] = ParseInt(f[2]);
            killValues[frame] = ParseInt(f[3]);
            staminaValues[frame] = ParseInt(f[4]);
        }

        // 요소 종류 -> 프레젠터 요소 번호 / 파일 접두사 (ref_v8과 같은 순서)
        string[] kinds = { "thr", "sta", "left", "right", "wave", "kill" };
        int[] elements =
        {
            RoguelikeWorldHudPresenter.ThresholdBandIndex, RoguelikeWorldHudPresenter.StaminaBandIndex, RoguelikeWorldHudPresenter.ThresholdLabelIndex,
            RoguelikeWorldHudPresenter.StaminaLabelIndex, RoguelikeWorldHudPresenter.WaveLabelIndex, RoguelikeWorldHudPresenter.KillLabelIndex,
        };
        RoguelikeWorldHudRect[] rects =
        {
            RoguelikeWorldHudGeometry.ThresholdBandRect, RoguelikeWorldHudGeometry.StaminaBandRect, RoguelikeWorldHudGeometry.ThresholdLabelRect,
            RoguelikeWorldHudGeometry.StaminaLabelRect, RoguelikeWorldHudGeometry.WaveLabelRect, RoguelikeWorldHudGeometry.KillLabelRect,
        };

        RoguelikeWorldHudPresenter presenter = new RoguelikeWorldHudPresenter(sheets);
        presenter.SetWave(7, false, 0);
        presenter.Show(showTime);

        bool bHideCalled = false;
        int saved = 0;

        for (int frame = 0; frameCount > frame; frame++)
        {
            double t = frame / (double)fps;

            if (false == bHideCalled && hideTime <= t)
            {
                bHideCalled = true;
                presenter.Hide(hideTime);
            }

            int stamina = staminaValues[frame];
            presenter.SetThreshold(treeValues[frame], threshold);
            presenter.SetKills(killValues[frame], target);
            presenter.SetStaminaNumbers(stamina, staminaMax);

            // 시뮬 frame(): 스태미너가 줄어든 프레임에 잔상 + 펄스(테 흰색 + 숫자 흔들림)를 건다.
            if (0 < frame && stamina < staminaValues[frame - 1])
            {
                presenter.PlayStaminaDamaged((staminaValues[frame - 1] - stamina) / (float)staminaMax);
                presenter.SetStaminaRatio(stamina / (float)staminaMax);
                presenter.PlayPulse();
            }
            else
            {
                presenter.SetStaminaRatio(stamina / (float)staminaMax);
            }

            presenter.Tick(t);
            presenter.PaintChanged();

            for (int k = 0; kinds.Length > k; k++)
            {
                string path = Path.Combine(outDir, kinds[k] + "_" + frame.ToString("000", CultureInfo.InvariantCulture) + ".png");
                Color32[] buffer = true == presenter.IsVisible ? presenter.GetBuffer(elements[k]) : null;
                bool bLabel = 2 <= k;
                SaveCanvas(path, buffer, in rects[k], true == bLabel && true == presenter.IsVisible ? presenter.GetOffsetX(elements[k]) : 0, true == bLabel && true == presenter.IsVisible ? presenter.GetOffsetY(elements[k]) : 0);
                saved++;
            }
        }

        // 정적 숫자 케이스: s|종류(wave / unl / rem)|id|값
        RoguelikeWorldHudPainter painter = new RoguelikeWorldHudPainter(sheets);
        for (int i = 0; lines.Length > i; i++)
        {
            string[] f = lines[i].Split('|');
            if ("s" != f[0]) continue;

            int value = ParseInt(f[3]);
            Color32[] buffer;
            RoguelikeWorldHudRect rect;

            if ("wave" == f[1])
            {
                painter.PaintWaveLabel(value);
                buffer = painter.WaveLabelBuffer;
                rect = RoguelikeWorldHudGeometry.WaveLabelRect;
            }
            else if ("unl" == f[1])
            {
                painter.PaintUnlimitLabel(value);
                buffer = painter.WaveLabelBuffer;
                rect = RoguelikeWorldHudGeometry.WaveLabelRect;
            }
            else
            {
                painter.PaintKillLabel(value, RoguelikeWorldHudDigitStyle.White);
                buffer = painter.KillLabelBuffer;
                rect = RoguelikeWorldHudGeometry.KillLabelRect;
            }

            SaveCanvas(Path.Combine(outDir, f[2] + ".png"), buffer, in rect, 0, 0);
            saved++;
        }

        Debug.Log("[RoguelikeWorldHudVerifier] 연출 저장 완료: " + saved + "개(" + frameCount + "프레임) -> " + outDir);
    }

    private static int ParseInt(string _text)
    {
        return int.Parse(_text, CultureInfo.InvariantCulture);
    }

    private static double ParseDouble(string _text)
    {
        return double.Parse(_text, CultureInfo.InvariantCulture);
    }

    /// <summary>'-' 또는 'ratio:N,ratio:F'를 대쉬 칸 상태로 푼다.</summary>
    private static void ParseDash(string _text, out int _count, out RoguelikeWorldHudDashCell _cell0, out RoguelikeWorldHudDashCell _cell1)
    {
        _count = 0;
        _cell0 = default;
        _cell1 = default;
        if ("-" == _text) return;

        string[] tokens = _text.Split(',');
        _count = tokens.Length;

        for (int i = 0; tokens.Length > i; i++)
        {
            string[] pair = tokens[i].Split(':');
            RoguelikeWorldHudDashCell cell = new RoguelikeWorldHudDashCell(float.Parse(pair[0], CultureInfo.InvariantCulture), "F" == pair[1]);
            if (0 == i) _cell0 = cell;
            else _cell1 = cell;
        }
    }

    /// <summary>요소 버퍼를 (_offsetX, _offsetY)px 옮긴 화면 위치로 640x360 투명 캔버스에 얹어 PNG로 저장한다. _buffer가 null이면 빈 캔버스.</summary>
    private static void SaveCanvas(string _path, Color32[] _buffer, in RoguelikeWorldHudRect _rect, int _offsetX, int _offsetY)
    {
        Color32[] canvas = new Color32[RoguelikeWorldHudGeometry.ScreenWidth * RoguelikeWorldHudGeometry.ScreenHeight];

        for (int ly = 0; null != _buffer && _rect.height > ly; ly++)
        {
            for (int lx = 0; _rect.width > lx; lx++)
            {
                int sx = _rect.x + _offsetX + lx;
                int sy = _rect.y + _offsetY + ly;
                if (0 > sx || RoguelikeWorldHudGeometry.ScreenWidth <= sx || 0 > sy || RoguelikeWorldHudGeometry.ScreenHeight <= sy) continue;

                canvas[(RoguelikeWorldHudGeometry.ScreenHeight - 1 - sy) * RoguelikeWorldHudGeometry.ScreenWidth + sx] = _buffer[(_rect.height - 1 - ly) * _rect.width + lx];
            }
        }

        Texture2D texture = new Texture2D(RoguelikeWorldHudGeometry.ScreenWidth, RoguelikeWorldHudGeometry.ScreenHeight, TextureFormat.RGBA32, false);
        texture.SetPixels32(canvas);
        File.WriteAllBytes(_path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
    }
}
#endif
