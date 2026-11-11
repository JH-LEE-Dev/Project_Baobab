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
    // 홀로 떨어진 작은 덩어리(나무 몇 그루만 흔드는 작은 돌풍)만 넓히는 배수. 잔디도 같은 덩어리를 따라 함께 넓어진다.
    // 1이면 그대로이고, 여러 덩어리가 이어진 큰 돌풍은 바뀌지 않는다.
    [Range(1f, 2f)] public float smallPatchScale = 1f;
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

    // 물가 테두리 달빛 물결: 물 타일 그림의 테두리색 픽셀 위로 옅은 빛띠가 천천히 지나가며 테두리를 따라 미끄러지듯 밝힌다.
    // 꺼져 있으면 테두리는 기존과 같다.
    public bool rimGlowEnabled = false;
    // 물 타일 그림의 테두리색(판정용, 허용 오차는 colorTolerance를 같이 쓴다)
    public Color rimColor = new Color(43f / 255f, 120f / 255f, 202f / 255f, 1f);
    // 빛띠가 지날 때 테두리가 물드는 색
    public Color rimGlowColor = new Color(0.62f, 0.86f, 1f, 1f);
    // 빛띠 한가운데서 테두리가 rimGlowColor로 물드는 비율(0~1)
    [Range(0f, 1f)] public float rimGlowStrength = 0.6f;
    // 빛띠가 흘러가는 방향(월드). 정규화해서 쓴다.
    public Vector2 rimWaveDirection = new Vector2(1f, 0f);
    // 빛띠 사이 간격(월드)
    public float rimWaveSpacing = 9f;
    // 한 자리에 빛띠가 다시 오기까지 걸리는 시간(초)
    public float rimWavePeriod = 6f;
    // 간격 가운데 빛띠가 차지하는 비율(0~1)
    [Range(0.01f, 1f)] public float rimWaveWidth = 0.2f;
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

/// <summary>
/// 물가 갈대. 맵을 깔 때 육지를 등진 물가(화면 위쪽이 육지인 물 칸)에 갈대를 덤불로 무리 지어 듬성듬성 놓는다.
/// 충돌이 없는 장식이라 게임 동작에는 영향이 없다. prefab이 비어 있거나 enabled가 꺼져 있으면 아무것도 놓지 않는다.
/// </summary>
[System.Serializable]
public class ShoreReedSettings
{
    public bool enabled = false;
    // 갈대 프리팹(HomeWaterGrass: 3프레임 흔들림 + 정렬)
    public GameObject prefab;
    // 갈대에 끼울 머티리얼. 비워 두면 프리팹 머티리얼 그대로다(ShoreReedWindGust면 돌풍이 지날 때 바람 쪽으로 눕는다).
    public Material material;
    // 바다 물가에도 놓는다(꺼 두면 섬 안 웅덩이 물가에만).
    public bool includeSea = true;
    // 바다: 놓을 수 있는 물가 칸 하나당 무리 수(기댓값)
    [Range(0f, 1f)] public float clusterDensity = 0.075f;
    // 웅덩이: 웅덩이 물 칸 하나당 무리 수(기댓값). 큰 웅덩이일수록 무리가 늘고, 작은 웅덩이도 1무리는 노린다.
    [Range(0f, 1f)] public float pondClusterDensity = 0.04f;
    // 무리끼리 떨어뜨릴 최소 거리(월드)
    public float minClusterSpacing = 4f;
    // 무리 하나의 갈대 수. 이어진 물가가 짧아 최소 수를 못 채우는 자리에는 놓지 않는다.
    [Min(1)] public int clusterSizeMin = 2;
    [Min(1)] public int clusterSizeMax = 3;
    // 물가선을 따라 이웃 갈대 사이 간격(칸 단위). 칸마다 두 개씩 놓인다.
    public float alongBankSpacing = 0.5f;
    // 물 칸 수면 중심에서 뒤쪽 육지 쪽으로 미는 정도(칸 단위, 최소~최대)
    public Vector2 shoreOffsetRange = new Vector2(0.2f, 0.4f);
    // 그림이 왼쪽으로 눕는 갈대를 바람이 오른쪽으로 불면 좌우를 뒤집어, 흔들림이 바람 쪽으로 눕게 한다.
    public bool flipToWind = true;
}

/// <summary>
/// 포자 안개. 바닥 위로 낮게 깔린 안개 덩어리가 천천히 흘러가며 모양을 바꾼다(SporeHaze 셰이더).
/// 카메라를 따라다니는 스프라이트 한 장으로 그리며 충돌·판정이 없어 게임 동작에는 영향이 없다.
/// enabled가 꺼져 있거나 material이 비어 있으면 아무것도 만들지 않는다.
/// </summary>
[System.Serializable]
public class SporeHazeSettings
{
    public bool enabled = false;
    // 안개 머티리얼(SporeHaze). 색·진하기·덩어리 크기·흐름은 머티리얼에서 정한다.
    public Material material;
    // 그리는 정렬 자리. 기본은 바닥 타일 바로 위, 표시기·그림자·캐릭터·나무 아래.
    public string sortingLayerName = "Indicators";
    public int sortingOrder = -32768;
    // 머티리얼이 화면 캡처(_CameraSortingLayerTexture)를 읽는지(용암 아지랑이 등). 켜면 떠 있는 동안 캡처를 켜 둔다.
    public bool usesScreenCapture = false;
}

/// <summary>
/// 포자 나무 숨빛. 나무 윗부분 그림의 밝은 포자 알(하이라이트) 픽셀만 숲을 천천히 지나가는 빛의 물결에 맞춰
/// 밝아졌다 어두워진다(나무 셰이더 Custom-Sprite-Default-Tree). 렌더링만 바뀌고 게임 동작에는 영향이 없다.
/// </summary>
[System.Serializable]
public class SporeGlowSettings
{
    public bool enabled = false;
    // 가장 밝을 때 더하는 밝기 비율(0.6이면 1.6배). 블룸 문턱을 넘는 만큼 은은히 번진다.
    public float glowAmount = 0.6f;
    // 물결 속도(라디안/초). 한 자리가 밝아졌다 돌아오는 데 2π/속도 초가 걸린다.
    public float waveSpeed = 1.2f;
    // 물결 크기(월드). 이 거리 안의 나무끼리 박자가 비슷해 빛이 번져 가는 것처럼 보인다.
    public float waveScale = 6f;
    // 포자 알로 볼 픽셀: 가장 어두운 채널이 이 값 이상(sRGB 0~1)이고 채널 차이가 maxSaturation 이하인 밝고 옅은 색.
    [Range(0f, 1f)] public float minWhite = 0.78f;
    [Range(0f, 1f)] public float maxSaturation = 0.28f;
    // 클수록 밝아지는 순간이 짧고 또렷하다(1이면 사인 그대로).
    [Min(1f)] public float sharpness = 2f;
}

