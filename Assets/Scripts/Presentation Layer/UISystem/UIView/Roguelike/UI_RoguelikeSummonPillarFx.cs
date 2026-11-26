using System;
using UnityEngine;

/// <summary>
/// 카드 선택 진입 연출 「빛기둥 소환」의 화면 단위 이펙트(승인 시안: Docs/Roguelike/_Work/CardSelectJuice/index.html, 시안 「pillar」).
/// 칸마다 한 줄기씩, 정해진 시각에 위 화면 끝에서 카드 아래 끝까지 빛기둥이 내리꽂힌다. 화면 번쩍 · 흔들림은 직접 하지 않고 StrikeEvent로 화면에 알린다.
///
/// 시간표(기둥이 치는 시각 = 0초, 칸마다 따로):
///   -0.20 ~ 0   예고  바닥선에 가로 빛 줄이 4 → 22px로 넓어지고(0.5 → 1.0 밝기), 바닥에서 빛 알갱이 `.`가 위로 피어오른다.
///   -0.10 ~ -0.03 (희귀만) 폭 4 흰 예고선
///   0           기둥 쾅  화면 위 끝 ~ 카드 아래 끝까지 기둥(일반 폭 18 · 0.5초 / 희귀 폭 30 · 0.75초). 폭은 (1 - k²)로 짝수 px씩 줄고 가운데에 절반 폭의 심, 바깥 1px은 어두운 색.
///               처음 0.1초는 3px 지그재그 번개. 바닥 납작 링(0.3초) + 입자 폭발(`+` `x` `.`, 일반 12 / 희귀 28).
///
/// 색: 일반 = 금 3톤(심 #fff4d8 · 몸통 #f3cc59 · 가장자리 #d9a640), 희귀 = 등급 팔레트(UI_RoguelikeRareRevealFx.GetGradeColors의 심 · 주색 · 어두운 색).
///
/// 그리는 방식(성능): 정수 사각형 UGUI 그래픽(UI_RoguelikeRareRevealParticles) 두 장 - 뒤(카드 뒤: 예고 줄 · 피어오르는 알갱이 · 바닥 링), 앞(카드 앞: 기둥 · 심 · 번개 · 폭발 입자).
/// 30Hz 틱이 바뀔 때만 다시 채우고 Commit한다. 입자는 해시 난수로 시간에서 바로 계산하므로 입자 상태 배열이 없고, 초기화 이후 힙 할당이 없다. unscaled 시간.
///
/// 좌표: 두 그래픽은 Content 바로 아래 가운데 앵커 · 크기 0이라, 정점 좌표 = 슬롯 anchoredPosition과 같은 Content 기준 px(y는 위쪽이 +)다.
/// </summary>
[DisallowMultipleComponent]
public class UI_RoguelikeSummonPillarFx : MonoBehaviour
{
    public const int Capacity = 8;

    private const float TickRate = 30f;
    private const float GlowMax = 2.5f;                 // UIPixelGlow의 _GlowMax와 같아야 한다
    private const float TeleSeconds = 0.2f;
    private const float RarePreLeadSeconds = 0.1f;
    private const float RarePreLifeSeconds = 0.07f;
    private const int RarePreWidth = 4;
    private const float NormalLifeSeconds = 0.5f;
    private const float RareLifeSeconds = 0.75f;
    private const int NormalWidth = 18;
    private const int RareWidth = 30;
    private const float BoltSeconds = 0.1f;
    private const int BoltBlock = 3;
    private const float PillarOpacity = 0.85f;
    // 폭발 입자의 최대 수명(0.35 + 0.4)이 다 지나는 시각. 이후 칸을 비운다.
    private const float EndSeconds = 0.8f;
    private const int NormalBurstCount = 12;
    private const int RareBurstCount = 28;
    private const float NormalBurstSpeed = 80f;
    private const float RareBurstSpeed = 130f;
    private const float BurstGravity = 60f;
    private const float BurstLift = 40f;
    private const float BurstDamping = 1.8971f;         // vx *= 0.15^t → -ln(0.15)
    private const float RingLifeSeconds = 0.3f;
    private const float NormalRingSpeed = 100f;
    private const float RareRingSpeed = 160f;
    private const float RingStartRadius = 4f;
    private const float TeleLifeSeconds = 0.3f;
    private const int TeleMinRadius = 4;
    private const int TeleRadiusGrow = 18;
    private const int TeleEndPixels = 3;
    private const float NormalMoteRate = 40f;
    private const float RareMoteRate = 80f;
    private const float MoteSpread = 40f;
    private const float StrikeCueStaleSeconds = 0.25f;

