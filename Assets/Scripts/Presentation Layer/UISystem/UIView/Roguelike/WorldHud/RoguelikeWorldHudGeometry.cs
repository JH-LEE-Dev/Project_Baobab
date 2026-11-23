using UnityEngine;

/// <summary>화면(640x360, 왼쪽 위 원점, 아래로 +y) 정수 픽셀 사각형.</summary>
public readonly struct RoguelikeWorldHudRect
{
    public readonly int x;
    public readonly int y;
    public readonly int width;
    public readonly int height;

    public RoguelikeWorldHudRect(int _x, int _y, int _width, int _height)
    {
        x = _x;
        y = _y;
        width = _width;
        height = _height;
    }

    /// <summary>화면 좌표(_screenX, _screenY)가 사각형 안이면 Unity 텍스처 버퍼(아래에서 위 행 순서) 인덱스를, 밖이면 -1을 돌려준다.</summary>
    public int IndexOf(int _screenX, int _screenY)
    {
        int lx = _screenX - x;
        int ly = _screenY - y;
        if (0 > lx || width <= lx || 0 > ly || height <= ly) return -1;

        return (height - 1 - ly) * width + lx;
    }
}

/// <summary>
/// 월드 HUD 기하(world_hud_v6.py 상수 · 규칙). 좌표는 모두 640x360 기준 화면 픽셀이다(왼쪽 위 원점).
/// 좌상단 변 기하: 픽셀 (x, y)에서 A = x - 2y(변 방향), B = x + 2y(가로질러). 띠는 A0 &lt;= A &lt; A1, B0 &lt;= B &lt; B1.
/// 정렬 규칙(시안 v6): 임계치 띠(좌상단)와 스태미너 띠(우상단, 좌우 반전)는 전체 길이가 같다(A값 426). 두 띠는 섬 변에서 같은 거리(B 22)만큼 바깥이다.
/// 숫자 명패는 띠 아래쪽 끝 마구리 중심 높이에 맞추고 마구리 바깥 끝에서 4px 떨어진다(좌: 오른쪽 맞춤, 우: 왼쪽 맞춤).
/// 모든 멤버는 상수 · 읽기 전용이라 할당이 없다.
/// </summary>
public static class RoguelikeWorldHudGeometry
{
    public const int ScreenWidth = 640;
    public const int ScreenHeight = 360;
    public const int PixelsPerUnit = 32;

    // 띠 기하
    public const int EdgeTopLeft = 358;
    public const int GapB = 22;
    public const int Inner = 14;
    // 테 두께(윤곽 + 테), 1테 = 2값
    public const int RimThickness = 2;
    public const int B1 = EdgeTopLeft - GapB;
    public const int B0 = B1 - Inner - 4 * RimThickness;
    public const int A0 = -170;
    // 임계치 칸(나무 2그루) = 채움 8 + 칸막이 2
    public const int Pitch = 10;
    public const int Split = 14;
    public const int StaminaPitch = 42;
    public const int StaminaCells = 10;
    public const int Total = StaminaCells * StaminaPitch - 2 + 4 * RimThickness;
    public const int A1 = A0 + Total;
    /// <summary>스태미너 채움이 놓이는 띠 안쪽 길이(A값).</summary>
    public const int StaminaInner = Total - 4 * RimThickness;

    // 마구리(띠 아래쪽 끝) 기준 좌표. CapCy = round(((B0 + B1) / 2 - A0) / 4)
    public const int CapLeftX = (A0 + B0) / 2;
    public const int CapCy = ((B0 + B1) / 2 - A0 + 2) / 4;
    public const int LabelGap = 4;
    // 위쪽 숫자 행(v8): 좌상단 웨이브(x 6)와 위 가운데 남은 벌목 수(x 320 가운데)는 같은 y에 놓인다.
    public const int WaveX = 6;
    public const int WaveY = 3;