/// <summary>
/// 물 위 포자. 물 타일 그림의 수면색 픽셀 위에 빛나는 포자 알갱이가 떠올라 바람 방향으로 천천히 떠가다 사라진다
/// (물 타일 셰이더 WaterTilemapShader). 렌더링만 바뀌고 게임 동작에는 영향이 없다.
/// </summary>
[System.Serializable]
public class WaterSporeSettings
{
    public bool enabled = false;
    // 물 타일 그림의 수면색 두 가지(판정용)
    public Color surfaceColorA = new Color(85f / 255f, 196f / 255f, 200f / 255f, 1f);
    public Color surfaceColorB = new Color(65f / 255f, 166f / 255f, 169f / 255f, 1f);
    // 색 판정 허용 오차(채널별 최대 차이, 0~1)
    [Range(0f, 0.2f)] public float colorTolerance = 0.03f;
    // 포자 색
    public Color sporeColor = new Color(0.8f, 0.62f, 1f, 1f);
    // 가장 밝을 때 밝기 배수(HDR). 블룸 문턱을 넘는 만큼 번진다.
    public float brightness = 1.25f;
    // 포자 하나가 맡는 칸 크기(텍셀). 클수록 드물다.
    public float cellTexels = 12f;
    // 한 알의 주기(초)와 그중 떠 있는 비율
    public float period = 7f;
    [Range(0f, 1f)] public float duty = 0.55f;
    // 떠가는 방향과, 한 번 떠 있는 동안 가는 거리(텍셀)
    public Vector2 driftDirection = new Vector2(1f, -0.3f);
    public float travelTexels = 5f;
    // 옆으로 흔들리는 폭(텍셀, 0~2)
    [Range(0f, 2f)] public float wobbleTexels = 1f;
    // 두 번째 포자 색. 포자마다 포자 색과 이 색 사이의 한 색이 된다(같으면 한 색).
    public Color sporeColorB = new Color(0.8f, 0.62f, 1f, 1f);
    // 밝게 떠 있을 때 큰 포자(절반쯤)가 둘레 한 칸으로 번져 솜뭉치처럼 보이는 세기(0이면 모두 텍셀 한 칸)
    [Range(0f, 1f)] public float glowArms = 0f;
    // 포자 둘레 물이 포자 색으로 물드는 반경(텍셀, 0이면 없음)과 진하기
    [Range(0f, 3f)] public float haloTexels = 0f;
    [Range(0f, 1f)] public float haloAlpha = 0f;
    // 떠 있는 동안 한 번 별처럼 반짝이는(상하좌우 빛줄기) 세기(0이면 없음)
    [Range(0f, 1f)] public float twinkle = 0f;
}

/// <summary>
/// 물(용암) 위 거품. 물 타일 그림의 수면색 픽셀 위에서 거품이 점처럼 올라와 둥글게 부풀고, 다 부풀면 톡 터지며
/// 끊어진 고리 조각이 퍼졌다 사라진다(물 타일 셰이더 WaterTilemapShader). 렌더링만 바뀌고 게임 동작에는 영향이 없다.
/// </summary>
[System.Serializable]
public class WaterBubbleSettings
{
    public bool enabled = false;
    // 물 타일 그림의 수면색(판정용)과 색 판정 허용 오차(채널별 최대 차이, 0~1)
    public Color surfaceColor = new Color(215f / 255f, 51f / 255f, 3f / 255f, 1f);
    [Range(0f, 0.2f)] public float colorTolerance = 0.03f;
    // 거품 몸 색(터질 때 흩어지는 조각도 이 색), 아래쪽 테두리(그늘)·막 올라올 때 점의 색, 윗쪽 하이라이트 색
    public Color bodyColor = new Color(1f, 0.55f, 0.1f, 1f);
    public Color edgeColor = new Color(0.62f, 0.1f, 0.02f, 1f);
    public Color highlightColor = new Color(1f, 0.9f, 0.45f, 1f);
    // 하이라이트 밝기 배수(HDR). 블룸 문턱을 넘는 만큼 번진다.
    public float highlightBrightness = 1.3f;
    // 거품 하나가 맡는 칸 크기(텍셀). 클수록 드물다.
    public float cellTexels = 10f;
    // 한 번 되풀이하는 주기(초)와 그중 거품이 있는 비율
    public float period = 2.5f;
    [Range(0f, 1f)] public float duty = 0.6f;
    // 다 부푼 반지름(텍셀, 최소~최대)
    public Vector2 maxRadiusRange = new Vector2(1.5f, 2.5f);
    // 거품이 있는 시간 중 터지는 데 쓰는 비율(0~1)
    [Range(0.05f, 0.9f)] public float popFraction = 0.2f;
    // 섬을 둘러싼 바다(섬 밖과 이어진 물)에서 거품이 올라오는 빈도(0~1). 1이면 섬 안 웅덩이와 같고, 낮출수록 바다 쪽 거품만 드물어진다.
    [Range(0f, 1f)] public float seaFrequency = 1f;
}

/// <summary>
/// 버섯 포자 뿜기. 화면에 보이는 큰 버섯 장식이 저마다의 박자로 몇 초에 한 번 갓 위로 작은 포자 알갱이를 몇 개 뿜는다.
/// 스테이지에 파티클 시스템 하나만 두고 보이는 버섯 자리에서만 뿜는다. 충돌·판정이 없어 게임 동작에는 영향이 없다.
/// enabled가 꺼져 있거나 material·mushroomPrefabs가 비어 있으면 아무것도 만들지 않는다.
/// </summary>
[System.Serializable]
public class MushroomSporeSettings
{
    public bool enabled = false;
    // 포자를 뿜을 장식 프리팹(스테이지 데코 목록에 들어 있는 것)
    public System.Collections.Generic.List<StaticObj> mushroomPrefabs = new System.Collections.Generic.List<StaticObj>();
    // 포자 알갱이 머티리얼(MushroomSpore 셰이더)
    public Material material;
    // 그리는 정렬 자리(기본은 오브젝트 맨 위)
    public string sortingLayerName = "Objects";
    public int sortingOrder = 32767;
    // 뿜는 자리: 장식 위치에서 갓 꼭대기까지의 오프셋과, 그 둘레로 흩어지는 폭(월드)
    public Vector2 emitOffset = new Vector2(0f, 0.45f);
    public Vector2 emitSpread = new Vector2(0.15f, 0.05f);
    // 한 버섯이 뿜는 간격(초, 버섯마다 이 범위 안에서 정해진다)
    public Vector2 intervalRange = new Vector2(4f, 7f);
    // 한 번에 뿜는 알갱이 수(최소~최대)
    public Vector2Int burstCountRange = new Vector2Int(5, 8);
    // 처음 솟는 속도(월드/초, 최소~최대)와 옆으로 퍼지는 속도
    public Vector2 riseSpeedRange = new Vector2(0.5f, 0.9f);
    public float sideSpeed = 0.45f;
    // 솟은 뒤 느려지는 정도(0~1, 클수록 빨리 멈춰 떠다닌다)
    [Range(0f, 1f)] public float dampen = 0.025f;
    // 느려진 뒤 계속 위로 떠오르는 힘(월드/초², 음수 중력)
    public float buoyancy = 0.08f;
    // 떠다니며 흔들리는 세기(월드/초)
    public float wanderStrength = 0.12f;
    // 알갱이 수명(초, 최소~최대)
    public Vector2 lifetimeRange = new Vector2(1.6f, 2.6f);
    // 알갱이 크기(픽셀, 최소~최대). 화면 픽셀에 맞춰 그린다.
    public Vector2Int sizePixelsRange = new Vector2Int(1, 2);
    // 알갱이 색(두 색 사이에서 무작위)
    public Color colorA = new Color(0.68f, 0.99f, 0.99f, 1f);
    public Color colorB = new Color(0.86f, 0.8f, 1f, 1f);
    // 동시에 떠 있는 알갱이 최대 수
    [Min(16)] public int maxParticles = 256;
    // 갓 숨쉬기: 뿜기 전 riseTime초 동안 갓이 서서히 밝아져 뿜는 순간 가장 밝고(원래 밝기의 1+amount배),
    // 뿜은 뒤 fallTime초 동안 원래 밝기로 돌아온다. amount가 0이면 끈다.
    [Min(0f)] public float capGlowAmount = 0.3f;
    [Min(0.05f)] public float capGlowRiseTime = 1.6f;
    [Min(0.05f)] public float capGlowFallTime = 0.5f;
}