    private static readonly Color32 White = new Color32(255, 255, 255, 255);
    private static readonly Color32 NormalMain = new Color32(243, 204, 89, 255);
    private static readonly Color32 NormalCore = new Color32(255, 244, 216, 255);
    private static readonly Color32 NormalDeep = new Color32(217, 166, 64, 255);

    private struct Pillar
    {
        public bool bActive;
        public bool bStruck;
        public bool bRare;
        public int grade;
        public int seed;
        public float strikeAt;
        public float centerX;
        public float bottomY;
        public float topY;
    }

    /// <summary>기둥이 쳤다(슬롯 번호, 희귀 여부). 화면이 번쩍 · 흔들림을 건다. 색은 LastStrikeColor.</summary>
    public event Action<int, bool> StrikeEvent;

    //외부 의존성 (프리팹에서 연결)
    [SerializeField, Tooltip("뒤 층(카드 뒤) 그래픽: 예고 줄 · 피어오르는 알갱이 · 바닥 링")]
    private UI_RoguelikeRareRevealParticles backGraphic;
    [SerializeField, Tooltip("앞 층(카드 앞) 그래픽: 기둥 · 심 · 번개 · 폭발 입자")]
    private UI_RoguelikeRareRevealParticles frontGraphic;

    //상태 변수
    private readonly Pillar[] pillars = new Pillar[Capacity];
    private bool bActive;
    private float time;
    private int lastTick = int.MinValue;
    private int playCount;
    private Color32 lastStrikeColor = NormalMain;

    /// <summary>재생 중(지연 대기 포함)인 기둥이 하나라도 있으면 true.</summary>
    public bool IsPlaying => bActive;

    /// <summary>가장 최근에 친 기둥의 주색(화면 번쩍 색).</summary>
    public Color32 LastStrikeColor => lastStrikeColor;

    // 퍼블릭 초기화 및 제어 메서드

    /// <summary>
    /// _slot번 칸에 기둥 하나를 예약한다. _strikeDelay초 뒤에 기둥이 치고, 그 0.2초 전부터 예고가 시작된다(지연이 0.2초보다 짧으면 예고가 잘린다).
    /// _bottomCenter = 카드 아래 가운데(Content 기준 px), _topY = 화면 위 끝(Content 기준 px), _gradeIndex = 희귀 등급 칸(0 에메랄드 · 1 프리즘 · 2 루비, 일반은 -1).
    /// 같은 칸에 다시 부르면 그 칸의 기둥만 새로 시작한다(칸별 리롤).
    /// </summary>
    public void Play(int _slot, Vector2 _bottomCenter, float _topY, float _strikeDelay, bool _bRare, int _gradeIndex)
    {
        if (0 > _slot || Capacity <= _slot) return;

        playCount++;
        ref Pillar pillar = ref pillars[_slot];
        pillar.bActive = true;
        pillar.bStruck = false;
        pillar.bRare = _bRare;
        pillar.grade = _gradeIndex;
        pillar.seed = playCount;
        pillar.strikeAt = time + Mathf.Max(0f, _strikeDelay);
        pillar.centerX = Mathf.Round(_bottomCenter.x);
        pillar.bottomY = Mathf.Round(_bottomCenter.y);
        pillar.topY = Mathf.Round(_topY);

        bActive = true;
        lastTick = int.MinValue;
    }

    /// <summary>모든 기둥을 즉시 멈추고 그림을 지운다.</summary>
    public void StopAll()
    {
        for (int i = 0; i < Capacity; i++) pillars[i].bActive = false;

        bActive = false;
        lastTick = int.MinValue;
        if (null != backGraphic) backGraphic.ClearQuads();
        if (null != frontGraphic) frontGraphic.ClearQuads();
    }

    // 일반 비즈니스 로직 및 내부 메서드

    private void CheckStrikes()
    {
        for (int i = 0; i < Capacity; i++)
        {
            ref Pillar pillar = ref pillars[i];
            if (false == pillar.bActive || true == pillar.bStruck || time < pillar.strikeAt) continue;

            pillar.bStruck = true;
            GetMainColor(in pillar, out lastStrikeColor);

            // [효과음 지점] 기둥 쾅 = 일반 AbilityAppear, 희귀 RareRevealChime(임시 재사용 - 전용 클립 수급 후 교체). 예고(바닥 줄 · 희귀 흰 예고선)는 소리 없음.
            if (time - pillar.strikeAt <= StrikeCueStaleSeconds) Sound.PlayUI(true == pillar.bRare ? SoundID.RareRevealChime : SoundID.AbilityAppear);

            StrikeEvent?.Invoke(i, pillar.bRare);
        }
    }

