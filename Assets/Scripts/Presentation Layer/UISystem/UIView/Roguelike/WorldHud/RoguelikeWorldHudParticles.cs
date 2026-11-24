using System;
using UnityEngine;

/// <summary>
/// 월드 HUD 연출 입자 풀(WorldHUD_Plan.md 10-3 / 10-4, world_hud_v7_motion.py spawn_burst / draw_particles).
/// 고정 크기 풀이라 할당이 없고, 난수(System.Random)는 만들 때 한 번만 만든다. 입자 모양은 '.'(1픽셀) / '+' / 'x'(5픽셀)뿐이다.
/// 입자는 위쪽 반원으로 퍼지고 중력(60 px/s^2)을 받으며, 수명 60% 뒤 흰색, 75% 뒤 1픽셀이 된다. 층(layer)마다 따로 그린다(띠 둘레 / 처치 숫자 둘레).
/// </summary>
public sealed class RoguelikeWorldHudParticles
{
    public const int Capacity = 128;
    public const int LayerCount = 2;
    public const int BandLayer = 0;
    public const int KillLayer = 1;

    private const double Gravity = 60d;
    private const double LifeMin = 0.28d;
    private const double LifeSpan = 0.17d;
    private const double WhiteAfter = 0.6d;
    private const double ShrinkAfter = 0.75d;
    // 모양: 0 = '.', 1 = '+', 2 = 'x'. 시뮬은 ['.', '.', '+', 'x']에서 고른다.
    private const int ShapeDot = 0;
    private const int ShapePlus = 1;
    private const int ShapeCross = 2;

    //내부 의존성
    private readonly System.Random random = new System.Random();

    // 입자 배열(고정 크기)
    private readonly bool[] alive = new bool[Capacity];
    private readonly byte[] layers = new byte[Capacity];
    private readonly byte[] shapes = new byte[Capacity];
    private readonly double[] startX = new double[Capacity];
    private readonly double[] startY = new double[Capacity];
    private readonly double[] velocityX = new double[Capacity];
    private readonly double[] velocityY = new double[Capacity];
    private readonly double[] born = new double[Capacity];
    private readonly double[] life = new double[Capacity];
    private readonly Color32[] colors = new Color32[Capacity];
    private readonly int[] aliveCounts = new int[LayerCount];

    /// <summary>_layer에 살아 있는 입자 수.</summary>
    public int AliveCount(int _layer)
    {
        return aliveCounts[_layer];
    }

    /// <summary>모든 입자를 지운다.</summary>
    public void Clear()
    {
        for (int i = 0; Capacity > i; i++) alive[i] = false;
        for (int i = 0; LayerCount > i; i++) aliveCounts[i] = 0;
    }

    /// <summary>
    /// 한 점에서 위쪽 반원으로 퍼지는 입자 _count개(spawn_burst). 색은 _c0 ~ _c2 중 _colorCount개에서 고른다. 풀이 가득 차면 남는 입자는 만들지 않는다.
    /// _speed = 최대 속도(px/s), _up = 위로 더하는 속도(px/s)의 3/10(시뮬의 up x 0.3).
    /// </summary>
    public void SpawnBurst(int _layer, int _x, int _y, double _now, int _count, in Color32 _c0, in Color32 _c1, in Color32 _c2, int _colorCount, double _speed, double _up)
    {
        for (int n = 0; _count > n; n++)
        {
            int slot = FindFreeSlot();
            if (0 > slot) return;

            double angle = -Math.PI * 0.95d + (-Math.PI * 0.05d - -Math.PI * 0.95d) * random.NextDouble();
            double speed = _speed * 0.5d + (_speed - _speed * 0.5d) * random.NextDouble();
            int shapePick = random.Next(4);
            int colorPick = random.Next(_colorCount);

            alive[slot] = true;
            layers[slot] = (byte)_layer;
            shapes[slot] = 2 > shapePick ? (byte)ShapeDot : (2 == shapePick ? (byte)ShapePlus : (byte)ShapeCross);
            startX[slot] = _x;
            startY[slot] = _y;
            velocityX[slot] = Math.Cos(angle) * speed;
            velocityY[slot] = Math.Sin(angle) * speed - _up * 0.3d;
            born[slot] = _now;
            life[slot] = LifeMin + LifeSpan * random.NextDouble();
            colors[slot] = 0 == colorPick ? _c0 : (1 == colorPick ? _c1 : _c2);
            aliveCounts[_layer]++;
        }
    }

    /// <summary>수명이 다한 입자를 풀에 돌려준다.</summary>
    public void Step(double _now)
    {
        for (int i = 0; Capacity > i; i++)
        {
            if (false == alive[i]) continue;
            if (life[i] >= _now - born[i]) continue;

            alive[i] = false;
            aliveCounts[layers[i]]--;
        }
    }

    /// <summary>_layer 입자를 버퍼(_rect, 아래에서 위 행 순서)에 그린다. 버퍼를 먼저 비운다. 시뮬 draw_particles와 같은 위치 · 모양 · 색 규칙이다.</summary>
    public void Paint(int _layer, double _now, Color32[] _buffer, in RoguelikeWorldHudRect _rect)
    {
        Array.Clear(_buffer, 0, _buffer.Length);

        for (int i = 0; Capacity > i; i++)
        {
            if (false == alive[i] || _layer != layers[i]) continue;

            double age = _now - born[i];
            if (0d > age || age > life[i]) continue;

            int x = (int)Math.Round(startX[i] + velocityX[i] * age);
            int y = (int)Math.Round(startY[i] + velocityY[i] * age + Gravity * age * age);
            Color32 color = age < life[i] * WhiteAfter ? colors[i] : RoguelikeWorldHudPalette.White;
            int shape = age > life[i] * ShrinkAfter ? ShapeDot : shapes[i];

            Plot(_buffer, in _rect, x, y, in color);
            if (ShapeDot == shape) continue;

            if (ShapePlus == shape)
            {
                Plot(_buffer, in _rect, x + 1, y, in color);
                Plot(_buffer, in _rect, x - 1, y, in color);
                Plot(_buffer, in _rect, x, y + 1, in color);
                Plot(_buffer, in _rect, x, y - 1, in color);
            }
            else
            {
                Plot(_buffer, in _rect, x + 1, y + 1, in color);
                Plot(_buffer, in _rect, x - 1, y - 1, in color);
                Plot(_buffer, in _rect, x + 1, y - 1, in color);
                Plot(_buffer, in _rect, x - 1, y + 1, in color);
            }
        }
    }

    private static void Plot(Color32[] _buffer, in RoguelikeWorldHudRect _rect, int _x, int _y, in Color32 _color)
    {
        int index = _rect.IndexOf(_x, _y);
        if (0 > index) return;

        _buffer[index] = _color;
    }

    private int FindFreeSlot()
    {
        for (int i = 0; Capacity > i; i++)
        {
            if (false == alive[i]) return i;
        }

        return -1;
    }
}