/// <summary>
/// 바닥 꽃버섯 반짝임. 블룸 데코 타일맵(작은 빛나는 꽃·버섯 장식)을 이 스테이지에서만 전용 머티리얼로 그려,
/// 장식들이 숲을 따라 느리게 이어지는 물결에 맞춰 하나씩 잠깐 밝아졌다 꺼지게 한다(BloomDecoTwinkle 셰이더).
/// 공용 스프라이트 머티리얼은 건드리지 않고 이 타일맵의 머티리얼만 갈아 끼우며, 꺼진 스테이지는 원래 머티리얼 그대로다.
/// 반짝임 세기·속도·물결 크기는 머티리얼에서 정한다.
/// </summary>
[System.Serializable]
public class BloomDecoTwinkleSettings
{
    public bool enabled = false;
    public Material material;
}

/// <summary>
/// 나무 별빛 반짝임. 나무 하이라이트 그림의 별점(지정한 색의 픽셀)이 점마다 따로, 제 박자대로 잠깐 밝아졌다
/// 평소 밝기로 돌아온다(나무 셰이더 Custom-Sprite-Default-Tree). 그림의 색은 그대로 두고 밝기만 곱한다.
/// 렌더링만 바뀌고 게임 동작에는 영향이 없다.
/// </summary>
[System.Serializable]
public class TreeStarTwinkleSettings
{
    public bool enabled = false;
    // 반짝일 별점 색(나무 그림의 sRGB 색)과 판정 허용 오차(채널별 최대 차이, 0~1)
    public Color starColor = new Color(1f, 1f, 140f / 255f, 1f);
    [Range(0f, 0.3f)] public float colorTolerance = 0.06f;
    // 평소 밝기 배율과 가장 밝을 때 배율
    [Min(0f)] public float restLevel = 0.8f;
    [Min(0f)] public float peakLevel = 2f;
    // 한 점이 한 번 반짝이고 다시 반짝이기까지의 평균 시간(초, 점마다 0.7~1.3배)
    [Min(0.1f)] public float period = 3f;
    // 그중 밝아져 있는 시간의 비율(0~1)
    [Range(0.01f, 1f)] public float duty = 0.25f;
    // 뜨거운 픽셀(빨강이 강한 픽셀: 빨강 ≥ hotMinRed, 빨강 - 초록·파랑 중 큰 값 ≥ hotMinRedLead)도 함께 반짝이게 한다.
    // hotMinRed가 0이면 끄고 별점 색만 반짝인다.
    [Range(0f, 1f)] public float hotMinRed = 0f;
    [Range(0f, 1f)] public float hotMinRedLead = 0.25f;
    // 켜면 점마다가 아니라 그림(나무) 하나가 한 박자로 함께 밝아진다(숨쉬기).
    public bool syncPerTree = false;
}

/// <summary>
/// 장식 숨쉬기. 화면에 보이는(컬링으로 켜져 있는) 지정한 장식이 저마다의 박자로 몇 초에 한 번 은은하게 밝아졌다 돌아온다.
/// 장식의 밝기 값(StaticObj HDR 배수)만 바꾸므로 렌더링만 바뀌고 게임 동작에는 영향이 없다.
/// enabled가 꺼져 있거나 decoPrefabs가 비어 있으면 아무것도 만들지 않는다.
/// </summary>
[System.Serializable]
public class DecoBreathSettings
{
    public bool enabled = false;
    // 숨쉴 장식 프리팹(스테이지 데코 목록에 들어 있는 것)
    public System.Collections.Generic.List<StaticObj> decoPrefabs = new System.Collections.Generic.List<StaticObj>();
    // 가장 밝을 때 원래 밝기의 1+amount배
    [Min(0f)] public float amount = 0.4f;
    // 한 번 숨쉬는 시간(초, 장식마다 이 범위 안에서 정해진다)
    public Vector2 periodRange = new Vector2(4f, 7f);
    // 클수록 밝아져 있는 순간이 짧다(1이면 사인 그대로)
    [Min(1f)] public float sharpness = 3f;
}

/// <summary>
/// 나무 타격 별가루. 나무를 때릴 때마다 나무 윗부분에서 작은 별가루 알갱이가 튀어 오르다 천천히 떨어지며 사라진다.
/// 기존 피격 이펙트는 그대로 두고 그 위에 덧붙인다. 스테이지에 파티클 시스템 하나만 두며 충돌·판정이 없어 게임 동작에는 영향이 없다.
/// enabled가 꺼져 있거나 material이 비어 있으면 아무것도 만들지 않는다.
/// </summary>
[System.Serializable]
public class HitSparkleSettings
{
    public bool enabled = false;
    // 알갱이 머티리얼(MushroomSpore 셰이더)
    public Material material;
    // 그리는 정렬 자리(기본은 오브젝트 맨 위)
    public string sortingLayerName = "Objects";
    public int sortingOrder = 32767;
    // 나무 윗부분 기준 자리에서의 오프셋과 흩어지는 폭(월드)
    public Vector2 emitOffset = new Vector2(0f, 0.2f);
    public Vector2 emitSpread = new Vector2(0.3f, 0.35f);
    // 한 번에 튀는 알갱이 수(최소~최대)
    public Vector2Int burstCountRange = new Vector2Int(3, 5);
    // 튀어 나가는 속도(월드/초, 최소~최대). 위쪽으로 조금 치우친다.
    public Vector2 speedRange = new Vector2(0.8f, 1.6f);
    public float upwardBias = 0.6f;
    // 튄 뒤 느려지는 정도(0~1)와 떨어지는 힘(월드/초²)
    [Range(0f, 1f)] public float dampen = 0.08f;
    public float gravity = 0.6f;
    // 알갱이 수명(초, 최소~최대)
    public Vector2 lifetimeRange = new Vector2(0.5f, 0.9f);
    // 알갱이 크기(픽셀, 최소~최대)
    public Vector2Int sizePixelsRange = new Vector2Int(1, 2);
    // 알갱이 색(두 색 사이에서 무작위)
    public Color colorA = new Color(1f, 0.96f, 0.6f, 1f);
    public Color colorB = new Color(0.85f, 0.95f, 1f, 1f);
    // 동시에 떠 있는 알갱이 최대 수와 한 프레임에 튀게 할 최대 타격 수(광역 타격 몰림 대비)
    [Min(16)] public int maxParticles = 192;
    [Min(1)] public int maxBurstsPerFrame = 6;
}

