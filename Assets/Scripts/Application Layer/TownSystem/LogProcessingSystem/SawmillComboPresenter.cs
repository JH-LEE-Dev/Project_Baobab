using PresentationLayer.VFX;
using UnityEngine;

/// <summary>
/// 가공 콤보(LogProcessorSpeedUp: 가공 성공마다 파이프라인 속도 증가, 최대 중첩, 상자가 비면 초기화) 연출.
/// LogProcessingManager의 콤보 상태를 매 프레임 읽어서 VFX_SawmillCombo(이펙트)와 SawmillComboGauge(게이지)에 넘기기만 한다.
/// 가공·벨트·판정 상태는 읽기만 하고 바꾸지 않는다(커터 그림의 색만 달아오름 연출로 바꾼다).
///
/// - 콤보가 쌓일수록: 켜는 중인 커터의 톱밥이 늘어난다.
/// - 최대 중첩에 닿는 순간: 커터마다 섬광·충격파·불티. 그동안 커터가 붉게 달아오르고 열기가 피어오른다.
/// - 상자가 비어 풀리는 순간: 커터마다 회색 연기.
/// - 원목 상자 위 게이지: 지금 콤보 수(특성을 가진 동안 항상). 기존 UI 판·커터 게이지 그림에 셰이더로 칸을 나눈다.
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

    // 게이지 바닥 자리: 상자 그림 경계 가운데에서 위로 경계 반높이의 이 비율만큼(그림 위쪽 여백을 빼고 상자 기둥 바로 위).
    private const float GaugeHeightRatio = 0.6f;

    private LogProcessingManager manager;
    private VFX_SawmillCombo vfx;
    private SawmillComboGauge gauge;
    private LogProcessLine[] lines = new LogProcessLine[0];

    private MapType mapType = MapType.Town;
    private bool bSynced;
    private int lastStack;
    private bool bTinted;
    private float heatTime;

    // 생성 누적.
    private float[] sawdustAccum = new float[0];
    private float[] heatAccum = new float[0];

    // 상자 그림 위 가운데의 상자 위치 기준 오프셋(처음 한 번 잰다).
    private bool bGaugeOffsetReady;
    private Vector3 gaugeOffset;

    private uint randomState = 1u;

    public void Setup(LogProcessingManager _manager, Sprite _gaugePanelSprite, Sprite _gaugeTrackSprite, Sprite _gaugeFillSprite, Shader _gaugeShader)
    {
        manager = _manager;

        lines = GetComponentsInChildren<LogProcessLine>(true);
        System.Array.Sort(lines, (a, b) => a.LineIndex.CompareTo(b.LineIndex));

        sawdustAccum = new float[lines.Length];
        heatAccum = new float[lines.Length];

        if (vfx == null) vfx = GetComponentInChildren<VFX_SawmillCombo>(true);
        if (vfx == null) vfx = VFX_SawmillCombo.Create(transform);

        if (gauge == null) gauge = GetComponentInChildren<SawmillComboGauge>(true);
        if (gauge == null) gauge = SawmillComboGauge.Create(transform, _gaugePanelSprite, _gaugeTrackSprite, _gaugeFillSprite, _gaugeShader);

        randomState = unchecked((uint)Random.Range(1, int.MaxValue) * 2654435761u) | 1u;
        bSynced = false;
        bGaugeOffsetReady = false;
    }

    public void SetMapType(MapType _mapType)
    {
        mapType = _mapType;

        // 맵이 바뀌면 그리던 것을 지우고, 돌아왔을 때 그동안 바뀐 콤보를 한꺼번에 터뜨리지 않도록 다시 맞춘다.
        if (vfx != null) vfx.ClearAll();
        if (gauge != null) gauge.Clear();
        ClearTint();
        bSynced = false;
    }

    private void OnDisable()
    {
        if (vfx != null) vfx.ClearAll();
        if (gauge != null) gauge.Clear();
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
            if (gauge != null) gauge.SetState(false, Vector3.zero, stack, max);
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

        if (gauge != null) gauge.SetState(true, GetGaugeAnchor(), stack, max);

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

    private Vector3 GetGaugeAnchor()
    {
        LogContainer container = manager.logContainer;
        if (container == null) return transform.position;

        if (false == bGaugeOffsetReady)
        {
            // 상자 그림(켜져 있는 자식 스프라이트)의 위쪽 가운데. 상자는 움직이지 않으니 상자 위치 기준으로 한 번만 잰다.
            // 원목이 박힐 때 상자 그림이 짜부(visualTransform 크기 변화)하는 동안 재면 경계가 어긋나므로, 그동안은
            // 이번 프레임 값만 쓰고 확정하지 않는다(적재량별 상자 그림은 크기가 같아 그림이 바뀌어도 상관없다).
            bool bResting = container.visualTransform == null || container.visualTransform.localScale == Vector3.one;
            SpriteRenderer[] renderers = container.GetComponentsInChildren<SpriteRenderer>(false);
            bool bFirst = true;
            Bounds bounds = new Bounds(container.transform.position, Vector3.zero);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || renderers[i].sprite == null) continue;
                if (bFirst)
                {
                    bounds = renderers[i].bounds;
                    bFirst = false;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            gaugeOffset = new Vector3(bounds.center.x, bounds.center.y + bounds.extents.y * GaugeHeightRatio, 0f) - container.transform.position;
            gaugeOffset.z = 0f;
            bGaugeOffsetReady = !bFirst && bResting;
        }

        return container.transform.position + gaugeOffset;
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