    private void GetMainColor(in Pillar _pillar, out Color32 _main)
    {
        if (false == _pillar.bRare)
        {
            _main = NormalMain;
            return;
        }

        UI_RoguelikeRareRevealFx.GetGradeColors(Mathf.Max(0, _pillar.grade), time, out _main, out _, out _);
    }

    private void Rebuild()
    {
        backGraphic.BeginQuads();
        frontGraphic.BeginQuads();

        bool bAny = false;
        for (int i = 0; i < Capacity; i++)
        {
            ref Pillar pillar = ref pillars[i];
            if (false == pillar.bActive) continue;

            float t = time - pillar.strikeAt;
            if (EndSeconds < t)
            {
                pillar.bActive = false;
                continue;
            }

            bAny = true;
            DrawPillar(in pillar, t);
        }

        backGraphic.Commit();
        frontGraphic.Commit();

        if (false == bAny) bActive = false;
    }

    private void DrawPillar(in Pillar _pillar, float _t)
    {
        Color32 main;
        Color32 core;
        Color32 deep;
        if (true == _pillar.bRare)
        {
            UI_RoguelikeRareRevealFx.GetGradeColors(Mathf.Max(0, _pillar.grade), time, out main, out core, out deep);
        }
        else
        {
            main = NormalMain;
            core = NormalCore;
            deep = NormalDeep;
        }

        DrawTele(in _pillar, _t, main);
        if (true == _pillar.bRare) DrawRarePre(in _pillar, _t);
        if (0f > _t) return;

        DrawStrikeColumn(in _pillar, _t, main, core, deep);
        DrawRing(in _pillar, _t, main);
        DrawBurst(in _pillar, _t, main);
    }

    // 예고: 바닥 가로 빛 줄(4 → 22px, 밝기 0.5 → 1.0) + 피어오르는 알갱이(`.`). 알갱이는 기둥이 친 뒤에도 수명이 끝날 때까지 올라간다.
    private void DrawTele(in Pillar _pillar, float _t, Color32 _main)
    {
        float bottom = _pillar.bottomY;
        float centerX = _pillar.centerX;

        if (-TeleSeconds <= _t && 0f > _t)
        {
            float tele = (_t + TeleSeconds) / TeleSeconds;
            int radius = Mathf.RoundToInt(TeleMinRadius + TeleRadiusGrow * tele);
            Color32 color = Fade(_main, 0.5f + 0.5f * tele);
            // 가운데는 카드 아래 한 줄 아래, 양 끝 3px은 카드 바닥선(위로 한 칸 올라선 모양). 폭 = 2 x 반지름(짝수).
            backGraphic.Rect(centerX - radius + TeleEndPixels, bottom - 2f, centerX + radius - TeleEndPixels, bottom - 1f, color);
            backGraphic.Rect(centerX - radius, bottom - 1f, centerX - radius + TeleEndPixels, bottom, color);
            backGraphic.Rect(centerX + radius - TeleEndPixels, bottom - 1f, centerX + radius, bottom, color);
        }

        float rate = true == _pillar.bRare ? RareMoteRate : NormalMoteRate;
        int count = Mathf.RoundToInt(rate * TeleSeconds);
        int seedBase = _pillar.seed * 1024;
        for (int m = 0; m < count; m++)
        {
            float age = _t - (-TeleSeconds + m / rate);
            if (0f > age || TeleLifeSeconds <= age) continue;

            float x = centerX + (Rnd(seedBase + m * 4) - 0.5f) * MoteSpread;
            float speed = 40f + Rnd(seedBase + m * 4 + 1) * 40f;
            float opacity = Mathf.Min(1f, (1f - age / TeleLifeSeconds) * 1.6f);
            Dot(backGraphic, 0, Mathf.RoundToInt(x), Mathf.RoundToInt(bottom - 2f + speed * age), Fade(_main, opacity));
        }
    }