/// <summary>
/// 용암 위 나무 그림자 없애기. 용암은 스스로 빛나는 표면이라 나무 그림자가 덮이면 노란 테두리가 올리브색으로 탁해진다.
/// 물 스텐실이 용암 칸에 나무 그림자 비트(4)까지 켜 두면, 그림자는 이미 그린 자리를 건너뛰는 규칙 그대로 용암 위를 건너뛴다.
/// 육지 스텐실은 용암 칸에 겹쳐 그린 육지에서 그 비트를 다시 꺼 육지 위 그림자는 그대로 남긴다.
/// 두 머티리얼을 이 스테이지 동안만 물·육지 스텐실 타일맵에 끼우고 다른 스테이지는 원래 머티리얼로 되돌린다. 렌더링 전용.
/// </summary>
[System.Serializable]
public class LavaShadowMaskSettings
{
    public bool enabled = false;
    // 물 스텐실 머티리얼(WaterStencil 셰이더, 스텐실 값 5·쓰기 마스크 5)
    public Material waterStencilMaterial;
    // 육지 스텐실 머티리얼(GroundStencil 셰이더, 쓰기 마스크 7)
    public Material groundStencilMaterial;
}

/// <summary>
/// 용암 빛 번짐. 용암 칸 둘레의 바닥이 용암 빛을 받아 주황으로 은은하게 물든다(바닥 셰이더 GroundLavaGlowLit).
/// 맵을 만들 때 칸마다 "용암에서 받는 빛"을 담은 작은 텍스처를 한 장 만들고, 바닥이 그걸 읽어 텍셀 단위 몇 단계로 끊어 칠한다.
/// 렌더링만 바뀌고 게임 동작에는 영향이 없다. enabled가 꺼져 있거나 groundMaterial이 비어 있으면 아무것도 하지 않는다.
/// </summary>
[System.Serializable]
public class LavaGlowSettings
{
    public bool enabled = false;
    // 바닥(과 바닥 장식) 타일맵에 끼울 머티리얼(GroundLavaGlowLit 셰이더)
    public Material groundMaterial;
    // 빛 색과 세기(더하는 양), 바닥 색을 얼마나 데우는지(곱하는 양)
    public Color color = new Color(1f, 0.38f, 0.08f, 1f);
    [Min(0f)] public float intensity = 0.18f;
    [Min(0f)] public float warmMultiply = 0.35f;
    // 빛이 닿는 거리(월드, 용암 칸 가운데부터)와 감쇠 곡선(클수록 가까운 곳에 몰린다)
    [Min(0.3f)] public float radius = 1.2f;
    [Min(0.1f)] public float falloffPower = 2f;
    // 진하기 단계 수(텍셀 단위로 끊어 픽셀아트처럼 보이게 한다)
    [Min(1f)] public float steps = 4f;
    // 천천히 일렁이는 정도(0이면 고정), 속도, 일렁임 무늬 크기(월드)
    [Range(0f, 1f)] public float flickerAmount = 0.2f;
    [Min(0f)] public float flickerSpeed = 0.35f;
    [Min(0.1f)] public float flickerScale = 2.5f;
    // 나무·캐릭터(서 있는 그림)가 받는 빛의 배율(바닥 세기 기준, 0이면 끔)과, 발밑에서 위로 빛이 사라지는 높이(월드).
    // 서 있는 그림은 발밑 칸이 받는 빛을 읽어 발밑에서 가장 밝고 위로 갈수록 옅게 칠한다.
    [Min(0f)] public float objectStrength = 1f;
    [Min(0.1f)] public float objectFadeHeight = 0.8f;
}

/// <summary>
/// 나무 열기 차오름. 열기를 내뿜기로 한 나무(카운트다운 중)의 뜨거운 줄기 픽셀이 내뿜는 순간까지 점점 밝아지며
/// 달군 빛 색(노랑)을 띤다(나무 셰이더 Custom-Sprite-Default-Tree). 껍질 등 나머지 픽셀은 그대로다. 바닥 예고(TreeHeatIndicator)와
/// 같은 카운트다운을 따른다. 렌더링만 바뀌고 게임 동작에는 영향이 없다. 꺼진 스테이지·로그라이크는 아무것도 칠하지 않는다.
/// </summary>
[System.Serializable]
public class TreeHeatChargeSettings
{
    public bool enabled = false;
    // 뜨거운 픽셀 판정(나무 그림 sRGB): 빨강 ≥ hotMinRed, 빨강 - 초록·파랑 중 큰 값 ≥ hotMinRedLead
    [Range(0f, 1f)] public float hotMinRed = 0.4f;
    [Range(0f, 1f)] public float hotMinRedLead = 0.25f;
    // 다 찼을 때 뜨거운 픽셀 밝기 배율 증가량(1이면 두 배)
    [Min(0f)] public float hotBoost = 1.2f;
    // 다 찼을 때 뜨거운 픽셀에 더하는 빛 색과 양
    public Color tintColor = new Color(1f, 0.75f, 0.3f, 1f);
    [Min(0f)] public float tintAmount = 0.5f;
    // 차오르는 곡선(클수록 끝에 몰려 밝아진다)
    [Min(0.1f)] public float curvePower = 2f;
}