    // 숫자 명패
    public const int LabelBodyHeight = 15;
    public const int LabelShadowDrop = 2;
    public const int LabelRectHeight = LabelBodyHeight + LabelShadowDrop + 1;
    public const int LabelMaxWidth = 70;
    // 번개 아이콘 열 아래 대쉬 칸까지 간격
    public const int DashGap = 3;
    public const int DashRectHeight = 14;
    /// <summary>숫자 명패 위쪽 y(큰 숫자 세로 중심이 마구리 중심 CapCy).</summary>
    public const int LabelTop = CapCy - LabelBodyHeight / 2;
    /// <summary>오른쪽 숫자(스태미너) 왼쪽 끝 x. 왼쪽 숫자는 오른쪽 끝(제외)이 CapLeftX - LabelGap다.</summary>
    public const int LabelRightX = ScreenWidth - 1 - CapLeftX + LabelGap + 1;

    // 요소별 텍스처 사각형. 띠: 실루엣 + 두께 2 + 그림자 5가 들어가는 여유를 둔다(검증: 기준 레이어의 바깥 픽셀이 전부 투명).
    private const int BandMinX = (A0 + B0) / 2 - 2;
    private const int BandMaxX = (A1 + B1) / 2 + 2;
    private const int BandMinY = (B0 - A1) / 4 - 2;
    private const int BandMaxY = (B1 - A0) / 4 + 8;
    private const int DashLeftMargin = 2;
    // 연출 입자 텍스처 영역(WorldHUD_Plan.md 10-4): 띠 둘레(불꽃 · 물방울이 날아가는 범위) / 남은 벌목 수 둘레(목표 달성 반짝이, 11장)
    private const int FxBandMinX = 30;
    private const int FxBandWidth = 310;
    private const int FxBandHeight = 170;
    private const int FxKillMinX = 250;
    private const int FxKillMinY = 0;
    private const int FxKillWidth = 140;
    private const int FxKillHeight = 64;

    public static readonly RoguelikeWorldHudRect ThresholdBandRect = new RoguelikeWorldHudRect(BandMinX, BandMinY, BandMaxX - BandMinX, BandMaxY - BandMinY);
    public static readonly RoguelikeWorldHudRect StaminaBandRect = new RoguelikeWorldHudRect(ScreenWidth - BandMaxX, BandMinY, BandMaxX - BandMinX, BandMaxY - BandMinY);
    public static readonly RoguelikeWorldHudRect ThresholdLabelRect = new RoguelikeWorldHudRect(CapLeftX - LabelGap - LabelMaxWidth, LabelTop, LabelMaxWidth, LabelRectHeight);
    public static readonly RoguelikeWorldHudRect StaminaLabelRect = new RoguelikeWorldHudRect(LabelRightX - DashLeftMargin, LabelTop, LabelMaxWidth + DashLeftMargin, LabelBodyHeight + DashGap + DashRectHeight + LabelShadowDrop);
    public static readonly RoguelikeWorldHudRect WaveLabelRect = new RoguelikeWorldHudRect(WaveX, WaveY, LabelMaxWidth, LabelRectHeight);
    public static readonly RoguelikeWorldHudRect KillLabelRect = new RoguelikeWorldHudRect(ScreenWidth / 2 - LabelMaxWidth / 2, WaveY, LabelMaxWidth, LabelRectHeight);
    public static readonly RoguelikeWorldHudRect FxBandRect = new RoguelikeWorldHudRect(FxBandMinX, 0, FxBandWidth, FxBandHeight);
    public static readonly RoguelikeWorldHudRect FxKillRect = new RoguelikeWorldHudRect(FxKillMinX, FxKillMinY, FxKillWidth, FxKillHeight);

    /// <summary>
    /// 화면 픽셀 (_screenX, _screenY)의 월드 위치 오프셋(루트 기준, 왼쪽 위 모서리). 시안 좌표 (x, y)의 월드 위치 = 루트 + ((x - 320) / 32, (180 - y) / 32).
    /// </summary>
    public static Vector3 ToWorldOffset(int _screenX, int _screenY)
    {
        return new Vector3((_screenX - ScreenWidth / 2) / (float)PixelsPerUnit, (ScreenHeight / 2 - _screenY) / (float)PixelsPerUnit, 0f);
    }
}
