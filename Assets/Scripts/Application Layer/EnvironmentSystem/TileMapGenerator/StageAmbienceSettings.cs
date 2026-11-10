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
/// 물 위 포자. 물 타일 그림의 수면색 픽셀 위에 포자 알갱이(텍셀 한 칸)가 떠올라 바람 방향으로 천천히 떠가다 사라진다
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