/// <summary>
/// 용암 기둥. 몇 초에 한 번, 화면 안 용암 칸(가장자리가 아닌 안쪽 칸 우선, 없으면 가장자리 칸) 한 곳에서 작은 용암 알갱이 기둥이 곧게 솟았다가 떨어진다.
/// 짧은 시간 동안 같은 자리에서 위 속도가 조금씩 다른 알갱이를 이어 쏘아 가는 기둥처럼 보이게 한다.
/// 스테이지에 파티클 시스템 하나만 두며 충돌·판정이 없어 게임 동작에는 영향이 없다.
/// enabled가 꺼져 있거나 material이 비어 있으면 아무것도 만들지 않는다.
/// </summary>
[System.Serializable]
public class LavaSpoutSettings
{
    public bool enabled = false;
    // 알갱이 머티리얼(용암 불씨와 같은 것)
    public Material material;
    // 그리는 정렬 자리(기본은 오브젝트 맨 아래: 용암·그림자 위, 나무·오프로드 뒤)
    public string sortingLayerName = "Objects";
    public int sortingOrder = -32768;
    // 다음 기둥까지 기다리는 시간(초, 최소~최대). 그때 화면에 용암이 안 보이면 건너뛴다.
    public Vector2 intervalRange = new Vector2(2.5f, 5f);
    // 자리를 고를 때 화면 안을 몇 번까지 찍어 볼지
    [Min(1)] public int pickAttempts = 24;
    // 기둥이 솟는 시간(초, 최소~최대)과 그동안 1초에 쏘는 알갱이 수
    public Vector2 durationRange = new Vector2(0.3f, 0.45f);
    [Min(1f)] public float particlesPerSecond = 70f;
    // 위 속도(월드/초, 최소~최대)와 옆으로 흩어지는 속도(월드/초, 좌우 무작위). 높이는 위 속도² / (2 × 떨어지는 힘).
    public Vector2 upSpeedRange = new Vector2(2.4f, 3.4f);
    [Min(0f)] public float sideSpeed = 0.12f;
    // 떨어지는 힘(월드/초²)
    public float gravity = 8f;
    // 쏘는 자리의 가로 흔들림(월드, 기둥 굵기)
    [Min(0f)] public float baseWidth = 0.05f;
    // 알갱이 크기(픽셀, 최소~최대)
    public Vector2Int sizePixelsRange = new Vector2Int(1, 2);
    // 알갱이 색(두 색 사이에서 무작위)
    public Color colorA = new Color(1f, 0.5f, 0.1f, 1f);
    public Color colorB = new Color(1f, 0.85f, 0.35f, 1f);
    // 동시에 떠 있는 알갱이 최대 수
    [Min(8)] public int maxParticles = 128;
}

/// <summary>
/// 데코 증기. 잠깐마다 화면 안 블룸 데코(불씨 바위 등) 한 곳에서 김 덩어리 몇 개가 연달아 "푸쉬쉭" 뿜어져
/// 떠오르며 커지다가 점점이 흩어져 사라진다. 덩어리 모양(울퉁불퉁한 구름·위아래 음영·흩어짐)은 머티리얼(DecoSteamPuff 셰이더)이 픽셀 단위로 그린다.
/// 스테이지에 파티클 시스템 하나만 두며 충돌·판정이 없어 게임 동작에는 영향이 없다.
/// enabled가 꺼져 있거나 material이 비어 있으면 아무것도 만들지 않는다.
/// </summary>
[System.Serializable]
public class DecoSteamSettings
{
    public bool enabled = false;
    // 김 덩어리 머티리얼(DecoSteamPuff 셰이더)
    public Material material;
    // 그리는 정렬 자리(기본은 오브젝트 맨 아래: 데코 위, 나무·오프로드 뒤)
    public string sortingLayerName = "Objects";
    public int sortingOrder = -32768;
    // 다음 뿜기까지 기다리는 시간(초, 최소~최대). 그때 화면에 블룸 데코가 안 보이면 건너뛴다.
    public Vector2 intervalRange = new Vector2(0.8f, 1.8f);
    // 자리를 고를 때 화면 안을 몇 번까지 찍어 볼지
    [Min(1)] public int pickAttempts = 32;
    // 데코 칸 가운데에서 뿜는 자리까지(월드)와 그 자리의 무작위 흔들림(월드, 좌우·위아래)
    public Vector2 originOffset = new Vector2(0.03f, -0.25f);
    public Vector2 originSpread = new Vector2(0.03f, 0.02f);
    // 한 번 뿜을 때 덩어리 수(최소~최대)와 덩어리 사이 시간(초)
    public Vector2Int puffsPerBurst = new Vector2Int(2, 3);
    [Min(0f)] public float puffGap = 0.14f;
    // 덩어리 칸 크기(픽셀, 최소~최대). 덩어리는 이 칸 안에서 작게 나타나 칸만큼 커진다.
    public Vector2Int sizePixelsRange = new Vector2Int(8, 11);
    // 처음 떠오르는 속도(월드/초, 최소~최대), 공기 저항(1초에 줄어드는 속도 비율), 옆으로 흘러가는 속도(월드/초, 좌우 무작위)
    public Vector2 upSpeedRange = new Vector2(0.6f, 0.85f);
    [Min(0f)] public float drag = 1f;
    [Min(0f)] public float sideDrift = 0.1f;
    // 덩어리 수명(초, 최소~최대)
    public Vector2 lifetimeRange = new Vector2(1f, 1.3f);
    // 김 색(두 색 사이에서 무작위)
    public Color colorA = new Color(0.86f, 0.84f, 0.84f, 0.42f);
    public Color colorB = new Color(0.72f, 0.7f, 0.7f, 0.32f);
    // 동시에 떠 있는 덩어리 최대 수
    [Min(8)] public int maxParticles = 48;
    // 뿜는 데코의 불빛이 잠깐 밝아지는 시간(초). 0이면 안 밝아진다. 밝아지는 세기는 블룸 데코 숨쉬기 머티리얼(BloomDecoBreath)이 정하므로
    // 숨쉬기를 켠 스테이지에서만 보인다.
    [Min(0f)] public float glowFlashDuration = 0f;
}

/// <summary>
/// 불씨 데코 숨쉬기. 블룸 데코 타일맵(불씨 바위 등)을 BloomDecoBreath 머티리얼로 그려, 장식마다 제 박자로 불빛이 은은하게
/// 밝아졌다 돌아오고 증기가 뿜어지는 순간 그 장식이 잠깐 더 밝아지게 한다. 숨쉬는 세기·속도는 머티리얼에서 정한다.
/// 공용 스프라이트 머티리얼은 건드리지 않고 이 타일맵의 머티리얼만 갈아 끼우며, 꺼진 스테이지는 원래 머티리얼 그대로다.
/// 반짝임(BloomDecoTwinkle)이 켜져 있으면 반짝임이 먼저다. 렌더링만 바뀌고 게임 동작에는 영향이 없다.
/// </summary>
[System.Serializable]
public class BloomDecoBreathSettings
{
    public bool enabled = false;
    public Material material;
}

