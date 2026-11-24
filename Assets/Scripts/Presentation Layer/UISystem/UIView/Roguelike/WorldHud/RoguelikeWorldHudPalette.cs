using UnityEngine;

/// <summary>
/// 월드 HUD 3톤 팔레트(밝은 줄 / 기본 / 그림자). world_hud_v6.py의 색 튜플과 같은 순서다.
/// </summary>
public readonly struct RoguelikeWorldHudTone
{
    public readonly Color32 light;
    public readonly Color32 main;
    public readonly Color32 shadow;

    public RoguelikeWorldHudTone(in Color32 _light, in Color32 _main, in Color32 _shadow)
    {
        light = _light;
        main = _main;
        shadow = _shadow;
    }

    public RoguelikeWorldHudTone(int _light, int _main, int _shadow)
    {
        light = RoguelikeWorldHudPalette.Hex(_light);
        main = RoguelikeWorldHudPalette.Hex(_main);
        shadow = RoguelikeWorldHudPalette.Hex(_shadow);
    }
}

/// <summary>
/// 로그라이크 월드 HUD 색(석호 팔레트, world_hud_v6.py 상수와 1:1). 반투명은 그림자 두 가지(띠 알파 80 / 숫자 · 대쉬 칸 알파 90)뿐이다.
/// 무적 3톤(#FFF0B8 / #F2CF5A / #B8902E)은 기존 로그라이크 HUD 값을 그대로 쓴다.
/// </summary>
public static class RoguelikeWorldHudPalette
{
    public static readonly Color32 White = Hex(0xFFFFFF);
    public static readonly Color32 Outline = Hex(0x0A3B46);
    public static readonly Color32 Segment = Hex(0x0A3B46);
    public static readonly Color32 Side1 = Hex(0x0F5260);
    public static readonly Color32 Side2 = Hex(0x0A3B46);
    public static readonly Color32 BandShadow = new Color32(5, 45, 55, 80);
    public static readonly Color32 LabelShadow = new Color32(5, 45, 55, 90);
    // 숫자 그림자(12장 B 단단한 그림자): 불투명 더 진한 남색. 대쉬 칸은 위의 LabelShadow(반투명)를 그대로 쓴다.
    public static readonly Color32 LabelShadowSolid = Hex(0x06222A);

    // 테: 먼 쪽 밝게 / 옆 / 가까운 쪽 어둡게
    public static readonly RoguelikeWorldHudTone Rim = new RoguelikeWorldHudTone(0x46BAC4, 0x2A97A3, 0x1C7884);
    // 초과 토막 테(산호)
    public static readonly RoguelikeWorldHudTone RimOver = new RoguelikeWorldHudTone(0xFF8A96, 0xE0566E, 0xB23A55);
    // 초과 토막 펄스 / 스태미너 강제 경고 테: 먼 쪽 흰색
    public static readonly RoguelikeWorldHudTone RimFlash = new RoguelikeWorldHudTone(0xFFFFFF, 0xFF8A96, 0xE0566E);
    // 손실 펄스 때 스태미너 띠 테: (흰, RIM_FAR, RIM) (world_hud_v7_motion.py srim)
    public static readonly RoguelikeWorldHudTone RimPulse = new RoguelikeWorldHudTone(0xFFFFFF, 0x46BAC4, 0x2A97A3);

    public static readonly RoguelikeWorldHudTone Empty = new RoguelikeWorldHudTone(0x1E7E8C, 0x166B79, 0x115C69);
    public static readonly RoguelikeWorldHudTone EmptyOver = new RoguelikeWorldHudTone(0x1B7280, 0x14616E, 0x0F5360);
    public static readonly RoguelikeWorldHudTone Amber = new RoguelikeWorldHudTone(0xFFF2A8, 0xFFC845, 0xE8902A);
    public static readonly RoguelikeWorldHudTone Hot = new RoguelikeWorldHudTone(0xFFC2B2, 0xFF5A63, 0xC9304C);
    // 초과 토막 펄스 채움: (WHITE, HOT[0], HOT[1])
    public static readonly RoguelikeWorldHudTone HotFlash = new RoguelikeWorldHudTone(0xFFFFFF, 0xFFC2B2, 0xFF5A63);
    public static readonly RoguelikeWorldHudTone Stamina = new RoguelikeWorldHudTone(0xE6FFA0, 0x9BEB3F, 0x52B52C);
    public static readonly RoguelikeWorldHudTone Trail = new RoguelikeWorldHudTone(0xFFFFFF, 0xDFFBF8, 0xA6E8E4);
    public static readonly RoguelikeWorldHudTone Invincible = new RoguelikeWorldHudTone(0xFFF0B8, 0xF2CF5A, 0xB8902E);
    public static readonly RoguelikeWorldHudTone WhiteTone = new RoguelikeWorldHudTone(0xFFFFFF, 0xFFFFFF, 0xFFFFFF);

    // 연출 입자 · 큰 숫자 색: 물방울 FOAM, 목표 달성 금색 (world_hud_v2.py FOAM, world_hud_v7_motion.py GOLD)
    public static readonly Color32 Foam = Hex(0xDDFBF6);
    public static readonly Color32 GoldLight = Hex(0xFFF6C8);
    public static readonly Color32 GoldMain = Hex(0xFFD24A);

    // 대쉬 칸
    public static readonly RoguelikeWorldHudTone Dash = new RoguelikeWorldHudTone(0xD2F6FF, 0x5CC8E2, 0x2E86A8);
    // 충전 중 채움: (DASH[1], DASH[2], DASH[2])
    public static readonly RoguelikeWorldHudTone DashCharging = new RoguelikeWorldHudTone(0x5CC8E2, 0x2E86A8, 0x2E86A8);

    public static Color32 Hex(int _rgb)
    {
        return new Color32((byte)((_rgb >> 16) & 0xFF), (byte)((_rgb >> 8) & 0xFF), (byte)(_rgb & 0xFF), 255);
    }
}
