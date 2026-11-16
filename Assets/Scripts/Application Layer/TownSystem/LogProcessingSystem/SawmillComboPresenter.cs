using PresentationLayer.VFX;
using UnityEngine;

/// <summary>
/// 가공 콤보(LogProcessorSpeedUp: 가공 성공마다 파이프라인 속도 증가, 최대 중첩, 제재소 파이프라인 전체가 비면 초기화) 연출.
/// LogProcessingManager의 콤보 상태를 매 프레임 읽어서 VFX_SawmillCombo(이펙트)에 넘기기만 한다.
/// 가공·벨트·판정 상태는 읽기만 하고 바꾸지 않는다(커터 그림의 색만 달아오름 연출로 바꾼다).
///
/// - 콤보가 쌓일수록: 켜는 중인 커터의 톱밥이 늘어난다.
/// - 최대 중첩에 닿는 순간: 커터마다 섬광·충격파·불티. 그동안 커터가 붉게 달아오르고 열기가 피어오른다.
/// - 파이프라인이 비어 풀리는 순간: 커터마다 회색 연기.
/// 마을에 있을 때만 그린다(던전에 있는 동안 제재소는 화면 밖에서 돈다).
/// </summary>
public class SawmillComboPresenter : MonoBehaviour
{
    // 콤보 비율(0~1)에 따른 초당 생성 수.
    private const float SawdustRateMin = 6f;    // 커터 하나당
    private const float SawdustRateMax = 40f;
    private const float HeatRate = 9f;          // 최대 중첩 동안 커터 하나당

    // 커터 윗면 자리: 커터 그림 경계 가운데에서 위로 경계 반높이의 이 비율만큼(그림 위쪽은 빈 여백이라 가운데 근처가 윗면이다).
    private const float CutterTopRatio = 0f;
    private const float CutterHalfWidth = 0.38f;

    // 달아오름: 최대 중첩 동안 커터 그림 색이 흰색과 이 색 사이를 맥박친다.
    private static readonly Color HeatTint = new Color(1f, 0.74f, 0.64f, 1f);
    private const float HeatPulseSpeed = 6f;

    private LogProcessingManager manager;
    private VFX_SawmillCombo vfx;
    private LogProcessLine[] lines = new LogProcessLine[0];

    private MapType mapType = MapType.Town;
    private bool bSynced;
    private int lastStack;
    private bool bTinted;
    private float heatTime;

    // 생성 누적.
    private float[] sawdustAccum = new float[0];
    private float[] heatAccum = new float[0];

    private uint randomState = 1u;

    public void Setup(LogProcessingManager _manager)
    {
        manager = _manager;

        lines = GetComponentsInChildren<LogProcessLine>(true);
        System.Array.Sort(lines, (a, b) => a.LineIndex.CompareTo(b.LineIndex));

        sawdustAccum = new float[lines.Length];
        heatAccum = new float[lines.Length];

        if (vfx == null) vfx = GetComponentInChildren<VFX_SawmillCombo>(true);
        if (vfx == null) vfx = VFX_SawmillCombo.Create(transform);

        randomState = unchecked((uint)Random.Range(1, int.MaxValue) * 2654435761u) | 1u;
        bSynced = false;
    }

    public void SetMapType(MapType _mapType)
    {
        mapType = _mapType;

        // 맵이 바뀌면 그리던 것을 지우고, 돌아왔을 때 그동안 바뀐 콤보를 한꺼번에 터뜨리지 않도록 다시 맞춘다.
        if (vfx != null) vfx.ClearAll();
        ClearTint();
        bSynced = false;
    }

    private void OnDisable()
    {
        if (vfx != null) vfx.ClearAll();
        ClearTint();
        bSynced = false;
    }

    private void Update()
    {
        if (manager == null) return;

        int stack = manager.ProcessorComboStack;
        int max = Mathf.Max(1, manager.ProcessorComboMaxStack);

        if (mapType != MapType.Town || false == manager.IsProcessorComboActive)
        {
            ClearTint();
            lastStack = stack;
            bSynced = true;
            return;
        }

        int activeCount = Mathf.Min(manager.ActiveLineCount, lines.Length);
        float dt = Time.deltaTime;

        // 쌓이고 풀리는 순간
        if (false == bSynced)
        {
            lastStack = stack;
            bSynced = true;
        }
        else if (stack != lastStack)
        {
            if (vfx != null && stack >= max && lastStack < max)
            {
                for (int i = 0; i < activeCount; i++) vfx.PlayOverdrive(GetCutterTop(lines[i].Cutter));
            }
            else if (vfx != null && stack == 0 && lastStack > 0)
            {
                for (int i = 0; i < activeCount; i++) vfx.PlaySmoke(GetCutterTop(lines[i].Cutter), CutterHalfWidth);
            }
            lastStack = stack;
        }

        float ratio = stack / (float)max;
        bool bMax = stack >= max;

        for (int i = 0; i < activeCount; i++)
        {
            LogProcessLine line = lines[i];
            if (line == null || vfx == null) continue;

            if (stack > 0)
            {
                EmitSawdust(line.Cutter, i, ratio, dt);
            }
            else
            {
                sawdustAccum[i] = 0f;
            }

            if (bMax)
            {
                heatAccum[i] += dt * HeatRate;
                while (heatAccum[i] >= 1f)
                {
                    heatAccum[i] -= 1f;
                    vfx.EmitHeat(GetCutterTop(line.Cutter), CutterHalfWidth * 0.8f);
                }
            }
            else
            {
                heatAccum[i] = 0f;
            }
        }

        // 달아오름(최대 중첩 동안 커터 그림이 붉게 맥박친다)
        if (bMax)
        {
            heatTime += dt;
            float pulse = 0.5f + 0.5f * Mathf.Sin(heatTime * HeatPulseSpeed);
            Color tint = Color.Lerp(Color.white, HeatTint, 0.45f + 0.55f * pulse);
            for (int i = 0; i < activeCount; i++) lines[i].Cutter.SetVisualTint(tint);
            bTinted = true;
        }
        else
        {
            heatTime = 0f;
            ClearTint();
        }
    }

    private void EmitSawdust(LogCutter _cutter, int _slot, float _ratio, float _dt)
    {
        if (_cutter == null || false == _cutter.IsCuttingForward)
        {
            sawdustAccum[_slot] = 0f;
            return;
        }

        sawdustAccum[_slot] += _dt * Mathf.Lerp(SawdustRateMin, SawdustRateMax, _ratio);
        while (sawdustAccum[_slot] >= 1f)
        {
            sawdustAccum[_slot] -= 1f;
            vfx.EmitSawdust(_cutter.EffectPoint, _cutter.SampleEffectColor(Rand01()));
        }
    }

    private static Vector3 GetCutterTop(LogCutter _cutter)
    {
        Bounds bounds = _cutter.VisualBounds;
        return new Vector3(bounds.center.x, bounds.center.y + bounds.extents.y * CutterTopRatio, 0f);
    }

    private void ClearTint()
    {
        if (false == bTinted) return;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i] != null && lines[i].Cutter != null) lines[i].Cutter.SetVisualTint(Color.white);
        }
        bTinted = false;
    }

    private float Rand01()
    {
        randomState = unchecked(randomState * 1664525u + 1013904223u);
        return ((randomState >> 8) & 0xFFFF) / 65535f;
    }
}