    // 희귀 예고선: 기둥 0.1초 전에 폭 4 흰 줄이 0.07초 동안 번쩍인다.
    private void DrawRarePre(in Pillar _pillar, float _t)
    {
        float k = (_t + RarePreLeadSeconds) / RarePreLifeSeconds;
        if (0f > k || 1f < k) return;

        int width = ToEvenWidth(RarePreWidth * (1f - k * k));
        if (2 > width) return;

        // [효과음 지점] 희귀 예고선(틱) - 전용 클립 수급 후 연결
        Color32 color = Fade(White, PillarOpacity * (1f - k));
        frontGraphic.Rect(_pillar.centerX - width * 0.5f, _pillar.bottomY, _pillar.centerX + width * 0.5f, _pillar.topY, color);
    }

    // 기둥: 폭 (1 - k²) 짝수 px, 알파 0.85 x (1 - k). 바깥 1px 어두운 색 · 몸통 · 가운데 절반 폭 심(겹치지 않게 다섯 조각). 처음 0.1초는 3px 지그재그 번개.
    private void DrawStrikeColumn(in Pillar _pillar, float _t, Color32 _main, Color32 _core, Color32 _deep)
    {
        float life = true == _pillar.bRare ? RareLifeSeconds : NormalLifeSeconds;
        if (life < _t) return;

        float k = _t / life;
        int width = ToEvenWidth((true == _pillar.bRare ? RareWidth : NormalWidth) * (1f - k * k));
        if (2 > width) return;

        float opacity = PillarOpacity * (1f - k);
        float left = _pillar.centerX - width * 0.5f;
        float right = left + width;
        float bottom = _pillar.bottomY;
        float top = _pillar.topY;

        if (6 > width)
        {
            frontGraphic.Rect(left, bottom, right, top, Fade(_main, opacity));
        }
        else
        {
            int coreWidth = ToEvenWidth(width * 0.5f);
            float coreLeft = _pillar.centerX - coreWidth * 0.5f;
            float coreRight = coreLeft + coreWidth;
            Color32 deepColor = Fade(_deep, opacity);
            Color32 mainColor = Fade(_main, opacity);
            frontGraphic.Rect(left, bottom, left + 1f, top, deepColor);
            frontGraphic.Rect(right - 1f, bottom, right, top, deepColor);
            frontGraphic.Rect(left + 1f, bottom, coreLeft, top, mainColor);
            frontGraphic.Rect(coreRight, bottom, right - 1f, top, mainColor);
            frontGraphic.Rect(coreLeft, bottom, coreRight, top, Fade(_core, opacity));
        }

        if (BoltSeconds <= _t) return;

        // 번개: 위에서 아래로 3px 블록이 좌우로 흔들리며 내려온다(시안 drawTopCols). 틱마다 모양이 바뀐다.
        Color32 boltColor = Fade(White, opacity);
        int tickSeed = _pillar.seed * 7919 + lastTick * 131;
        float x = _pillar.centerX;
        int step = 0;
        for (float y = top; y > bottom; y -= BoltBlock)
        {
            x += Mathf.Round((Rnd(tickSeed + step) - 0.5f) * 6f);
            frontGraphic.Rect(x - 1f, Mathf.Max(bottom, y - BoltBlock), x + 2f, y, boltColor);
            step++;
        }
    }

    // 바닥 납작 링: 반지름 4에서 초당 100(희귀 160)씩 퍼지는 한 줄 점선(2px 간격 1px 점), 0.3초, 알파 0.8 x (1 - age).
    private void DrawRing(in Pillar _pillar, float _t, Color32 _main)
    {
        if (RingLifeSeconds <= _t) return;

        float speed = true == _pillar.bRare ? RareRingSpeed : NormalRingSpeed;
        int radius = Mathf.RoundToInt(RingStartRadius + speed * _t);
        Color32 color = Fade(_main, 0.8f * (1f - _t / RingLifeSeconds));
        int y = Mathf.RoundToInt(_pillar.bottomY) - 1;
        int centerX = Mathf.RoundToInt(_pillar.centerX);
        for (int k = -radius; k <= radius; k += 2) backGraphic.Rect(centerX + k, y, centerX + k + 1, y + 1, color);
    }