/// <summary>
/// 용암 온도 무늬. 물 타일 그림의 수면색(용암 안쪽 주황) 픽셀 위에 큰 단위로 천천히 바뀌는 식은 곳(짙은 주황)과
/// 뜨거운 곳(밝은 노랑)을 텍셀 단위로 덧칠해, 한 가지 색 판처럼 보이지 않게 한다(물 타일 셰이더 WaterTilemapShader).
/// 거품은 이 위에 그린다. 렌더링만 바뀌고 게임 동작에는 영향이 없다.
/// </summary>
[System.Serializable]
public class WaterMagmaSettings
{
    public bool enabled = false;
    // 물 타일 그림의 수면색(판정용)과 색 판정 허용 오차(채널별 최대 차이, 0~1)
    public Color surfaceColor = new Color(215f / 255f, 51f / 255f, 3f / 255f, 1f);
    [Range(0f, 0.2f)] public float colorTolerance = 0.03f;
    // 식은 곳·뜨거운 곳 색
    public Color coolColor = new Color(0.72f, 0.14f, 0.02f, 1f);
    public Color hotColor = new Color(1f, 0.36f, 0.03f, 1f);
    // 무늬 크기(월드)와 흘러가는 방향·속도(월드/초), 모양이 바뀌는 속도
    [Min(0.2f)] public float scale = 1.2f;
    public Vector2 drift = new Vector2(0.06f, 0.03f);
    [Min(0f)] public float evolveSpeed = 0.08f;
    // 이 값보다 낮으면 식은 곳, 높으면 뜨거운 곳(무늬 값 0~1)
    [Range(0f, 1f)] public float coolThreshold = 0.38f;
    [Range(0f, 1f)] public float hotThreshold = 0.66f;

    // 섬을 둘러싼 바다(섬 밖과 이어진 용암)만 따로 칠한다. 꺼 두면 바다도 섬 안 웅덩이와 똑같다.
    public bool seaEnabled = false;
    // 바다 무늬 크기(월드). 웅덩이보다 크게 두면 넓은 바다가 잔무늬로 시끄럽지 않다.
    [Min(0.2f)] public float seaScale = 2.4f;
    // 해안에서 이 칸 수만큼 떨어지면 "먼 바다" 값이 다 걸린다(그 사이는 부드럽게 이어진다).
    [Min(1f)] public float seaFarCells = 10f;
    // 먼 바다로 갈수록 식은 곳 문턱을 올리고(식은 곳이 넓어짐) 뜨거운 곳 문턱을 올린다(밝은 곳이 줄어듦).
    [Range(0f, 1f)] public float seaFarCoolRaise = 0.12f;
    [Range(0f, 1f)] public float seaFarHotRaise = 0.15f;
    // 먼 바다로 갈수록 이 색(식어서 어두운 용암)으로 덮는 비율(0~1)
    public Color seaFarColor = new Color(0.32f, 0.05f, 0.02f, 1f);
    [Range(0f, 1f)] public float seaFarDarken = 0.5f;

    // 바다 위를 천천히 떠다니는 굳은 껍질(검은 현무암 판). 판 사이 틈과 가장자리는 뜨거운 용암이 비친다.
    public bool crustEnabled = false;
    // 판 하나 크기(월드)와 흘러가는 방향·속도(월드/초)
    [Min(0.2f)] public float crustSize = 1.3f;
    public Vector2 crustDrift = new Vector2(0.035f, 0.018f);
    // 판이 깔리는 비율(0~1). 해안 쪽 값에서 먼 바다 값으로 이어진다. 해안 쪽으로 흘러오면 줄어들며 녹아 사라진다.
    [Range(0f, 1f)] public float crustCoverageNear = 0f;
    [Range(0f, 1f)] public float crustCoverageFar = 0.45f;
    // 판 사이 틈 넓이와 빛나는 가장자리 넓이(판 크기 대비, 0~1)
    [Range(0f, 1f)] public float crustGap = 0.1f;
    [Range(0f, 1f)] public float crustRim = 0.07f;
    // 껍질 색 두 가지(어두운 바탕, 얼룩), 빛나는 가장자리 색
    public Color crustColorA = new Color(0.13f, 0.06f, 0.06f, 1f);
    public Color crustColorB = new Color(0.22f, 0.09f, 0.07f, 1f);
    public Color crustRimColor = new Color(0.95f, 0.42f, 0.06f, 1f);
}

/// <summary>
/// 용암 불씨.화면 안의 용암 칸에서 작은 불씨 알갱이가 비스듬히 튀어 올라 포물선을 그리며 떨어지고,
/// 튀어 오른 높이쯤으로 내려오면 사라진다.
/// 화면 안 무작위 자리를 정해진 횟수만큼 골라 그 자리가 용암 칸일 때만 튀게 하므로, 화면에 보이는 용암 넓이만큼 튀고
/// 용암이 안 보이면 거의 아무 일도 하지 않는다. 스테이지에 파티클 시스템 하나만 두며 충돌·판정이 없어 게임 동작에는 영향이 없다.
/// enabled가 꺼져 있거나 material이 비어 있으면 아무것도 만들지 않는다.
/// </summary>
[System.Serializable]
public class LavaEmberSettings
{
    public bool enabled = false;
    // 알갱이 머티리얼(MushroomSpore 셰이더)
    public Material material;
    // 그리는 정렬 자리(기본은 오브젝트 맨 아래: 용암·그림자 위, 나무·오프로드 뒤)
    public string sortingLayerName = "Objects";
    public int sortingOrder = -32768;
    // 1초에 화면 안을 몇 번 골라 볼지(고른 자리가 용암일 때만 튄다)와 한 프레임에 고르는 최대 횟수
    public float samplesPerSecond = 30f;
    [Min(1)] public int maxSamplesPerFrame = 4;
    // 한 번에 튀는 알갱이 수(최소~최대)
    public Vector2Int burstCountRange = new Vector2Int(1, 3);
    // 처음 튀어 오르는 위 속도와 옆 속도(월드/초, 최소~최대). 옆 방향(왼쪽·오른쪽)은 무작위.
    public Vector2 launchUpSpeedRange = new Vector2(1.8f, 2.8f);
    public Vector2 launchSideSpeedRange = new Vector2(0.3f, 0.9f);
    // 떨어지는 힘(월드/초²). 클수록 포물선이 낮고 짧다.
    public float gravity = 8f;
    // 튀어 오른 높이보다 얼마나 더 내려와서 사라질지(월드, 최소~최대)
    public Vector2 fallBelowRange = new Vector2(0f, 0.15f);
    // 알갱이 크기(픽셀, 최소~최대)
    public Vector2Int sizePixelsRange = new Vector2Int(1, 1);
    // 알갱이 색(두 색 사이에서 무작위)
    public Color colorA = new Color(1f, 0.45f, 0.08f, 1f);
    public Color colorB = new Color(1f, 0.82f, 0.3f, 1f);
    // 동시에 떠 있는 알갱이 최대 수
    [Min(8)] public int maxParticles = 64;
}

