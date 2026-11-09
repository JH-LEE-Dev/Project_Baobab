using UnityEngine;

/// <summary>
/// 돌풍 연출 설정. 돌풍 덩어리가 지나가는 자리의 나무가 잠깐 바람 쪽으로 눕고(TreeWindSway),
/// 잔디는 풀잎이 하나둘씩 바람 쪽으로 1텍셀 누웠다가 일어선다(GroundWindGustLit). 렌더링만 바꾸고 게임 동작에는 영향이 없다.
/// enabled가 꺼져 있으면 셰이더 출력이 기존과 같다.
/// </summary>
[System.Serializable]
public class WindGustSettings
{
    public bool enabled = false;
    // 바람이 부는 방향(월드). 정규화해서 쓴다.
    public Vector2 direction = new Vector2(1f, -0.35f);
    // 돌풍이 지나가는 속도(월드/초)
    public float speed = 3.2f;
    // 같은 줄에서 다음 돌풍까지의 간격(월드)
    public float spacing = 22f;
    // 돌풍 앞쪽 반폭(월드). 지나간 뒤 꼬리는 2.5배로 길게 빠진다.
    public float bandHalfWidth = 0.9f;
    // 바람과 수직 방향으로 끊기는 덩어리 크기(월드)
    public float patchSize = 3f;
    // 덩어리가 덮는 비율(0~1). 덩어리는 모두 고르게 세다.
    [Range(0f, 1f)] public float patchCoverage = 0.5f;
    // 돌풍 무리 길이(월드). 이 길이 단위로 돌풍이 몇 번 몰아치다 한동안 잠잠해진다(약한 돌풍 없이 불거나 쉬거나). 0이면 늘 분다.
    public float gustGroupLength = 40f;

    // 돌풍이 지날 때 나무가 바람 쪽으로 더 눕는 각도(도)
    public float treeLeanDegrees = 5.5f;
    // 돌풍 중 잔떨림 속도·비율
    public float treeFlutterSpeed = 9f;
    [Range(0f, 1f)] public float treeFlutterRatio = 0.35f;

    // 잔디: 돌풍을 바람과 함께 흘러가는 작은 덩어리로 다시 쪼갤 크기(월드). 이 덩어리 안에서만 풀이 반응한다.
    public float grassClumpSize = 1.6f;
    // 잔디: 풀잎이 바람 쪽으로 1텍셀 눕기 시작하는 가장 약한 돌풍 세기(0~1).
    [Range(0f, 1f)] public float grassBendThreshold = 0.2f;
    // 잔디: 풀잎마다 눕는 문턱이 퍼지는 폭. 문턱 = grassBendThreshold + 이 값 x (풀잎마다 0~1).
    // 1을 넘으면 돌풍 한가운데서도 일부 풀잎만 눕는다.
    public float grassBendSpread = 1.1f;
    // 잔디: 누운 풀잎이 밝아지는 비율(0.1 = 10%)
    [Range(0f, 0.5f)] public float grassBendBrightness = 0.18f;
    // 잔디 효과를 그리는 바닥 타일맵 머티리얼(GroundWindGustLit). 비워 두면 잔디는 그대로고 나무만 반응한다.
    public Material groundMaterial;
}

/// <summary>
/// 물 반짝임 연출 설정. 물 타일 셰이더(WaterTilemapShader, WaterURP)가 읽는다.
/// 물 타일 그림의 수면색으로 덧그릴 픽셀을 고르므로, 스테이지 물 타일 팔레트에 맞춰 둔다.
/// 렌더링만 바꾸고 게임 동작에는 영향이 없다. enabled가 꺼져 있으면 셰이더 출력이 기존과 같다.
/// </summary>
[System.Serializable]
public class WaterShimmerSettings
{
    public bool enabled = false;
    // 물 타일 그림의 수면색(판정용)
    public Color surfaceColor = new Color(85f / 255f, 196f / 255f, 200f / 255f, 1f);
    // 색 판정 허용 오차(채널별 최대 차이, 0~1)
    [Range(0f, 0.2f)] public float colorTolerance = 0.03f;
    // 반짝임 색
    public Color foamColor = new Color(0.92f, 1f, 1f, 1f);
    // 반짝임 한 점이 맡는 칸 크기(텍셀). 클수록 드물다.
    public float sparkleCellTexels = 16f;
    // 반짝임 한 점의 주기(초)와 그중 켜져 있는 비율. 켜진 동안 부드럽게 밝아졌다 사그라든다.
    public float sparklePeriod = 5f;
    [Range(0f, 1f)] public float sparkleDuty = 0.1f;
    // 반짝임 가운데 최고 밝기(HDR). 카메라 블룸 문턱(선형 약 1.9)을 넘는 만큼 번진다.
    public float sparkleIntensity = 2.6f;
    // 가운데 둘레 십자 팔의 최고 진하기(0이면 가운데 한 점만)
    [Range(0f, 1f)] public float sparkleArmStrength = 0.35f;
}

/// <summary>
/// 스테이지 전용 색보정. 기존 전역 후처리 볼륨 위에 이 프로필을 덧씌운다(밝은 곳은 따뜻하게, 어두운 곳은 차갑게,
/// 진한 색은 살짝 덜 진하게, 초록 기 도는 비네트). 렌더링만 바꾸고 게임 동작에는 영향이 없다.
/// 프로필에는 옵션 슬라이더가 쓰는 밝기·채도(ColorAdjustments)를 넣지 않는다(덮어쓰면 슬라이더가 안 먹는다).
/// </summary>
[System.Serializable]
public class StageColorGradingSettings
{
    public bool enabled = false;
    public UnityEngine.Rendering.VolumeProfile profile;
    // 덧씌우는 세기(0~1)
    [Range(0f, 1f)] public float weight = 1f;
}