    // 입자 폭발: 중심 바닥에서 사방으로(위쪽으로 살짝 치우침) 퍼져 중력으로 떨어진다. 가로 속도는 0.15^t로 줄어든다. 모양 `+` `x` `.` `.`, 색 주색 / 흰색.
    private void DrawBurst(in Pillar _pillar, float _t, Color32 _main)
    {
        // 입자는 틱마다 30fps로 끊어 움직인다.
        float age = Mathf.Floor(_t * TickRate) / TickRate;
        bool bRare = _pillar.bRare;
        int count = true == bRare ? RareBurstCount : NormalBurstCount;
        float baseSpeed = true == bRare ? RareBurstSpeed : NormalBurstSpeed;
        float damping = (1f - Mathf.Exp(-BurstDamping * age)) / BurstDamping;
        int seedBase = _pillar.seed * 2048 + 512;

        for (int j = 0; j < count; j++)
        {
            int n = seedBase + j * 8;
            float life = 0.35f + Rnd(n + 2) * 0.4f;
            if (life <= age) continue;

            float angle = Rnd(n) * Mathf.PI * 2f;
            float speed = baseSpeed * (0.4f + Rnd(n + 1) * 0.8f);
            float vx = Mathf.Cos(angle) * speed;
            float vy = Mathf.Sin(angle) * speed - BurstLift;
            float x = _pillar.centerX + vx * damping;
            float down = vy * age + 0.5f * BurstGravity * age * age;
            int shape = Mathf.Min(3, (int)(Rnd(n + 3) * 4f));
            Color32 rgb = Rnd(n + 4) < 0.5f ? _main : White;
            float opacity = Mathf.Min(1f, (1f - age / life) * 1.6f);
            Dot(frontGraphic, 1 == shape ? 1 : (0 == shape ? 2 : 0), Mathf.RoundToInt(x), Mathf.RoundToInt(_pillar.bottomY - down), Fade(rgb, opacity));
        }
    }

    // 픽셀 입자: 0 = `.`(1칸), 1 = `x`(대각 5칸), 2 = `+`(십자 5칸). (_x, _y)가 가운데 칸의 왼쪽 아래 모서리다.
    private static void Dot(UI_RoguelikeRareRevealParticles _graphic, int _shape, int _x, int _y, Color32 _color)
    {
        switch (_shape)
        {
            case 1:
                _graphic.Rect(_x - 1, _y - 1, _x, _y, _color);
                _graphic.Rect(_x + 1, _y - 1, _x + 2, _y, _color);
                _graphic.Rect(_x, _y, _x + 1, _y + 1, _color);
                _graphic.Rect(_x - 1, _y + 1, _x, _y + 2, _color);
                _graphic.Rect(_x + 1, _y + 1, _x + 2, _y + 2, _color);
                break;
            case 2:
                _graphic.Rect(_x - 1, _y, _x, _y + 1, _color);
                _graphic.Rect(_x + 1, _y, _x + 2, _y + 1, _color);
                _graphic.Rect(_x, _y - 1, _x + 1, _y + 2, _color);
                break;
            default:
                _graphic.Rect(_x, _y, _x + 1, _y + 1, _color);
                break;
        }
    }

    /// <summary>색 그대로 불투명도 _opacity(0 ~ 1)로 칠한다. UIPixelGlow 약속: 정점 알파 0.4 = 발광 1.0(색 그대로).</summary>
    private static Color32 Fade(Color32 _rgb, float _opacity)
    {
        _rgb.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(_opacity) * 255f / GlowMax);
        return _rgb;
    }

    /// <summary>폭을 0 이상의 짝수 px로 맞춘다(가운데 기준이라 홀수 폭은 가장자리가 반 픽셀에 걸린다).</summary>
    private static int ToEvenWidth(float _width)
    {
        return Mathf.Max(0, Mathf.FloorToInt(_width * 0.5f + 0.5f) * 2);
    }

    // 정수 해시 난수 0 이상 1 미만(상태 없음: 같은 입력이면 같은 값).
    private static float Rnd(int _n)
    {
        uint h = (uint)_n;
        h = (h ^ 61u) ^ (h >> 16);
        h *= 9u;
        h ^= h >> 4;
        h *= 0x27d4eb2du;
        h ^= h >> 15;
        return (h & 0xFFFFFFu) / 16777216f;
    }

    // 유니티 라이프사이클 이벤트 함수

    private void Update()
    {
        if (false == bActive || null == backGraphic || null == frontGraphic) return;

        time += Time.unscaledDeltaTime;
        CheckStrikes();

        int tick = Mathf.FloorToInt(time * TickRate);
        if (tick == lastTick) return;

        lastTick = tick;
        Rebuild();
    }

    private void OnDisable()
    {
        StopAll();
    }

    private void OnDestroy()
    {
        StrikeEvent = null;
    }
}