/// <summary>
/// 물가·풀숲 반딧불. 물가(물과 땅의 경계에서 몇 칸 안)와 풀 장식이 있는 칸에서 작은 빛 알갱이가 천천히 떠다니며 깜빡인다.
/// 화면 안 무작위 자리를 정해진 횟수만큼 골라 물가면 늘, 풀숲이면 정해진 확률로 띄운다(물가 판정은 StageShoreMap).
/// 스테이지에 파티클 시스템 하나만 두며 충돌·판정이 없어 게임 동작에는 영향이 없다.
/// enabled가 꺼져 있거나 material이 비어 있으면 아무것도 만들지 않는다.
/// </summary>
[System.Serializable]
public class FireflySettings
{
    public bool enabled = false;
    // 알갱이 머티리얼(MushroomSpore 셰이더)
    public Material material;
    // 그리는 정렬 자리(기본은 오브젝트 맨 위)
    public string sortingLayerName = "Objects";
    public int sortingOrder = 32765;
    // 1초에 화면 안을 몇 번 골라 볼지와 한 프레임에 고르는 최대 횟수
    public float samplesPerSecond = 6f;
    [Min(1)] public int maxSamplesPerFrame = 3;
    // 물가로 치는 거리(물과 땅의 경계에서 칸 수). 이 안이면 늘 띄운다.
    public float shoreReachCells = 1.5f;
    // 물가가 아닌 풀숲(풀 장식 칸)에서 띄울 확률(0~1)
    [Range(0f, 1f)] public float grassChance = 0.3f;
    // 떠오르는 높이(월드, 최소~최대). 땅에서 이만큼 위에서 생긴다.
    public Vector2 heightRange = new Vector2(0.1f, 0.6f);
    // 떠다니는 빠르기(월드/초)와 방향이 바뀌는 빠르기(Hz)
    public float wanderSpeed = 0.25f;
    public float wanderFrequency = 0.35f;
    // 알갱이 수명(초, 최소~최대). 수명 동안 두세 번 깜빡인다.
    public Vector2 lifetimeRange = new Vector2(3.5f, 5.5f);
    // 알갱이 크기(픽셀, 최소~최대)
    public Vector2Int sizePixelsRange = new Vector2Int(1, 2);
    // 알갱이 색(두 색 사이에서 무작위)
    public Color colorA = new Color(1f, 0.92f, 0.55f, 1f);
    public Color colorB = new Color(0.65f, 0.95f, 1f, 1f);
    // 동시에 떠 있는 알갱이 최대 수
    [Min(8)] public int maxParticles = 64;
}

/// <summary>
/// 숲의 숨결. 몇십 초에 한 번, 화면 밖 먼 곳에서 출발한 느린 빛 물결이 땅을 따라 화면을 쓸고 지나간다.
/// 물결이 닿는 곳의 꽃 장식(BloomDecoTwinkle)이 밝아지고, 숨쉬는 장식(DecoBreath의 장식들)이 함께 숨을 들이쉬고,
/// 물 위에는 물결을 따라 반짝임이 흩뿌려지며 물가 테두리가 달빛으로 물든다. 나무는 밑동에 물결이 닿으면 밝아지며 별점이 차례로 반짝이고,
/// 땅(바닥·풀 장식 타일맵, groundMaterial로 바꿔 끼운다)은 아주 옅은 달빛으로 물들며 이슬 같은 반짝임이 흩뿌려진다.
/// 지나가면 모두 평소대로 돌아온다.
/// 물결 하나만 있고 그 위치는 StageForestBreath가 매 프레임 전역값으로 넘기며, 셰이더와 장식이 같은 값을 읽는다.
/// 렌더링만 바뀌고 게임 동작에는 영향이 없다. enabled가 꺼져 있으면 아무것도 만들지 않고 셰이더 출력도 기존과 같다.
/// </summary>
[System.Serializable]
public class ForestBreathSettings
{
    public bool enabled = false;
    // 물결과 물결 사이 시간(초, 최소~최대)과 맵을 깐 뒤 첫 물결까지 시간(초, 최소~최대)
    public Vector2 intervalRange = new Vector2(20f, 30f);
    public Vector2 firstDelayRange = new Vector2(6f, 10f);
    // 물결이 땅 위에서 퍼지는 빠르기(땅 거리/초). 땅 거리는 아이소메트릭 바닥 기준(화면 세로를 두 배로 친다).
    [Min(0.1f)] public float speed = 3f;
    // 물결 앞머리가 닿아 가장 밝아지기까지의 폭과, 그 뒤 잦아들기까지의 폭(땅 거리)
    [Min(0.05f)] public float riseWidth = 1.2f;
    [Min(0.05f)] public float fallWidth = 4f;
    // 앞머리가 곧은 선으로 보이지 않게 앞뒤로 흔들리는 폭(땅 거리)과 흔들림 무늬 크기(월드)
    [Min(0f)] public float frontWobble = 1.5f;
    [Min(0.5f)] public float frontWobbleSize = 4f;
    // 꽃 장식이 가장 밝을 때 더하는 밝기 비율
    [Min(0f)] public float flowerBrightness = 1.6f;
    // 숨쉬는 장식이 가장 밝을 때 더하는 밝기 비율
    [Min(0f)] public float decoBrightness = 1f;
    // 물 위 반짝임: 물 반짝임 칸 중 물결이 지날 때 반짝이는 칸의 비율(0~1)과 한 점이 반짝이는 길이(땅 거리)
    [Range(0f, 1f)] public float waterSparkleCoverage = 0.85f;
    [Min(0.05f)] public float waterSparkleLength = 1f;
    // 물가 테두리가 물결에 물드는 세기(0~1)와 수면 전체가 아주 옅게 밝아지는 세기(0~1)
    [Range(0f, 1f)] public float shoreGlow = 0.8f;
    [Range(0f, 1f)] public float surfaceSheen = 0.1f;
    // 나무: 별점(나무 별빛 반짝임의 별점 색)이 가장 밝을 때 더하는 밝기 비율과 나무 전체가 밝아지는 비율
    [Min(0f)] public float treeStarBrightness = 6f;
    [Min(0f)] public float treeBrightness = 0.6f;
    // 땅: 바닥·풀 장식 타일맵에 끼울 머티리얼(GroundForestBreathLit 셰이더). 비어 있으면 땅은 그대로다.
    public Material groundMaterial;
    // 땅 달빛 띠·이슬 반짝임 색
    public Color groundGlowColor = new Color(0.62f, 0.86f, 1f, 1f);
    // 땅이 물결을 따라 옅게 물드는 세기(0~1)
    [Range(0f, 1f)] public float groundSheen = 0.08f;
    // 이슬 반짝임: 칸 중 반짝이는 칸의 비율(0~1), 칸 크기(텍셀), 한 점이 반짝이는 길이(땅 거리), 가장 밝을 때 밝기(HDR)
    [Range(0f, 1f)] public float groundGlintCoverage = 0.5f;
    [Min(4f)] public float groundGlintCellTexels = 10f;
    [Min(0.05f)] public float groundGlintLength = 1f;
    [Min(0f)] public float groundGlintBrightness = 1.8f;
}

