using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 희귀 카드(에메랄드·프리즘·루비)의 픽셀 입자 이펙트. 카드 앞에 얹히는 한 장의 그래픽이 별가루·불씨·모서리 글린트·공개 스파크를 그린다.
/// 프로젝트의 다른 이펙트(전리품 진화·별 비·스태미너 바 회복)와 같은 규칙을 따른다.
///   - 모양은 . (1px · 2px), + 와 x (3px · 5px)뿐이다. 입자마다 회전 속도가 다르고, 45° 단위로 끊어 + 와 x 가 번갈아 돈다(점은 회전하지 않는다). 반짝은 1 → 3 → 5 → 3 → 1px로 팝한다.
///   - 모든 그림은 1px 단위 정수 사각형이고 항상 불투명이다(텍스처 없이 색 사각형 메쉬만 그린다). 위치는 시간으로 연속 계산해 그릴 때 정수 픽셀로 반올림한다.
///   - 색 계열마다 3톤이다. 입자는 나이가 들면 톤이 한 단계씩 내려가다 깜빡이며 사라진다. 프리즘은 스태미너 바의 프리즘과 같은 파스텔 6색 팔레트(색마다 3톤)를 쓴다.
///   - 깜빡임·톤 단계·팝·회전은 24Hz 틱으로 끊는다. 입자 풀을 돌려 쓰고(힙 할당 없음), unscaled 시간이라 timeScale 0에서도 움직인다.
///
/// [버스트] 공개가 끝나는 순간(진행도 1) 중심에서 방사형으로 튀는 . + x 입자와 네 모서리 글린트가 한 번 터진다(EmitBurst).
/// [공개] 카드가 앞면으로 드러나는 순간 프레임 셰이더(UI/CardGradeFrame)의 대각 광택 띠가 왼쪽 위에서 오른쪽 아래로 지나가며 레어로 바꾼다. SetRevealProgress(0~1)로 같은 진행도를 받아
///        그 띠 위에서 . + x 스파크를 흩날린다(띠 위치 = 셰이더와 같은 식: 대각 좌표 0.8x - y가 SweepRange 범위를 훑는다).
/// [등급별] 에메랄드(0, 실버) 은백 별가루·모서리 5px 글린트, 프리즘(1) 파스텔 6색 입자·팔레트 글린트, 루비(2) 불씨(. x).
/// [상호작용] SetIntensity(1)이면 호버(입자가 늘어난다), SetDim은 다른 카드 선택 중 어두워짐(톤을 어둡게, 여전히 불투명).
/// 좌표는 이 RectTransform의 왼쪽 아래 기준 정수 픽셀이다(카드 50x81).
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UI_RoguelikeCardAura : MaskableGraphic
{
    private enum EKind : byte
    {
        Dot1,
        Dot2,
        Glyph,
        Pop,
    }

    private struct Particle
    {
        public bool bAlive;
        public Vector2 position;
        public Vector2 velocity;
        public float age;
        public float life;
        public float drag;
        public float swayAmplitude;
        public float swayFrequency;
        public float swayPhase;
        public float spin;
        public int spinOffset;
        public EKind kind;
        public int family;
    }

    private const int CardWidth = 50;
    private const int CardHeight = 81;
    private const int Capacity = 128;
    private const float TicksPerSecond = 24f;
    private const float MaxStep = 1f / 20f;

    // 공개 띠가 훑는 대각 좌표 범위. 셰이더의 (_LineHalf.x * 0.8 + _LineHalf.y + _GlintWidth + 4)와 같은 값(22.5 * 0.8 + 38 + 5 + 4).
    private const float SweepRange = 65f;

    // 에메랄드(실버) / 루비 3톤(밝음 → 어두움). 프리즘은 아래 팔레트 6색을 색마다 3톤으로 쓴다.
    private static readonly Color32[] SilverTones =
    {
        new Color32(237, 245, 255, 255),
        new Color32(189, 209, 237, 255),
        new Color32(132, 158, 199, 255),
    };

    private static readonly Color32[] RubyTones =
    {
        new Color32(255, 184, 89, 255),
        new Color32(242, 82, 46, 255),
        new Color32(140, 26, 31, 255),
    };

    // 스태미너 바(HUD_Stemina)의 프리즘 팔레트: 민트, 하늘, 라벤더, 분홍, 살구, 연노랑.
    private static readonly Color32[] PrismBase =
    {
        new Color32(179, 245, 204, 255),
        new Color32(168, 235, 255, 255),
        new Color32(199, 189, 255, 255),
        new Color32(255, 184, 224, 255),
        new Color32(255, 204, 184, 255),
        new Color32(255, 242, 179, 255),
    };

    private static readonly Color32[,] PrismTones = BuildPrismTones();

    //상태 변수
    private readonly Particle[] particles = new Particle[Capacity];
    private int grade = -1;
    private float intensity;
    private float intensityTarget;
    private float dim;
    private float time;
    private float revealProgress = 1f;
    private float emitAccumulator;
    private float sparkAccumulator;
    private float cornerTimer0 = 0.5f;
    private float cornerTimer1 = 1.4f;
    private float cornerTimer2 = 2.3f;
    private float cornerTimer3 = 3.2f;
    private uint rng = 2463534242u;

    /// <summary>등급(0 에메랄드/실버, 1 프리즘, 2 루비). -1이면 이펙트를 끄고 입자를 모두 지운다.</summary>
    public void SetGrade(int _grade)
    {
        if (_grade == grade) return;

        grade = _grade;
        ClearParticles();
        time = 0f;
        SetVerticesDirty();
    }

    /// <summary>호버(1)·평소(0). 입자가 늘어난다.</summary>
    public void SetIntensity(float _target)
    {
        intensityTarget = Mathf.Clamp01(_target);
    }

    /// <summary>다른 카드가 선택 중일 때 어두워지는 정도(0~1). 톤을 어둡게 할 뿐 불투명은 유지한다.</summary>
    public void SetDim(float _dim)
    {
        if (Mathf.Approximately(dim, _dim)) return;

        dim = _dim;
        SetVerticesDirty();
    }

    /// <summary>공개 연출 진행도(0 = 시작, 1 = 끝/평소). 0과 1 사이인 동안 광택 띠가 지나가는 자리에서 스파크를 흩날린다.</summary>
    public void SetRevealProgress(float _progress)
    {
        float previous = revealProgress;
        revealProgress = Mathf.Clamp01(_progress);

        // 공개가 끝나는 순간(진행 중 → 1) 한 번 터진다.
        if (previous < 1f && 1f <= revealProgress && grade >= 0) EmitBurst();
    }

    /// <summary>입자·진행 상태를 모두 지운다(카드가 새로 채워질 때·꺼질 때).</summary>
    public void Clear()
    {
        grade = -1;
        intensity = 0f;
        intensityTarget = 0f;
        dim = 0f;
        revealProgress = 1f;
        ClearParticles();
        SetVerticesDirty();
    }

    /// <summary>편집기 검증용: 실제 시간 없이 _seconds초만큼 시뮬레이션한다.</summary>
    public void Simulate(float _seconds)
    {
        float remain = _seconds;
        while (0f < remain)
        {
            float dt = Mathf.Min(remain, 1f / 60f);
            Step(dt);
            remain -= dt;
        }
    }

    // // 시뮬레이션

    private void Step(float _dt)
    {
        time += _dt;
        intensity = Mathf.MoveTowards(intensity, intensityTarget, _dt * 4f);

        if (grade >= 0)
        {
            if (revealProgress < 1f) EmitSweepSparks(_dt);
            else Emit(_dt);
        }

        for (int i = 0; i < Capacity; i++)
        {
            if (false == particles[i].bAlive) continue;

            ref Particle p = ref particles[i];
            p.age += _dt;
            if (p.age >= p.life)
            {
                p.bAlive = false;
                continue;
            }

            p.velocity *= Mathf.Max(0f, 1f - p.drag * _dt);
            p.position += p.velocity * _dt;
            if (0f < p.swayAmplitude) p.position.x += Mathf.Sin(p.age * p.swayFrequency + p.swayPhase) * p.swayAmplitude * _dt;
        }

        SetVerticesDirty();
    }

    private void Emit(float _dt)
    {
        float boost = 1f + intensity * 0.8f;

        switch (grade)
        {
            case 0: EmitDust(_dt, boost, false); break;
            case 1: EmitDust(_dt, boost, true); break;
            default: EmitEmbers(_dt, boost); break;
        }

        if (2 == grade) return;

        TickCorner(ref cornerTimer0, _dt, boost, 3f, 3f);
        TickCorner(ref cornerTimer1, _dt, boost, 46f, 3f);
        TickCorner(ref cornerTimer2, _dt, boost, 3f, 77f);
        TickCorner(ref cornerTimer3, _dt, boost, 46f, 77f);
    }

    /// <summary>
    /// 레어 완성 순간의 버스트: 카드 중심에서 방사형으로 튀는 입자 24개(회전하는 + x · 점, 일부는 1 → 3 → 5 → 3 → 1px 팝)와 네 모서리의 5px 글린트.
    /// 등급 3톤을 따르고(프리즘은 6색 파스텔이 섞인다) 감속하며 퍼지다 톤이 내려가며 깜빡이다 사라진다.
    /// </summary>
    private void EmitBurst()
    {
        for (int i = 0; i < 24; i++)
        {
            float angle = Range(0f, 6.2832f);
            float speed = Range(50f, 140f);
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            Particle p = default;
            p.bAlive = true;
            p.position = new Vector2(CardWidth * 0.5f, CardHeight * 0.5f) + new Vector2(direction.x * Range(2f, 20f), direction.y * Range(2f, 34f));
            p.velocity = direction * speed;
            p.drag = 3f;
            p.life = Range(0.45f, 0.9f);
            float roll = Rand();
            p.kind = roll < 0.3f ? EKind.Dot1 : (roll < 0.45f ? EKind.Dot2 : (roll < 0.75f ? EKind.Glyph : EKind.Pop));
            p.spin = Range(3f, 9f);
            p.spinOffset = (int)(Rand() * 2f);
            p.family = 1 == grade ? (int)(Rand() * 6f) % 6 : 0;
            Spawn(in p);
        }

        SpawnCornerPop(3f, 3f);
        SpawnCornerPop(46f, 3f);
        SpawnCornerPop(3f, 77f);
        SpawnCornerPop(46f, 77f);
    }

    private void SpawnCornerPop(float _x, float _y)
    {
        Particle p = default;
        p.bAlive = true;
        p.position = new Vector2(_x, _y);
        p.life = 0.6f;
        p.kind = EKind.Pop;
        p.spin = Range(4f, 8f);
        p.spinOffset = (int)(Rand() * 2f);
        p.family = 1 == grade ? (int)(Rand() * 6f) % 6 : 0;
        Spawn(in p);
    }

    /// <summary>공개 연출 중: 광택 띠가 지나가는 자리(대각 좌표 0.8x - y = s)에서 . + x 스파크를 흩날린다.</summary>
    private void EmitSweepSparks(float _dt)
    {
        float sweep = Mathf.Lerp(-SweepRange, SweepRange, revealProgress);
        sparkAccumulator += _dt * 70f;

        while (1f <= sparkAccumulator)
        {
            sparkAccumulator -= 1f;

            float x = Range(0f, CardWidth);
            float y = 0.8f * (x - CardWidth * 0.5f) - sweep + CardHeight * 0.5f;
            if (y < -3f || y > CardHeight + 3f) continue;

            Particle p = default;
            p.bAlive = true;
            p.position = new Vector2(x, y);
            p.velocity = new Vector2(Range(-14f, 14f), Range(-14f, 14f));
            p.drag = 3f;
            p.life = Range(0.3f, 0.6f);
            float roll = Rand();
            p.kind = roll < 0.4f ? EKind.Dot1 : (roll < 0.55f ? EKind.Dot2 : EKind.Glyph);
            p.spin = Range(3f, 9f);
            p.spinOffset = (int)(Rand() * 2f);
            p.family = 1 == grade ? (int)(Rand() * 6f) % 6 : 0;
            Spawn(in p);
        }
    }

    /// <summary>에메랄드·프리즘의 별가루: . 와 회전하는 + x 가 천천히 오른다.</summary>
    private void EmitDust(float _dt, float _boost, bool _bPrism)
    {
        emitAccumulator += _dt * 9f * _boost;
        while (1f <= emitAccumulator)
        {
            emitAccumulator -= 1f;

            Particle p = default;
            p.bAlive = true;
            p.position = RandomCardPoint();
            p.velocity = new Vector2(Range(-2.5f, 2.5f), Range(4f, 11f));
            p.life = Range(1.5f, 2.6f);
            float roll = Rand();
            p.kind = roll < 0.4f ? EKind.Glyph : (roll < 0.55f && _bPrism ? EKind.Dot2 : EKind.Dot1);
            p.spin = Range(1.5f, 5f);
            p.spinOffset = (int)(Rand() * 2f);
            p.family = _bPrism ? (int)(Rand() * 6f) % 6 : 0;
            Spawn(in p);
        }
    }

    /// <summary>루비의 불씨: . 와 x 가 흔들리며 오른다(톤이 밝은 주황 → 붉은색 → 짙은 적색으로 내려간다).</summary>
    private void EmitEmbers(float _dt, float _boost)
    {
        emitAccumulator += _dt * 14f * _boost;
        while (1f <= emitAccumulator)
        {
            emitAccumulator -= 1f;

            Particle p = default;
            p.bAlive = true;
            p.position = new Vector2(Range(1f, 49f), Range(-2f, 28f));
            p.velocity = new Vector2(Range(-4f, 4f), Range(12f, 32f));
            p.drag = 0.3f;
            p.swayAmplitude = Range(2f, 5f);
            p.swayFrequency = Range(3f, 7f);
            p.swayPhase = Range(0f, 6.2832f);
            p.life = Range(0.9f, 1.7f);
            float roll = Rand();
            p.kind = roll < 0.25f ? EKind.Glyph : (roll < 0.45f ? EKind.Dot2 : EKind.Dot1);
            p.spin = Range(2f, 6f);
            p.spinOffset = 1;
            Spawn(in p);
        }
    }

    /// <summary>모서리 글린트: 타이머가 끝나면 그 모서리에서 1 → 3 → 5 → 3 → 1px로 팝하며 회전하는 + x 를 하나 띄운다.</summary>
    private void TickCorner(ref float _timer, float _dt, float _boost, float _x, float _y)
    {
        _timer -= _dt * _boost;
        if (_timer > 0f) return;

        _timer = Range(2.5f, 5.2f);
        SpawnCornerPop(_x, _y);
    }

    /// <summary>카드 주변의 무작위 점. 아이콘 칸(가운데 위쪽) 안은 피한다(몇 번 다시 뽑는다) - 입자가 아이콘을 덮지 않게.</summary>
    private Vector2 RandomCardPoint()
    {
        Vector2 point = Vector2.zero;
        for (int i = 0; i < 4; i++)
        {
            point = new Vector2(Range(-3f, 53f), Range(-2f, 83f));
            if (point.x < 6f || point.x > 44f || point.y < 36f || point.y > 74f) break;
        }

        return point;
    }

    private void Spawn(in Particle _particle)
    {
        for (int i = 0; i < Capacity; i++)
        {
            if (particles[i].bAlive) continue;

            particles[i] = _particle;
            particles[i].bAlive = true;
            return;
        }
    }

    private void ClearParticles()
    {
        for (int i = 0; i < Capacity; i++) particles[i].bAlive = false;
        emitAccumulator = 0f;
        sparkAccumulator = 0f;
    }

    // // 그리기

    protected override void OnPopulateMesh(VertexHelper _vh)
    {
        _vh.Clear();
        if (grade < 0) return;

        int originX = Mathf.RoundToInt(rectTransform.rect.min.x);
        int originY = Mathf.RoundToInt(rectTransform.rect.min.y);
        int tick = (int)(time * TicksPerSecond);

        for (int i = 0; i < Capacity; i++)
        {
            if (false == particles[i].bAlive) continue;

            ref Particle p = ref particles[i];
            float u = Mathf.Clamp01(p.age / p.life);

            // 수명 끝 무렵에는 3틱 중 1틱씩 꺼지며 깜빡이다 사라진다. 루비 불씨는 중간에도 가끔 깜빡인다.
            if (0.7f < u && 0 == (tick + i) % 3) continue;
            if (2 == grade && 0.35f < u && 0 == (tick + i) % 5) continue;

            int toneIndex = u < 0.4f ? 0 : (u < 0.75f ? 1 : 2);
            Color32 color = Shade(GetParticleTone(p.family, toneIndex));
            int cx = originX + Mathf.RoundToInt(p.position.x);
            int cy = originY + Mathf.RoundToInt(p.position.y);

            // 45° 단위 회전: 짝수 단계는 + , 홀수 단계는 x. 입자마다 속도(spin)와 시작 단계(spinOffset)가 다르다.
            bool bCross = 1 == ((int)(p.age * p.spin) + p.spinOffset) % 2;

            switch (p.kind)
            {
                case EKind.Dot1:
                    AddRect(_vh, cx, cy, 1, 1, color);
                    break;

                case EKind.Dot2:
                    if (u < 0.55f) AddRect(_vh, cx, cy, 2, 2, color);
                    else AddRect(_vh, cx, cy, 1, 1, color);
                    break;

                case EKind.Glyph:
                    DrawGlyph(_vh, cx, cy, u < 0.6f ? 1 : 0, bCross, color);
                    break;

                default:
                    // 1 → 3 → 5 → 3 → 1px
                    DrawGlyph(_vh, cx, cy, u < 0.12f ? 0 : (u < 0.3f ? 1 : (u < 0.6f ? 2 : (u < 0.8f ? 1 : 0))), bCross, color);
                    break;
            }
        }
    }

    /// <summary>+ 또는 x 글리프. _arm 0 = 1px 점, 1 = 3px, 2 = 5px.</summary>
    private static void DrawGlyph(VertexHelper _vh, int _cx, int _cy, int _arm, bool _bCross, Color32 _color)
    {
        if (_arm <= 0)
        {
            AddRect(_vh, _cx, _cy, 1, 1, _color);
            return;
        }

        if (false == _bCross)
        {
            AddRect(_vh, _cx - _arm, _cy, _arm * 2 + 1, 1, _color);
            AddRect(_vh, _cx, _cy - _arm, 1, _arm * 2 + 1, _color);
            return;
        }

        AddRect(_vh, _cx, _cy, 1, 1, _color);
        for (int k = 1; k <= _arm; k++)
        {
            AddRect(_vh, _cx + k, _cy + k, 1, 1, _color);
            AddRect(_vh, _cx - k, _cy + k, 1, 1, _color);
            AddRect(_vh, _cx + k, _cy - k, 1, 1, _color);
            AddRect(_vh, _cx - k, _cy - k, 1, 1, _color);
        }
    }

    private static void AddRect(VertexHelper _vh, int _x, int _y, int _w, int _h, Color32 _color)
    {
        int index = _vh.currentVertCount;
        _vh.AddVert(new Vector3(_x, _y), _color, Vector2.zero);
        _vh.AddVert(new Vector3(_x + _w, _y), _color, Vector2.zero);
        _vh.AddVert(new Vector3(_x + _w, _y + _h), _color, Vector2.zero);
        _vh.AddVert(new Vector3(_x, _y + _h), _color, Vector2.zero);
        _vh.AddTriangle(index, index + 1, index + 2);
        _vh.AddTriangle(index + 2, index + 3, index);
    }

    private Color32 GetParticleTone(int _family, int _tone)
    {
        switch (grade)
        {
            case 0: return SilverTones[_tone];
            case 1: return PrismTones[Mathf.Clamp(_family, 0, 5), _tone];
            default: return RubyTones[_tone];
        }
    }

    /// <summary>다른 카드가 선택 중일 때 톤을 어둡게(불투명 유지).</summary>
    private Color32 Shade(Color32 _color)
    {
        if (dim <= 0f) return _color;

        float factor = 1f - 0.4f * dim;
        return new Color32((byte)(_color.r * factor), (byte)(_color.g * factor), (byte)(_color.b * factor), 255);
    }

    private static Color32[,] BuildPrismTones()
    {
        Color32[,] tones = new Color32[6, 3];
        for (int i = 0; i < 6; i++)
        {
            Color32 c = PrismBase[i];
            tones[i, 0] = c;
            tones[i, 1] = new Color32((byte)(c.r * 0.78f), (byte)(c.g * 0.78f), (byte)(c.b * 0.78f), 255);
            tones[i, 2] = new Color32((byte)(c.r * 0.55f), (byte)(c.g * 0.55f), (byte)(c.b * 0.55f), 255);
        }

        return tones;
    }

    // // 난수

    private float Rand()
    {
        rng ^= rng << 13;
        rng ^= rng >> 17;
        rng ^= rng << 5;
        return (rng & 0xFFFFFFu) / 16777216f;
    }

    private float Range(float _min, float _max)
    {
        return _min + (_max - _min) * Rand();
    }

    private void Update()
    {
        if (false == Application.isPlaying) return;
        if (grade < 0) return;

        Step(Mathf.Min(Time.unscaledDeltaTime, MaxStep));
    }

    protected override void OnDisable()
    {
        ClearParticles();
        base.OnDisable();
    }
}