/// <summary>
/// 포자막 상태 연출(2스테이지). 포자막을 때리면 결정이 번쩍이며 조각이 튀고, 깎인 만큼 결정이 어둡고 탁해지며,
/// 다시 차오르는 동안 둘레의 포자 알갱이가 수관으로 빨려 들어가고 결정 위로 빛 띠가 훑어 오르고, 가득 차는 순간 한 번 맥동한다.
/// 결정 표현은 나무 셰이더(TreeSporeShield.hlsl)가, 알갱이는 StageSporeShieldFx의 파티클 시스템 둘이 그린다.
/// 렌더링만 바뀌고 포자막 수치·회복·판정은 그대로다. enabled가 꺼져 있거나 material이 비어 있으면 아무것도 만들지 않는다.
/// </summary>
[System.Serializable]
public class SporeShieldFxSettings
{
    public bool enabled = false;
    // 알갱이 머티리얼(MushroomSpore 셰이더)
    public Material material;
    // 그리는 정렬 자리(기본은 오브젝트 맨 위)
    public string sortingLayerName = "Objects";
    public int sortingOrder = 32767;
    // 결정이 박힌 수관: 나무 윗부분 기준점(GetTopRootPosition, 밑동에서 0.75 위)에서의 가운데 오프셋과 가로·세로 반지름(월드).
    // 2스테이지 세 나무의 결정은 밑동에서 0.5~1.45 높이에 있다.
    public Vector2 crownOffset = new Vector2(0f, 0.2f);
    public Vector2 crownRadius = new Vector2(0.36f, 0.4f);
    // 맞은 순간 결정 번쩍임이 잦아드는 시간(초)과 가득 참 맥동이 잦아드는 시간(초)
    [Min(0.01f)] public float hitFlashDuration = 0.22f;
    [Min(0.01f)] public float fullPulseDuration = 0.5f;
    // 차오름 표현이 켜지는/꺼지는 시간(초)
    [Min(0.01f)] public float regenFadeIn = 0.12f;
    [Min(0.01f)] public float regenFadeOut = 0.35f;
    // 빨려 드는 알갱이: 나무 한 그루가 1초에 띄우는 수, 출발 거리(수관 가운데에서, 월드, 최소~최대), 세로로 눌리는 비율, 날아가는 시간(초), 크기(픽셀), 색
    [Min(0f)] public float motesPerSecond = 8f;
    public Vector2 moteSpawnRadiusRange = new Vector2(0.75f, 1.15f);
    [Range(0.2f, 1f)] public float moteSpawnFlatten = 0.75f;
    public Vector2 moteLifetimeRange = new Vector2(0.45f, 0.7f);
    public Vector2Int moteSizePixelsRange = new Vector2Int(1, 2);
    public Color moteColorA = new Color(0.68f, 0.99f, 0.99f, 1f);
    public Color moteColorB = new Color(0.86f, 0.8f, 1f, 1f);
    // 맞을 때 튀는 결정 조각: 수(최소~최대), 속도(월드/초), 위쪽 치우침, 느려지는 정도(0~1), 떨어지는 힘(월드/초²), 수명(초), 크기(픽셀), 색
    public Vector2Int shardCountRange = new Vector2Int(2, 4);
    public Vector2 shardSpeedRange = new Vector2(1.2f, 2.2f);
    public float shardUpwardBias = 0.4f;
    [Range(0f, 1f)] public float shardDampen = 0.06f;
    public float shardGravity = 1.2f;
    public Vector2 shardLifetimeRange = new Vector2(0.3f, 0.5f);
    public Vector2Int shardSizePixelsRange = new Vector2Int(1, 2);
    public Color shardColorA = new Color(0.63f, 0.95f, 0.93f, 1f);
    public Color shardColorB = new Color(0.94f, 1f, 0.97f, 1f);
    // 가득 차는 순간 퍼지는 빛 알갱이: 수, 속도(월드/초), 수명(초)
    [Min(0)] public int pulseSparkleCount = 6;
    public Vector2 pulseSparkleSpeedRange = new Vector2(0.5f, 0.8f);
    public Vector2 pulseSparkleLifetimeRange = new Vector2(0.45f, 0.65f);
    // 동시에 떠 있는 알갱이 최대 수(시스템마다)와 한 프레임에 조각을 튀게 할 최대 타격 수(광역 타격 몰림 대비)
    [Min(16)] public int maxParticles = 256;
    [Min(1)] public int maxHitBurstsPerFrame = 6;
}

/// <summary>
/// 결정 장식 숨쉬기. 바닥 장식 타일맵(결정 새싹·돌·웅덩이 장식)을 이 스테이지에서만 전용 머티리얼(DecoCrystalBreathLit)로 그려,
/// 장식 타일마다 제 박자로 결정 픽셀만 은은하게 밝아졌다 돌아오게 한다. 숨쉬는 세기·박자는 머티리얼에서 정한다.
/// 공용 머티리얼은 건드리지 않고 이 타일맵의 머티리얼만 갈아 끼우며, 꺼진 스테이지는 원래 머티리얼 그대로다.
/// 용암 빛·숲의 숨결 머티리얼이 켜진 스테이지는 그쪽이 먼저다. 렌더링만 바뀌고 게임 동작에는 영향이 없다.
/// </summary>
[System.Serializable]
public class CrystalDecoBreathSettings
{
    public bool enabled = false;
    public Material material;
}

/// <summary>
/// 물 깊이. 물 타일 그림의 수면색·옆면색 픽셀을 물가에서 멀어질수록 몇 단계로 깊은 색으로 칠한다(물 타일 셰이더 WaterTilemapShader,
/// WaterDepth.hlsl, 물가 거리는 StageShoreMap). 단계 경계 근처만 텍셀 단위로 엇갈려 픽셀아트 그대로다.
/// 렌더링만 바뀌고 게임 동작에는 영향이 없다. enabled가 꺼져 있으면 셰이더 출력이 기존과 같다.
/// </summary>
[System.Serializable]
public class WaterDepthSettings
{
    public bool enabled = false;
    // 물 타일 그림의 수면색·옆면색(판정용)과 색 판정 허용 오차(채널별 최대 차이, 0~1)
    public Color surfaceColor = new Color(85f / 255f, 196f / 255f, 200f / 255f, 1f);
    public Color sideColor = new Color(65f / 255f, 166f / 255f, 169f / 255f, 1f);
    [Range(0f, 0.2f)] public float colorTolerance = 0.03f;
    // 가장 깊은 곳에서 곱하는 색
    public Color deepColor = new Color(0.66f, 0.82f, 0.92f, 1f);
    // 깊어지기 시작하는 거리와 가장 깊어지는 거리(물가에서 칸 수)
    [Min(0f)] public float startCells = 0.5f;
    [Min(0.1f)] public float fullCells = 4f;
    // 단계 수와 단계 경계가 텍셀 단위로 엇갈리는 폭(0이면 칼같이 나뉘고 1이면 단계 사이 전체가 엇갈린다)
    [Range(2, 8)] public int bands = 3;
    [Range(0f, 1f)] public float ditherWidth = 0.35f;
}
