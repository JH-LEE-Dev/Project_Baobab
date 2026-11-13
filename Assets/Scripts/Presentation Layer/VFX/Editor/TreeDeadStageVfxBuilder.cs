#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 2~4스테이지 나무가 쓰러질 때 재생되는 계열 전용 사망 이펙트 에셋 구성기. 여러 번 실행해도 같은 결과로 맞춘다.
/// 기존 사망 이펙트(TreeDeadEffect_Top 잎 폭발 + TreeDeadEffect_Bottom 연기·고리 폭발)와 같은 두 자리(수관 Top, 밑동 Bottom)에서
/// 계열마다 다른 한 쌍으로 재생된다. 1스테이지 나무는 기존 이펙트 그대로다.
///
///  - 포자 계열(뭉글 포자 숲: 포자솜·균사솜·방울솔) "퐁, 둥실"
///    수관: 솜뭉치(일부는 민트빛 갓을 얹은 채)가 퐁 부풀며 흩어져 공중에 머물고, 떨어져 나온 포자 갓이 둥실 떠오르며,
///          포자 티끌이 반짝이며 흩날린다. 세 계열 중 가장 느리고 가볍다.
///    밑동: 옅은 솜먼지가 바닥을 따라 낮게 퍼지고, 민트빛 고리가 바닥에 번지며, 나무껍질 부스러기가 낮게 튄다.
///  - 별 계열(별뿌리 숲: 별뿌리·달무리·은하알) "반짝, 사르르"
///    수관: 큰 도트 별이 번쩍이고, 밤하늘 수관이 별점이 박힌 조각으로 깨져 떨어지며, 박혀 있던 별(셋 중 하나는 초승달)이 풀려나
///          반짝이다 푸르게 식고, 별가루가 깜빡이며 밑동 높이까지 사르르 내려앉는다.
///    밑동: 점선 별빛 고리가 바닥에 번지고, 작은 별이 바닥에서 반짝이며, 껍질 부스러기가 튄다.
///  - 용암 계열(용암 숲: 잿불·용암수액·흑요) "쩍, 치익"
///    수관: 들쭉날쭉한 섬광과 함께 결정이 모서리가 달아오른 조각으로 깨져 무겁게 튀었다 떨어지며 식고, 흰빛 용암 방울이 포물선으로 튀며
///          주황 -> 빨강 -> 검붉게 식고, 그을음과 1px 불티가 피어오른다.
///    밑동: 끊어진 불고리가 바닥에 번지며 식고, 달아오른 숯 부스러기가 튀고, 그을음이 낮게 깔린다.
///    바닥 불씨 아이템(빛나는 구슬)과 헷갈리지 않도록 빛나는 것은 모두 1~2px 크기이고 1초 안에 식어 사라진다.
///    용암 계열에는 큰 폭발 한 쌍(VFX_TreeDead_MagmaBlast_*, 시트·머티리얼 따로)을 더해 함께 재생한다. BuildMagmaBlastSpec 참고.
///  - 세 계열 공통: 밑동에는 기본 밑동 폭발과 같은 큰 연기 덩이(BottomSmoke, 기존 VFX_ExplosionSmokes와 같은 모양·움직임)를 함께 낸다.
///  - 기본(Basic, 1스테이지 나무와 계열이 없는 나무): 기존 잎 폭발(VFX_LeafExplosion)·밑동 폭발(VFX_TreeExplosion)과 같은 구성·움직임을
///    이 규칙대로 다시 그린 한 쌍(VFX_TreeDead_Basic_*)을 TreeDeadEffect_Top/Bottom 태그로 재생한다. 기존 두 프리팹은 더 이상 쓰지 않는다.
///  - 임팩트(1~4스테이지 공통 구조, 색만 계열별): 수관 자리에서 큰 섬광(꽉 찬 원 -> 고리), 방사 충격선, 한 박자 늦은 반짝 팝이 터진다.
///    1스테이지(Leaf)는 기존 잎 폭발·밑동 폭발 위에 이 임팩트만 더한다(VFX_TreeDead_Leaf_Top, 밑동 프리팹 없음).
///    2~4스테이지는 수관 조각·밑동 부스러기가 더 빠르게 튀었다 강하게 감속하고, 바닥 충격파 고리가 더 크고(가로 19px) 굵게 번진다.
///  - 보석 나무(황금·다이아·무지개): 최종으로 쓰러질 때는 나무 종류 대신 보석 단계별 한 쌍(VFX_TreeDead_Gem{Gold,Diamond,Rainbow}_*)으로
///    재생한다. 보석 조각·임팩트·큰 반짝이 별·보석 가루 분수 + 보석 부스러기·충격파 고리·큰 연기 덩이. 색은 덮어쓰지 않는다.
///
/// 그림 규칙(픽셀 아트, 모든 이펙트 필수) - EnforcePixelRules가 빌드 때 모든 시스템에 강제한다
///  - JAGGIES·HUGGING·DOUBLE PIXEL 금지.
///    · 입자는 전부 32 PPU 격자의 정수 픽셀 도트다. 입자 크기 = 시트 칸 크기로 고정하고(배율 1), 회전하지 않으며, 크기 변화는 시트 프레임을
///      계단식으로 넘겨서 낸다(6px -> 5px -> 4px...). 셰이더(PixelParticleGlow)가 쿼드 꼭짓점을 격자에 맞춘다. 방향 변화는 90도 단위 뒤집기 행으로만 낸다.
///    · 고리·원은 정수 격자의 매끈한 꽉 찬 도형에서 외곽 한 줄만 뽑고 L자 모서리(겹 픽셀)를 지운다(RemoveDoubles). 점선은 둘레 순서대로 일정 간격만 남긴다.
///    · 방사선은 1:0, 1:1, 1:2, 2:1 기울기만 써서 계단 길이가 일정하다.
///    · 외곽선을 따라 붙는 명암 띠(동심 명암)를 쓰지 않는다. 명암은 한쪽(왼쪽 위) 빛 방향으로만 나눈다.
///  - 큰 입자(가로·세로 중 큰 쪽이 4px 이상)에 여러 색이 필요하면 입자 색 한 계열의 명암 최대 3단(W/L/M)만 쓴다. 다른 계열의 강조 픽셀은 쓰지 않는다.
///  - 너무 작은 입자(가로·세로 모두 3px 이하)는 한 색(W)만 쓴다. 그라데이션·명암 없음.
///  - 수명 동안의 색 변화: 작은 프레임이 하나라도 있는 시스템은 색·밝기가 수명 내내 고정이고 블룸을 받지 않는다(발광 배율 1).
///    큰 입자만 있는 시스템은 고정이거나, 한 계열 3색 이내를 보간 없이(Fixed) 계단식으로만 바꾼다.
///  - 사라짐은 모양이 줄어들거나 깜빡이며 꺼지는 것으로만 낸다(반투명 페이드 없음). 큰 연기 덩이만 고정 불투명도(_Opacity)를 쓴다.
///  - 발광은 정점 알파(BrandWrapGlow와 같은 규약, 큰 입자만)다.
///  - 각 프리팹의 루트(수관 조각 / 밑동 부스러기)는 실행 중에 나무별 이펙트 색(TreeVisualData.topVfxColor / bottomVfxColor)으로 덮어써
///    나무 종류마다 색이 다르다. 자식 시스템은 계열 고유의 색을 쓴다.
///  - 바닥 고리는 등각 2:1 타원(중점 알고리즘, 1px 굵기, 앞쪽 반은 밝게 뒤쪽 반은 어둡게)이고 ShockWave 레이어(나무·캐릭터 아래)에 깔린다.
///
/// 성능: 모든 시스템이 "시간 0 버스트 1회"만 쓰므로 SharedBurstEmitter로 공유 재생된다(활성화 비용 0, 시스템당 드로우 1).
/// 그루당 입자 수는 수관+밑동 합쳐 40~52개(연기 덩이 3개 포함)로, 기존 한 쌍(잎 폭발 130개 + 밑동 폭발 209개)보다 훨씬 적다.
///
/// 생성물
///  1) Assets/Graphics/VFX/TreeDead/TreeDead_Sheet.png: 모든 계열이 같이 쓰는 도트 모양 시트 하나(같은 모양 블록은 한 번만, Point/무압축)
///  2) Assets/Graphics/VFX/Materials/TreeDead/M_TreeDead_*.mat: PixelParticleGlow 머티리얼. 속성(시트 영역·강조색 등)이 같은 시스템끼리 공유한다
///  3) Assets/Prefabs/VFX/TreeDead/VFX_TreeDead_{계열}_{Top,Bottom}.prefab
///  4) GameInstaller.prefab: 기존 나무 사망 이펙트가 등록된 VFXComponent 풀에 태그를 등록한다(프리팹 값만, 미리 만들 인스턴스 0개)
///  이 빌드가 쓰지 않는 시트·머티리얼은 1), 2) 폴더에서 지운다(이 빌더 전용 폴더).
/// </summary>
public static class TreeDeadStageVfxBuilder
{
    private const string ShaderName = "Custom/VFX/PixelParticleGlow";
    private const string TextureFolder = "Assets/Graphics/VFX/TreeDead";
    private const string MaterialFolder = "Assets/Graphics/VFX/Materials/TreeDead";
    private const string PrefabFolder = "Assets/Prefabs/VFX/TreeDead";
    private const string ReferenceTopPrefabPath = "Assets/Prefabs/VFX/Basic/Impact/VFX_LeafExplosion.prefab";
    // 기본 한 쌍(Basic)이 모양을 그대로 옮겨 오는 기존 이펙트 시트(연기 덩이·바닥 임팩트). 빌드 때 PNG를 직접 읽는다.
    private const string SmokeSheetPath = "Assets/Graphics/VFX/Resources/Explosion/Explosion.png";
    private const string ImpactSheetPath = "Assets/Graphics/VFX/Resources/Basic/Impact_02.png";
    private const string InstallerPrefabPath ="Assets/Prefabs/Installer/Installer/GameInstaller.prefab";
    private const string ReferenceTag = "TreeDeadEffect_Top";
    private const string GroundSortingLayer = "ShockWave";

    private const float PixelsPerUnit = 32.0f;
    private const float GlowMax = 2.6f;

    // 시트 명암 단계(sRGB 회색). 입자 색에 곱해진다. 강조 픽셀은 노란색(R=1, B=0)으로 칠해 셰이더가 구분한다.
    private const byte ShadeW = 255;
    private const byte ShadeL = 219;
    private const byte ShadeM = 184;
    private const byte ShadeD = 143;

    // 렌더 순서(기존 잎 폭발 렌더러 순서 + 이 값): 뒤 티끌/그을음 < 몸통 조각 < 빛나는 요소 < 섬광
    private const int OrderBack = 0;
    private const int OrderBody = 1;
    private const int OrderGlow = 2;
    private const int OrderFlash = 3;

    private sealed class SystemSpec
    {
        public string name;
        public int cellPx = 8;
        public string[][][] rows;          // rows[행][프레임] = 모양(위 -> 아래 줄)
        public float[] frameStarts;        // 프레임별 시작 시점(수명 비율). null이면 균등
        public int count;
        public Vector2 lifetime;
        public Vector2 speed;              // 방출 타원 바깥 방향 초기 속도(유닛/초)
        public float shapeRadius;
        public float shapeYScale = 0.8f;   // 방출 원의 세로 비율(수관은 살짝, 바닥은 납작하게 눌린다)
        public float gravity;              // gravityModifier (1 = 9.81 유닛/초²)
        public float drag;                 // 선형 감속(0이면 끔)
        public Vector2 rise;               // 수명 내내 더해지는 세로 속도(입자별 랜덤, 위 +)
        public float noiseStrength;
        public float noiseFrequency = 1.0f;
        public float noiseScroll;
        public Color colorA = Color.white; // 시작 색(둘 사이 랜덤). 루트는 실행 중 나무 색으로 덮어쓴다(빌드 값은 미리보기 색)
        public Color colorB = Color.white;
        public Gradient overLifetime;      // RGB는 색 변화(곱), 알파는 발광 세기
        public bool bodyGlow = true;       // false면 몸통은 빛나지 않고 정점 알파는 강조 픽셀 세기만 정한다
        public Color accentColor = Color.white;
        public Vector2 accentGlow = Vector2.one; // 강조 픽셀 HDR 배율(정점 알파 0, 1)
        public bool ground;                // true면 바닥 레이어(ShockWave)에 그린다
        public int sortingOffset;
        public float sortingFudge;         // 같은 순서·같은 위치끼리의 그리는 순서를 고정한다(+면 먼저 = 아래). 머티리얼을 공유해도 순서가 머티리얼 ID에 좌우되지 않게
        public bool randomStartFrame;      // 깜빡임 박자를 입자마다 어긋나게 한다
        public Gradient randomStartGradient; // 있으면 시작 색을 이 그라디언트에서 입자마다 랜덤으로 고른다(colorA/B 대신. 보석 팔레트·무지개)
        public Vector2 driftX;             // 수명 내내 더해지는 가로 속도(입자별 랜덤, 기본 이펙트의 좌우 흩어짐)
        public Vector2 boxSize;            // 0이 아니면 원 대신 이 크기의 상자 안에서 낸다(속도는 화면 밖 방향이라 driftX·rise로만 움직인다)
        public float shapeArc = 360.0f;    // 방출 원의 호(도, 오른쪽에서 반시계). 180이면 위쪽 반원
        public bool shapeEdge;             // true면 방출 원의 둘레에서만 낸다
        public float opacity = 1.0f;       // 고정 불투명도(큰 연기 덩이만 1 미만)
    }

    private sealed class StageSpec
    {
        public string key;
        public SystemSpec[] top;    // [0]이 루트(수관 조각, 나무 top 색)
        public SystemSpec[] bottom; // [0]이 루트(밑동 부스러기, 나무 bottom 색)
        // 수관 한가운데 같은 정렬 순서로 정확히 겹치는 단일 입자 시스템의 그리는 순서(먼저 그리는 것부터, 이름).
        // 비워 두면 기본 순서(CenterDrawOrderDefault)를 쓴다. ApplyCenterDrawOrder 참고.
        public string[] centerDrawOrder;
        // 기본(Basic) 한 쌍은 기존 태그(TreeDeadEffect_Top/Bottom)와 기존 풀 크기를 그대로 이어받는다. 비워 두면 키로 태그를 만든다.
        public string topTag;
        public string bottomTag;
        public int initialPoolSize;
    }

    public static string TopTag(string _key) => "TreeDeadEffect_" + _key + "Top";
    public static string BottomTag(string _key) => "TreeDeadEffect_" + _key + "Bottom";
    private static string TopTag(StageSpec _stage) => string.IsNullOrEmpty(_stage.topTag) ? TopTag(_stage.key) : _stage.topTag;
    private static string BottomTag(StageSpec _stage) => string.IsNullOrEmpty(_stage.bottomTag) ? BottomTag(_stage.key) : _stage.bottomTag;

    [MenuItem("LumberBoy/VFX/Build Tree Dead Stage VFX")]
    public static void Build()
    {
        Shader shader = Shader.Find(ShaderName);
        if (null == shader)
        {
            Debug.LogError("[TreeDeadStageVfxBuilder] 셰이더를 찾지 못했습니다: " + ShaderName);
            return;
        }

        ParticleSystemRenderer referenceRenderer = LoadReferenceRenderer();
        if (null == referenceRenderer)
        {
            Debug.LogError("[TreeDeadStageVfxBuilder] 기준 잎 폭발 프리팹을 찾지 못했습니다: " + ReferenceTopPrefabPath);
            return;
        }

        EnsureFolder(TextureFolder);
        EnsureFolder(MaterialFolder);
        EnsureFolder(PrefabFolder);

        StageSpec[] stages =
        {
            BuildBasicSpec(), BuildLeafSpec(), BuildSporeSpec(), BuildStarSpec(), BuildMagmaSpec(),
            BuildGemSpec("GemGold", new[] { "FFF6C8", "F2D27A", "D9A93A" }, "FFF2A8", "FFF8D8", "FFE27A", "FFE45C", "FFB52E"),
            BuildGemSpec("GemDiamond", new[] { "F0FCFF", "A8E4FF", "6FB4F0" }, "E0FAFF", "FFFFFF", "9ED8FF", "C8FFFF", "7FC8FF"),
            BuildGemSpec("GemRainbow", new[] { "FFB8E0", "FFD8A8", "FFF6A8", "B8F0C0", "A8D8FF", "D0B8FF", "FFB8E0" }, "FFE8F8", "FFFFFF", "E8C8FF", "FFFFFF", "FFFFFF"),
        };
        // 계열·보석 밑동에는 기본 밑동 폭발과 같은 큰 연기 덩이를 더한다(기본 한 쌍은 이미 들어 있다. 1스테이지 Leaf는 밑동이 없다).
        for (int i = 0; i < stages.Length; i++)
        {
            if (0 < stages[i].bottom.Length && null == stages[i].bottomTag) stages[i].bottom = Concat(stages[i].bottom, new[] { BuildSmokeSpec() });
            ApplyCenterDrawOrder(stages[i]);
        }

        if (false == EnforcePixelRules(stages)) return;

        System.Collections.Generic.List<string> tags = new System.Collections.Generic.List<string>(stages.Length * 2);
        System.Collections.Generic.List<ParticleSystem> prefabs = new System.Collections.Generic.List<ParticleSystem>(stages.Length * 2);
        System.Collections.Generic.List<int> initialSizes = new System.Collections.Generic.List<int>(stages.Length * 2);

        // 모든 계열의 시스템을 한 줄로 모은다(계열 순서대로 수관 -> 밑동). 시트는 하나로 합치고 같은 모양 블록은 한 번만 그리며,
        // 속성이 모두 같은 머티리얼은 하나를 같이 쓴다(임팩트·보석 조각 등). 그려지는 결과는 계열별 시트일 때와 같다.
        System.Collections.Generic.List<SystemSpec> allSystems = new System.Collections.Generic.List<SystemSpec>(64);
        System.Collections.Generic.List<string> ownerKeys = new System.Collections.Generic.List<string>(64);
        for (int i = 0; i < stages.Length; i++)
        {
            foreach (SystemSpec spec in stages[i].top) { allSystems.Add(spec); ownerKeys.Add(stages[i].key); }
            foreach (SystemSpec spec in stages[i].bottom) { allSystems.Add(spec); ownerKeys.Add(stages[i].key); }
        }

        Vector4[] allRegions;
        Texture2D atlas = BuildAtlas(allSystems, ownerKeys, out allRegions, TextureFolder, AtlasName);
        if (null == atlas) return;
        Material[] allMaterials = BuildMaterials(allSystems, ownerKeys, shader, atlas, allRegions, MaterialFolder);

        int systemCursor = 0;
        for (int i = 0; i < stages.Length; i++)
        {
            StageSpec stage = stages[i];
            Material[] topMaterials = new Material[stage.top.Length];
            Material[] bottomMaterials = new Material[stage.bottom.Length];
            System.Array.Copy(allMaterials, systemCursor, topMaterials, 0, stage.top.Length);
            System.Array.Copy(allMaterials, systemCursor + stage.top.Length, bottomMaterials, 0, stage.bottom.Length);
            systemCursor += stage.top.Length + stage.bottom.Length;

            ParticleSystem topPrefab = BuildPrefab("VFX_TreeDead_" + stage.key + "_Top", stage.top, topMaterials, referenceRenderer);
            if (null == topPrefab) return;
            tags.Add(TopTag(stage));
            prefabs.Add(topPrefab);
            initialSizes.Add(stage.initialPoolSize);

            // 1스테이지(Leaf)는 수관 임팩트만 있고 밑동은 기본 밑동 폭발(Basic)을 그대로 쓴다.
            if (0 < stage.bottom.Length)
            {
                ParticleSystem bottomPrefab = BuildPrefab("VFX_TreeDead_" + stage.key + "_Bottom", stage.bottom, bottomMaterials, referenceRenderer);
                if (null == bottomPrefab) return;
                tags.Add(BottomTag(stage));
                prefabs.Add(bottomPrefab);
                initialSizes.Add(stage.initialPoolSize);
            }
        }

        // AssetDatabase.SaveAssets()는 에디터에 열려 있는 다른 에셋(UI 프리팹 등)의 변경까지 저장하므로 쓰지 않는다.
        // 머티리얼은 만들 때 각각 저장하고, 텍스처·프리팹은 쓰는 즉시 디스크에 기록된다.
        if (false == WireInstaller(tags.ToArray(), prefabs.ToArray(), initialSizes.ToArray())) return;

        // 예전 시트·합쳐진 머티리얼은 새 프리팹과 풀 연결이 모두 끝난 뒤에 지운다.
        // 도중에 실패하면 지우지 않고 남겨, 이전 프리팹이 머티리얼을 잃지 않게 한다.
        DeleteStaleAssets(atlas, allMaterials, TextureFolder, MaterialFolder);

        Debug.Log("[TreeDeadStageVfxBuilder] 완료: " + PrefabFolder + " / " + InstallerPrefabPath);

        // 용암 계열에 더해 터지는 폭발 한 쌍도 같이 맞춘다(시트·머티리얼 폴더가 따로라 위 정리와 서로 건드리지 않는다).
        BuildMagmaBlast();
    }

    // ───────────────────────── 용암 폭발(4스테이지 나무 추가 이펙트) ─────────────────────────
    // 용암 계열(잿불·용암수액·흑요) 나무가 쓰러질 때 계열 이펙트(VFX_TreeDead_Magma_*) 위에 더해 재생하는 큰 폭발 한 쌍. "콰광!"
    //  수관: 흰빛 불덩이가 부풀어 오르며 노랑 -> 주황 -> 빨강으로 식고 구멍이 뚫리며 흩어진다. 불꽃 덩이가 사방으로 터져 나가 그을음으로 식고,
    //        불티가 빠르게 튀었다 확 멈추며, 한 박자씩 늦게 작은 폭발이 둘레에서 연달아 터지고, 불씨가 흔들리며 솟아오른다.
    //  밑동: 바닥이 한 번 번쩍이고, 계열 고리보다 큰 굵은 불고리가 바닥을 따라 번지며 식는다(한 시스템).
//  가벼움: 그루당 입자 34개·시스템 6개, 노이즈 모듈 없음. 범위 공격으로 한꺼번에 쓰러질 때는 재생 쪽(InDungeonVFXManager)이 개수를 제한한다.
    //  빛나는 것은 모두 허공에서 1초 안에 식어 사라진다(바닥 불씨 아이템과 헷갈리지 않게 바닥에 남는 빛은 없다).
    //  색은 실행 중에 덮어쓰지 않는다(나무 색 대신 이 폭발의 고정 색).
    // 시트·머티리얼은 계열 시트와 따로 둔다. 이 폭발만 고쳐 다시 구워도 기존 계열 시트·머티리얼·프리팹은 바뀌지 않는다.
    private const string BlastKey = "MagmaBlast";
    private const string BlastTextureFolder = "Assets/Graphics/VFX/TreeDeadBlast";
    private const string BlastMaterialFolder = "Assets/Graphics/VFX/Materials/TreeDeadBlast";
    private const string BlastAtlasName = "TreeDead_MagmaBlast_Sheet";
    private const int BlastFireballCell = 33;
    private const int BlastRingCell = 53;

    [MenuItem("LumberBoy/VFX/Build Tree Dead Magma Blast VFX")]
    public static void BuildMagmaBlast()
    {
        Shader shader = Shader.Find(ShaderName);
        ParticleSystemRenderer referenceRenderer = LoadReferenceRenderer();
        if (null == shader || null == referenceRenderer)
        {
            Debug.LogError("[TreeDeadStageVfxBuilder] 용암 폭발: 셰이더 또는 기준 잎 폭발 프리팹을 찾지 못했습니다.");
            return;
        }

        EnsureFolder(BlastTextureFolder);
        EnsureFolder(BlastMaterialFolder);
        EnsureFolder(PrefabFolder);

        StageSpec blast = BuildMagmaBlastSpec();
        if (false == EnforcePixelRules(new[] { blast })) return;
        System.Collections.Generic.List<SystemSpec> systems = new System.Collections.Generic.List<SystemSpec>(16);
        System.Collections.Generic.List<string> owners = new System.Collections.Generic.List<string>(16);
        foreach (SystemSpec spec in blast.top) { systems.Add(spec); owners.Add(blast.key); }
        foreach (SystemSpec spec in blast.bottom) { systems.Add(spec); owners.Add(blast.key); }

        Vector4[] regions;
        Texture2D atlas = BuildAtlas(systems, owners, out regions, BlastTextureFolder, BlastAtlasName);
        if (null == atlas) return;
        Material[] materials = BuildMaterials(systems, owners, shader, atlas, regions, BlastMaterialFolder);

        Material[] topMaterials = new Material[blast.top.Length];
        Material[] bottomMaterials = new Material[blast.bottom.Length];
        System.Array.Copy(materials, 0, topMaterials, 0, blast.top.Length);
        System.Array.Copy(materials, blast.top.Length, bottomMaterials, 0, blast.bottom.Length);

        ParticleSystem topPrefab = BuildPrefab("VFX_TreeDead_" + blast.key + "_Top", blast.top, topMaterials, referenceRenderer);
        if (null == topPrefab) return;
        ParticleSystem bottomPrefab = BuildPrefab("VFX_TreeDead_" + blast.key + "_Bottom", blast.bottom, bottomMaterials, referenceRenderer);
        if (null == bottomPrefab) return;

        if (false == WireInstaller(new[] { TopTag(blast.key), BottomTag(blast.key) }, new[] { topPrefab, bottomPrefab }, new[] { 0, 0 })) return;
        DeleteStaleAssets(atlas, materials, BlastTextureFolder, BlastMaterialFolder);

        Debug.Log("[TreeDeadStageVfxBuilder] 용암 폭발 완료: " + PrefabFolder + "/VFX_TreeDead_" + blast.key + "_{Top,Bottom}");
    }

    private static StageSpec BuildMagmaBlastSpec()
    {
        // ── 수관 ──
        // 1) 불덩이(루트): 흰빛 심을 품은 불덩이가 부풀어 오르며 식고, 바깥부터 구멍이 뚫리며 흩어진다.
        SystemSpec fireball = new SystemSpec
        {
            name = "TopFireball",
            cellPx = BlastFireballCell,
            rows = new[]
            {
                new[] { BuildFireball(7, 0.5f, 0.0f, 0.0f, 11), BuildFireball(11, 0.32f, 0.0f, 0.0f, 12), BuildFireball(13, 0.0f, 0.1f, 0.0f, 13), BuildFireball(14, 0.0f, 0.35f, 0.2f, 14), BuildFireball(14, 0.0f, 0.65f, 0.45f, 15) },
                new[] { BuildFireball(7, 0.5f, 0.0f, 0.0f, 21), BuildFireball(11, 0.32f, 0.0f, 0.0f, 22), BuildFireball(13, 0.0f, 0.1f, 0.0f, 23), BuildFireball(14, 0.0f, 0.35f, 0.2f, 24), BuildFireball(14, 0.0f, 0.65f, 0.45f, 25) },
            },
            frameStarts = new[] { 0.0f, 0.1f, 0.28f, 0.52f, 0.76f },
            count = 1,
            lifetime = new Vector2(0.4f, 0.4f),
            colorA = Color.white,
            colorB = Color.white,
            overLifetime = Grad(
                new[] { Key(0.0f, "FFF6C8"), Key(0.18f, "FFC24A"), Key(0.45f, "FF6A22"), Key(0.75f, "B02C12"), Key(1.0f, "4E1712") },
                new[] { Alpha(0.0f, 1.0f), Alpha(0.35f, 0.75f), Alpha(0.7f, 0.25f), Alpha(1.0f, 0.0f) }),
            accentColor = Hex("FFFBE6"),
            accentGlow = new Vector2(1.6f, 3.4f),
            sortingOffset = OrderFlash,
        };

        // 2) 불꽃 덩이: 사방으로 터져 나가 강하게 멈추며, 주황 -> 빨강 -> 그을음으로 식고 작아진다.
        SystemSpec flame = new SystemSpec
        {
            name = "TopFlame",
            rows = new[]
            {
                new[] { Puff6, Puff5, Puff4, Puff3, Block2Dark },
                new[] { Puff5, Puff5, Puff4, Puff3, DotM },
            },
            frameStarts = new[] { 0.0f, 0.3f, 0.5f, 0.7f, 0.88f },
            count = 8,
            lifetime = new Vector2(0.45f, 0.65f),
            speed = new Vector2(2.6f, 4.6f),
            shapeRadius = 0.2f,
            drag = 5.0f,
            rise = new Vector2(0.3f, 0.8f),
            colorA = Color.white,
            colorB = Color.white,
            overLifetime = Grad(
                new[] { Key(0.0f, "FFE9A0"), Key(0.2f, "FFA030"), Key(0.45f, "E8501E"), Key(0.7f, "6A2A20"), Key(1.0f, "3A2A2E") },
                new[] { Alpha(0.0f, 0.9f), Alpha(0.25f, 0.6f), Alpha(0.55f, 0.0f), Alpha(1.0f, 0.0f) }),
            sortingOffset = OrderGlow,
        };

        // 3) 불티: 빠르게 사방으로 튀었다 확 멈추고, 떨어지며 식는다.
        SystemSpec spark = new SystemSpec
        {
            name = "TopSpark",
            rows = new[]
            {
                new[] { Block2, Dot, Dot, DotL },
                new[] { Dot, Dot, DotL, Empty },
            },
            count = 14,
            lifetime = new Vector2(0.32f, 0.55f),
            speed = new Vector2(4.5f, 8.0f),
            shapeRadius = 0.25f,
            gravity = 0.5f,
            drag = 6.0f,
            colorA = Color.white,
            colorB = Color.white,
            overLifetime = Grad(
                new[] { Key(0.0f, "FFFFFF"), Key(0.2f, "FFE070"), Key(0.5f, "FF8A30"), Key(0.8f, "C83A18"), Key(1.0f, "5A1A10") },
                new[] { Alpha(0.0f, 1.0f), Alpha(0.3f, 0.9f), Alpha(0.7f, 0.45f), Alpha(1.0f, 0.0f) }),
            sortingOffset = OrderGlow,
        };

        // 4) 연쇄 폭발: 한 박자씩 늦게(앞쪽 빈 프레임) 불덩이 둘레에서 작은 폭발이 연달아 터진다.
        SystemSpec pop = new SystemSpec
        {
            name = "TopPop",
            cellPx = SparkleCell,
            rows = new[]
            {
                new[] { Empty, Burst7, Diamond5, Plus3, Dot, Empty, Empty, Empty },
                new[] { Empty, Empty, Empty, Burst7, Diamond5, Plus3, Dot, Empty },
                new[] { Empty, Empty, Empty, Empty, Empty, Burst7, Diamond5, Plus3 },
            },
            count = 4,
            lifetime = new Vector2(0.42f, 0.5f),
            speed = new Vector2(0.2f, 0.6f),
            shapeRadius = 0.6f,
            drag = 3.0f,
            colorA = Hex("FFF2C0"),
            colorB = Color.white,
            overLifetime = Grad(new[] { Key(0.0f, "FFFFFF"), Key(1.0f, "FFB25A") }, new[] { Alpha(0.0f, 1.0f), Alpha(1.0f, 0.6f) }),
            sortingOffset = OrderFlash,
        };

        // 5) 불씨: 깜빡이며 솟아오르다 식는다. 흔들림(노이즈 모듈)은 입자마다 계산이 가장 무거운 모듈이라 쓰지 않고,
        //    처음 튀는 방향·솟는 빠르기를 입자마다 달리해 흩날리는 느낌만 낸다(계열 이펙트의 불티가 이미 흔들리며 날린다).
        SystemSpec ember = new SystemSpec
        {
            name = "TopEmber",
            rows = new[]
            {
                new[] { Dot, Dot, Empty, Dot, Dot, Empty },
                new[] { Dot, Empty, Dot, Dot, Empty, Dot },
            },
            count = 6,
            lifetime = new Vector2(0.7f, 1.0f),
            speed = new Vector2(1.0f, 2.2f),
            shapeRadius = 0.3f,
            drag = 2.5f,
            rise = new Vector2(0.8f, 1.7f),
            colorA = Color.white,
            colorB = Color.white,
            overLifetime = Grad(
                new[] { Key(0.0f, "FFE08A"), Key(0.4f, "FF7A2E"), Key(0.8f, "B8321A"), Key(1.0f, "4A1A12") },
                new[] { Alpha(0.0f, 0.9f), Alpha(0.4f, 0.6f), Alpha(0.8f, 0.2f), Alpha(1.0f, 0.0f) }),
            sortingOffset = OrderGlow,
            randomStartFrame = true,
        };

        // ── 밑동 ──
        // 바닥 불고리(루트, 시스템 하나): 밑동 둘레 바닥이 한 번 환하게 번쩍인 뒤, 번쩍임이 성글게 풀리는 동안 굵은 고리가
        // 계열 고리(가로 19px)보다 멀리(26px) 번지며 식는다. 번쩍임과 고리를 한 입자의 프레임에 같이 그려 시스템·드로우를 하나로 줄였다.
        int[] ringRadii = { 0, 15, 21, 26 };
        string[][] ringFrames = new string[ringRadii.Length][];
        ringFrames[0] = BuildFilledEllipse(10, 0.0f);
        for (int i = 1; i < ringRadii.Length; i++)
        {
            int sparse = (ringRadii.Length - 1 == i) ? 1 : 0;
            ringFrames[i] = (0 == sparse) ? BuildThickRing(ringRadii[i], ERingStyle.Solid) : BuildRing(ringRadii[i], ERingStyle.Solid, sparse);
        }

        // 번쩍임이 풀리는 모습을 고리 안쪽에 겹친다(고리 픽셀이 앞선다).
        ringFrames[1] = OverlayCentered(ringFrames[1], BuildFilledEllipse(13, 0.45f));
        ringFrames[2] = OverlayCentered(ringFrames[2], BuildFilledEllipse(14, 0.8f));

        SystemSpec ring = new SystemSpec
        {
            name = "BottomBlastRing",
            cellPx = BlastRingCell,
            rows = new[] { ringFrames },
            frameStarts = new[] { 0.0f, 0.16f, 0.4f, 0.7f },
            count = 1,
            lifetime = new Vector2(0.36f, 0.36f),
            colorA = Color.white,
            colorB = Color.white,
            overLifetime = Grad(
                new[] { Key(0.0f, "FFF6D0"), Key(0.3f, "FF9A3A"), Key(0.65f, "D8481E"), Key(1.0f, "7A2014") },
                new[] { Alpha(0.0f, 1.0f), Alpha(0.5f, 0.55f), Alpha(1.0f, 0.0f) }),
            ground = true,
        };

        return new StageSpec
        {
            key = BlastKey,
            top = new[] { fireball, flame, spark, pop, ember },
            bottom = new[] { ring },
        };
    }

    // 불덩이 한 프레임(가로세로 2r+5). 둥근 몸통 둘레에 작은 원 덩이를 붙여 뭉게뭉게한 외곽을 만든다(모두 매끈한 정수 원이라 외곽 계단이 고르다).
    // 명암은 동심 띠 대신 왼쪽 위 빛 방향으로만 세 단(W/L/M)으로 나눈다(외곽을 따라 붙는 띠 없음). _core: 가운데부터 이 비율까지는 가장 밝은 W.
    // _holes: 빠지는 정도(바깥일수록 더 빠진다). 무작위 대신 4x4 순서 디더로 빼서 흩어진 점이 고른 격자 무늬로만 남는다.
    // _dim: 0이면 그대로, 클수록 명암을 어둡게 민다(식는 프레임). _seed: 둘레 덩이 배치 갈래.
    private static string[] BuildFireball(int _r, float _core, float _holes, float _dim, int _seed)
    {
        int half = _r + 2;
        int size = 2 * half + 1;
        bool[,] body = new bool[size, size];
        StampDisc(body, half, half, _r);

        // 둘레 덩이: 반지름의 40% 크기 원 다섯 개를 몸통 가장자리 안쪽에 고른 간격(갈래마다 시작 각만 다르게)으로 붙인다.
        int lobeR = Mathf.Max(1, Mathf.RoundToInt(_r * 0.4f));
        float startAngle = BlastHash01(_seed, 7, 2) * Mathf.PI * 2.0f;
        for (int i = 0; i < 5; i++)
        {
            float angle = startAngle + i * Mathf.PI * 2.0f / 5.0f;
            int cx = half + Mathf.RoundToInt(Mathf.Cos(angle) * (_r - lobeR + 1));
            int cy = half - Mathf.RoundToInt(Mathf.Sin(angle) * (_r - lobeR + 1));
            StampDisc(body, cx, cy, lobeR);
        }

        string[] lines = new string[size];
        for (int y = 0; y < size; y++)
        {
            char[] row = new char[size];
            for (int x = 0; x < size; x++)
            {
                char c = '.';
                if (body[y, x])
                {
                    float dx = x - half;
                    float dy = half - y;
                    float t = Mathf.Sqrt(dx * dx + dy * dy) / (_r + 1.0f);
                    float light = (-0.6f * dx + 0.8f * dy) / (_r + 1.0f); // 왼쪽 위가 +
                    float shade = 0.5f * t - 0.5f * light + _dim;
                    if (t < _core || shade < 0.15f) c = 'W';
                    else if (shade < 0.45f) c = 'L';
                    else c = 'M';

                    if (0.0f < _holes && OrderedDither(x, y) < _holes * (0.35f + t)) c = '.';
                }

                row[x] = c;
            }

            lines[y] = new string(row);
        }

        return lines;
    }

    // 정수 원(반지름 r, 픽셀 중심이 r + 0.5 안)을 마스크에 찍는다.
    private static void StampDisc(bool[,] _mask, int _cx, int _cy, int _r)
    {
        float limit = (_r + 0.5f) * (_r + 0.5f);
        for (int y = _cy - _r; y <= _cy + _r; y++)
        {
            for (int x = _cx - _r; x <= _cx + _r; x++)
            {
                if (y < 0 || x < 0 || y >= _mask.GetLength(0) || x >= _mask.GetLength(1)) continue;
                if ((x - _cx) * (x - _cx) + (y - _cy) * (y - _cy) <= limit) _mask[y, x] = true;
            }
        }
    }

    // 4x4 순서 디더 문턱값(0~1). 같은 위치는 늘 같은 값이라 빠지는 픽셀이 고른 격자 무늬로 늘어난다.
    private static float OrderedDither(int _x, int _y)
    {
        int[] bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        return (bayer[(_y & 3) * 4 + (_x & 3)] + 0.5f) / 16.0f;
    }

    // 바닥에 누운 꽉 찬 타원(가로 반지름 a, 세로 a/2, 가로 2a+1), 한 색. _holes만큼 순서 디더로 픽셀이 빠진다(바깥일수록 더).
    private static string[] BuildFilledEllipse(int _a, float _holes)
    {
        int b = _a / 2;
        bool[,] mask = FilledEllipseMask(_a, b);
        string[] lines = new string[mask.GetLength(0)];
        for (int y = 0; y < mask.GetLength(0); y++)
        {
            char[] row = new char[mask.GetLength(1)];
            for (int x = 0; x < mask.GetLength(1); x++)
            {
                char c = mask[y, x] ? 'W' : '.';
                if ('.' != c && 0.0f < _holes)
                {
                    float nx = (x - _a) / (_a + 0.5f);
                    float ny = (y - b) / (b + 0.5f);
                    if (OrderedDither(x, y) < _holes * (0.5f + Mathf.Sqrt(nx * nx + ny * ny))) c = '.';
                }

                row[x] = c;
            }

            lines[y] = new string(row);
        }

        return lines;
    }

    // 정수 둘과 갈래 번호로 0~1 값(같은 입력이면 늘 같은 값이라 빌드할 때마다 모양이 같다).
    private static float BlastHash01(int _x, int _y, int _channel)
    {
        unchecked
        {
            uint h = (uint)_x * 374761393u + (uint)_y * 668265263u + (uint)_channel * 2246822519u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFFu) / 16777216f;
        }
    }

    // ───────────────────────── 모양 ─────────────────────────
    // 문자: '.' 빈칸, W/L/M/D 몸통 명암(밝음 -> 어두움), A 강조 픽셀(머티리얼 고정 색)

    private static readonly string[] Empty = { "." };
    private static readonly string[] Dot = { "W" };
    private static readonly string[] DotL = { "L" };
    private static readonly string[] DotM = { "M" };
    private static readonly string[] DotA = { "A" };
    private static readonly string[] Block2 = { "WL", "LM" };
    private static readonly string[] Block2Dark = { "LM", "MD" };
    private static readonly string[] Plus3 = { ".W.", "WWW", ".W." };
    private static readonly string[] Plus3Soft = { ".L.", "LWL", ".L." };
    private static readonly string[] Diamond3 = { ".W.", "WLW", ".W." };

    // 보석 조각(깎인 면 명암 + 왼쪽 위 반짝임 한 칸 A)
    private static readonly string[] GemL = { "...A...", "..AWL..", ".WWLLM.", "WLLLMMD", ".LLMMD.", "..MMD..", "...D..." };
    private static readonly string[] GemA = { "..A..", ".WLL.", "WLLMM", ".LMD.", "..D.." };
    private static readonly string[] GemB = { "AW..", "WLM.", ".LMD", "..MD" };
    private static readonly string[] GemC = { "..A..", ".WLM.", "WLLMD" };
    private static readonly string[] Gem3 = { ".A.", "WLM", ".D." };
    private static readonly string[] Gem2 = { "AL", "MD" };

    // 포자 솜뭉치(왼쪽 위가 밝고 오른쪽 아래가 어둡다). Cap 붙은 것은 나무처럼 민트빛 갓을 얹고 있다.
    private static readonly string[] PuffCap6 =
    {
        ".AAA..",
        "AAAALM",
        "WWLLLM",
        "LLLLMM",
        "LLMMMD",
        ".MMMD.",
    };
    private static readonly string[] PuffCap5 =
    {
        ".AAA.",
        "AAALM",
        "WLLLM",
        "LLMMD",
        ".MMD.",
    };
    private static readonly string[] Puff6 =
    {
        ".WWLL.",
        "WWWLLM",
        "WWLLLM",
        "LLLLMM",
        "LLMMMD",
        ".MMMD.",
    };
    private static readonly string[] Puff5 =
    {
        ".WWL.",
        "WWLLM",
        "WLLLM",
        "LLMMD",
        ".MMD.",
    };
    private static readonly string[] Puff4 =
    {
        ".WL.",
        "WWLM",
        "LLMD",
        ".MD.",
    };
    private static readonly string[] Puff3 =
    {
        ".W.",
        "WLM",
        ".M.",
    };
    private static readonly string[] Cluster7 =
    {
        ".WL.WL.",
        "WWLWLLM",
        "WLLLLLM",
        "LLMMMMD",
        ".MD.MD.",
    };

    // 포자 갓(나무 위 민트빛 덩이)
    private static readonly string[] Cap5 =
    {
        ".WWW.",
        "WWWWL",
        "LWWLL",
        ".LLM.",
    };
    private static readonly string[] Cap4 =
    {
        ".WW.",
        "WWWL",
        ".LM.",
    };
    private static readonly string[] Cap3 =
    {
        "WWL",
        ".L.",
    };

    // 나무껍질 부스러기
    private static readonly string[] Chip2 = { "WL", "LM" };
    private static readonly string[] Chip21 = { "WL" };
    private static readonly string[] Chip12 = { "W", "M" };
    private static readonly string[] HotChip2 = { "AL", "LM" };
    private static readonly string[] HotChip21 = { "AL" };
    private static readonly string[] HotChip12 = { "A", "M" };

    // 밤하늘 수관 조각(가운데 별점)
    private static readonly string[] Shard3 = { "WL.", "LAM", ".MD" };
    private static readonly string[] Shard32 = { "WAL", ".MD" };
    private static readonly string[] Shard23 = { "W.", "LA", "MD" };
    private static readonly string[] ShardSmall = { "AL", "LM" };

    // 별
    private static readonly string[] Star9 =
    {
        "....L....",
        "....W....",
        "....W....",
        "...WWW...",
        "LWWWWWWWL",
        "...WWW...",
        "....W....",
        "....W....",
        "....L....",
    };
    private static readonly string[] Star7 =
    {
        "...L...",
        "...W...",
        "..WWW..",
        "LWWWWWL",
        "..WWW..",
        "...W...",
        "...L...",
    };
    private static readonly string[] Plus5 =
    {
        "..W..",
        "..W..",
        "WWWWW",
        "..W..",
        "..W..",
    };
    private static readonly string[] Star5 =
    {
        "..L..",
        "..W..",
        "LWWWL",
        "..W..",
        "..L..",
    };
    private static readonly string[] Crescent =
    {
        ".WW",
        "WL.",
        "WL.",
        ".WW",
    };
    private static readonly string[] CrescentSmall = { ".W", "W.", ".W" };

    // 용암 숲 결정 조각(왼쪽 위 모서리가 달아올라 있다)
    private static readonly string[] CrystalA = { ".A.", "ALM", "LLM", "LMD", ".D." };
    private static readonly string[] CrystalA2 = { "AL", "LM", "MD" };
    private static readonly string[] CrystalB = { "AA..", "LLA.", "LMMD" };
    private static readonly string[] CrystalB2 = { "AL.", "LMD" };
    private static readonly string[] CrystalC = { "A.", "AL", "LM", ".D" };
    private static readonly string[] Teardrop = { "W.", "WL", "LM" };
    private static readonly string[] Burst7 =
    {
        "...W...",
        ".W.W.W.",
        "..WWW..",
        "WWWWWWW",
        "..WWW..",
        ".W.W.W.",
        "...W...",
    };
    private static readonly string[] Diamond5 =
    {
        "..W..",
        ".WWW.",
        "WWWWW",
        ".WWW.",
        "..W..",
    };
    private static readonly string[] Ash4 = { ".LL.", "LWLM", "LLMM", ".MM." };

    // 바닥 고리: 등각 2:1 타원(가로 반지름 a, 세로 반지름 a/2). 칸 26px에 a=12까지 들어간다.
    private const int RingCell = 41;
    private enum ERingStyle { Solid, Dotted, Broken }

    // ───────────────────────── 임팩트(쓰러지는 순간, 네 계열 공통 구조) ─────────────────────────
    // 수관 자리에서 한 번에 터진다. 색만 계열별로 다르다.
    //  1) 임팩트 섬광: 꽉 찬 원(지름 21px)이 1~2프레임 번쩍인 뒤 굵은 고리 -> 가는 고리 -> 점선 고리로 퍼지며 풀린다(강한 HDR 발광).
    //  2) 충격선: 수관 둘레로 8가닥 방사선이 바깥으로 뻗어 나가며 짧아지다 끝점만 남고 사라진다(두 가지 각도 배치 중 랜덤).
    //  3) 반짝 팝: 한 박자 늦게(앞쪽 빈 프레임으로 지연) 반짝이 별이 수관 둘레에서 차례로 톡톡 터진다.
    private const int ImpactFlashCell = 27;
    private const int ImpactLineCell = 49;
    private const int SparkleCell = 9;
    // 수관 한가운데 같은 섬광 순서로 정확히 겹치는 단일 입자(충격선·임팩트 섬광·계열 섬광)는 같은 정렬 순서·같은 위치라
    // 보정이 없으면 머티리얼에 따라 그리는 순서가 갈린다(머티리얼을 계열 사이에 공유하면 겹친 픽셀의 발광이 바뀐다).
    // 계열마다 선언한 순서를 거리 보정(sortingFudge)으로 못 박는다. 기본 순서와 별빛의 순서는 머티리얼을 계열마다 따로 쓰던 때
    // 실제로 그려지던 순서 그대로다. 공유 이미터에서 여러 그루가 0.2초 안에 서로 다른 자리에서 쓰러지면 시스템 bounds 중심이
    // 달라져 거리 정렬이 이 보정보다 앞선다(최적화 전과 같은 성질).
    private static readonly string[] CenterDrawOrderDefault = { "ImpactLines", "ImpactFlash", "TopFlash" };
    private const string CenterDrawOrderAnchor = "ImpactFlash"; // 보정 0인 기준(나머지는 앞이면 +, 뒤면 -)
    private const float CenterDrawOrderStep = 0.0001f;

    // 계열의 수관 시스템에 그리는 순서 보정을 매긴다. 목록에 없거나 계열에 없는 시스템은 보정 0.
    private static void ApplyCenterDrawOrder(StageSpec _stage)
    {
        string[] order = null != _stage.centerDrawOrder ? _stage.centerDrawOrder : CenterDrawOrderDefault;
        int anchor = System.Array.IndexOf(order, CenterDrawOrderAnchor);
        if (anchor < 0) anchor = 0;

        foreach (SystemSpec spec in _stage.top)
        {
            int index = System.Array.IndexOf(order, spec.name);
            spec.sortingFudge = index < 0 ? 0.0f : CenterDrawOrderStep * (anchor - index);
        }
    }

    private static SystemSpec[] BuildImpactSpecs(string _flashTint, string _lineColor, string _lineTint, string _sparkleColor, string _sparkleTint, int _sparkleCount = 6)
    {
        SystemSpec flash = new SystemSpec
        {
            name = "ImpactFlash",
            cellPx = ImpactFlashCell,
            rows = new[] { new[] { BuildDisc(10), BuildCircle(12, 3, false), BuildCircle(13, 2, false), BuildCircle(13, 1, true) } },
            frameStarts = new[] { 0.0f, 0.22f, 0.5f, 0.78f },
            count = 1,
            lifetime = new Vector2(0.16f, 0.16f),
            colorA = Color.white,
            colorB = Color.white,
            overLifetime = Grad(new[] { Key(0.0f, "FFFFFF"), Key(0.3f, _flashTint), Key(1.0f, _flashTint) }, new[] { Alpha(0.0f, 1.0f), Alpha(1.0f, 0.35f) }),
            sortingOffset = OrderFlash,
        };

        // 방사선 방향은 계단 길이가 일정한 기울기(1:0, 1:1 / 2:1, 1:2)만 쓴다. 두 배치 중 랜덤(행).
        Vector2Int[][] lineAngles =
        {
            new[] { new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(0, 1), new Vector2Int(-1, 1), new Vector2Int(-1, 0), new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1) },
            new[] { new Vector2Int(2, 1), new Vector2Int(1, 2), new Vector2Int(-1, 2), new Vector2Int(-2, 1), new Vector2Int(-2, -1), new Vector2Int(-1, -2), new Vector2Int(1, -2), new Vector2Int(2, -1) },
        };
        float[][] lineLengths =
        {
            new[] { 1.0f, 0.7f, 1.0f, 0.8f, 1.0f, 0.65f, 0.95f, 0.75f },
            new[] { 0.75f, 1.0f, 0.7f, 1.0f, 0.8f, 1.0f, 0.65f, 0.95f },
        };
        Vector2[] lineSpans = { new Vector2(7, 14), new Vector2(10, 19), new Vector2(14, 23), new Vector2(19, 24), new Vector2(23, 24) };
        string[][][] lineRows = new string[lineAngles.Length][][];
        for (int r = 0; r < lineAngles.Length; r++)
        {
            lineRows[r] = new string[lineSpans.Length][];
            for (int f = 0; f < lineSpans.Length; f++)
            {
                lineRows[r][f] = BuildImpactLines(lineAngles[r], lineLengths[r], lineSpans[f].x, lineSpans[f].y, f == lineSpans.Length - 1);
            }
        }

        SystemSpec lines = new SystemSpec
        {
            name = "ImpactLines",
            cellPx = ImpactLineCell,
            rows = lineRows,
            frameStarts = new[] { 0.0f, 0.16f, 0.36f, 0.6f, 0.82f },
            count = 1,
            lifetime = new Vector2(0.22f, 0.22f),
            colorA = Hex(_lineColor),
            colorB = Hex(_lineColor),
            overLifetime = Grad(new[] { Key(0.0f, "FFFFFF"), Key(1.0f, _lineTint) }, new[] { Alpha(0.0f, 1.0f), Alpha(1.0f, 0.3f) }),
            sortingOffset = OrderFlash,
        };

        SystemSpec sparkle = new SystemSpec
        {
            name = "ImpactSparkle",
            cellPx = SparkleCell,
            rows = new[]
            {
                new[] { Empty, Plus3, Star7, Plus5, Plus3, Dot, Empty },
                new[] { Empty, Empty, Plus3, Star7, Plus5, Plus3, Dot },
                new[] { Empty, Empty, Empty, Plus3, Star7, Plus5, Dot },
            },
            count = _sparkleCount,
            lifetime = new Vector2(0.5f, 0.6f),
            speed = new Vector2(1.4f, 2.4f),
            shapeRadius = 0.3f,
            drag = 4.5f,
            rise = new Vector2(0.1f, 0.3f),
            colorA = Hex(_sparkleColor),
            colorB = Color.white,
            overLifetime = Grad(new[] { Key(0.0f, "FFFFFF"), Key(1.0f, _sparkleTint) }, new[] { Alpha(0.0f, 1.0f), Alpha(1.0f, 0.4f) }),
            sortingOffset = OrderFlash,
        };

        return new[] { flash, lines, sparkle };
    }

    // ───────────────────────── 매끈한 도형(재기·겹 픽셀 없이) ─────────────────────────
    // 모든 원·타원은 "픽셀 중심이 (반지름 + 0.5) 타원 안이면 칠한다"는 한 규칙으로 꽉 찬 모양을 만들고, 고리는 그 외곽 한 줄
    // (4방향 이웃 중 하나라도 바깥인 픽셀)만 뽑은 뒤 L자로 겹친 모서리 픽셀을 지운다. 계단 길이가 둘레를 따라 고르게 늘고 준다.

    // 가로 반지름 _a, 세로 반지름 _b인 꽉 찬 타원(가로 2a+1, 세로 2b+1). [y, x], y 0이 맨 위.
    private static bool[,] FilledEllipseMask(int _a, int _b)
    {
        int w = 2 * _a + 1;
        int h = 2 * _b + 1;
        bool[,] mask = new bool[h, w];
        float ra = _a + 0.5f;
        float rb = _b + 0.5f;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float nx = (x - _a) / ra;
                float ny = (y - _b) / rb;
                mask[y, x] = nx * nx + ny * ny <= 1.0f;
            }
        }

        return mask;
    }

    private static bool MaskAt(bool[,] _mask, int _y, int _x)
    {
        return 0 <= _y && _y < _mask.GetLength(0) && 0 <= _x && _x < _mask.GetLength(1) && _mask[_y, _x];
    }

    // 꽉 찬 모양의 외곽 한 줄(4방향 이웃 중 하나라도 비어 있는 칠한 픽셀)을 뽑고 겹 픽셀을 지운다.
    private static bool[,] OutlineMask(bool[,] _filled)
    {
        int h = _filled.GetLength(0);
        int w = _filled.GetLength(1);
        bool[,] outline = new bool[h, w];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (false == _filled[y, x]) continue;
                outline[y, x] = false == MaskAt(_filled, y - 1, x) || false == MaskAt(_filled, y + 1, x)
                    || false == MaskAt(_filled, y, x - 1) || false == MaskAt(_filled, y, x + 1);
            }
        }

        RemoveDoubles(outline);
        return outline;
    }

    // 1px 선의 겹 픽셀(DOUBLE PIXEL) 제거: 가로 이웃 하나·세로 이웃 하나만 붙어 L자 모서리를 이루는 픽셀은
    // 지워도 두 이웃이 대각선으로 이어지므로 지운다(이웃이 그 둘뿐일 때만 지워 선이 끊기지 않는다).
    private static void RemoveDoubles(bool[,] _line)
    {
        int h = _line.GetLength(0);
        int w = _line.GetLength(1);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (false == _line[y, x]) continue;

                int neighbors = 0;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if ((0 != dx || 0 != dy) && MaskAt(_line, y + dy, x + dx)) neighbors++;
                    }
                }

                if (2 != neighbors) continue;

                bool horizontal = MaskAt(_line, y, x - 1) || MaskAt(_line, y, x + 1);
                bool vertical = MaskAt(_line, y - 1, x) || MaskAt(_line, y + 1, x);
                if (horizontal && vertical) _line[y, x] = false;
            }
        }
    }

    // 고리 픽셀을 둘레 순서(타원 각도)로 늘어놓는다. 점선은 이 순서에서 일정 간격으로 남겨 간격이 고르다.
    private static System.Collections.Generic.List<Vector2Int> OrderAround(bool[,] _mask)
    {
        int h = _mask.GetLength(0);
        int w = _mask.GetLength(1);
        float cx = (w - 1) * 0.5f;
        float cy = (h - 1) * 0.5f;
        float aspect = (float)w / h;
        System.Collections.Generic.List<Vector2Int> points = new System.Collections.Generic.List<Vector2Int>(64);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (_mask[y, x]) points.Add(new Vector2Int(x, y));
            }
        }

        points.Sort((p, q) => Mathf.Atan2((cy - p.y) * aspect, p.x - cx).CompareTo(Mathf.Atan2((cy - q.y) * aspect, q.x - cx)));
        return points;
    }

    // 마스크를 모양 문자열로 바꾼다. 위쪽 절반(뒤쪽)은 _back, 나머지는 _front로 칠한다.
    private static string[] MaskToShape(bool[,] _mask, char _front, char _back)
    {
        int h = _mask.GetLength(0);
        int w = _mask.GetLength(1);
        int middle = h / 2;
        string[] lines = new string[h];
        for (int y = 0; y < h; y++)
        {
            char[] row = new char[w];
            for (int x = 0; x < w; x++) row[x] = _mask[y, x] ? (y < middle ? _back : _front) : '.';
            lines[y] = new string(row);
        }

        return lines;
    }

    // 둘레 순서에서 _step개마다 하나만 남긴다(_step 1이면 그대로).
    private static bool[,] KeepEvery(bool[,] _mask, int _step)
    {
        if (_step <= 1) return _mask;
        bool[,] result = new bool[_mask.GetLength(0), _mask.GetLength(1)];
        System.Collections.Generic.List<Vector2Int> ordered = OrderAround(_mask);
        for (int i = 0; i < ordered.Count; i += _step) result[ordered[i].y, ordered[i].x] = true;
        return result;
    }

    // 꽉 찬 원(지름 2r+1), 한 색.
    private static string[] BuildDisc(int _r)
    {
        return MaskToShape(FilledEllipseMask(_r, _r), 'W', 'W');
    }

    // 굵기 _thick인 원 고리(지름 2r+1). 굵기 1은 꽉 찬 원의 외곽 한 줄, 그보다 굵으면 반지름 r 원에서 r - thick 원을 뺀다.
    // _sparse면 둘레 순서로 한 칸 건너 하나만 남긴다.
    private static string[] BuildCircle(int _r, int _thick, bool _sparse)
    {
        bool[,] ring;
        if (_thick <= 1)
        {
            ring = OutlineMask(FilledEllipseMask(_r, _r));
        }
        else
        {
            ring = FilledEllipseMask(_r, _r);
            bool[,] inner = FilledEllipseMask(_r - _thick, _r - _thick);
            for (int y = 0; y < inner.GetLength(0); y++)
            {
                for (int x = 0; x < inner.GetLength(1); x++)
                {
                    if (inner[y, x]) ring[y + _thick, x + _thick] = false;
                }
            }
        }

        if (_sparse) ring = KeepEvery(ring, 2);
        return MaskToShape(ring, 'W', 'W');
    }

    // 방사 충격선: 방향마다 반지름 _rIn -> (_rIn + (_rOut - _rIn) * 길이 비율)까지 1px 선을 긋는다(3px 이상이면 바깥 끝 한 칸은 L).
    // 방향은 정수 벡터(1:0, 1:1, 2:1, 1:2)라 큰 축으로 한 칸씩 가며 작은 축을 정수 나눗셈으로 올려 계단 길이가 늘 같다.
    // _tipOnly면 바깥 끝점만 남긴다(사라지는 프레임).
    private static string[] BuildImpactLines(Vector2Int[] _dirs, float[] _lengths, float _rIn, float _rOut, bool _tipOnly)
    {
        int c = ImpactLineCell / 2;
        char[][] grid = new char[ImpactLineCell][];
        for (int y = 0; y < ImpactLineCell; y++)
        {
            grid[y] = new char[ImpactLineCell];
            for (int x = 0; x < ImpactLineCell; x++) grid[y][x] = '.';
        }

        for (int i = 0; i < _dirs.Length; i++)
        {
            Vector2Int dir = _dirs[i];
            int ax = Mathf.Abs(dir.x);
            int ay = Mathf.Abs(dir.y);
            int major = Mathf.Max(ax, ay);
            int minor = Mathf.Min(ax, ay);
            float perStep = dir.magnitude / major; // 큰 축 한 칸당 실제 길이
            float end = Mathf.Min(_rIn + (_rOut - _rIn) * _lengths[i], c - 1);
            float start = _tipOnly ? end : Mathf.Min(_rIn * (0.6f + 0.4f * _lengths[i]), end);
            int mEnd = Mathf.FloorToInt(end / perStep);
            int mStart = _tipOnly ? mEnd : Mathf.CeilToInt(start / perStep);
            // 2:1 기울기는 계단 한 단이 2칸이므로 짝수 칸에서 시작해 첫 단도 2칸이 되게 한다.
            if (2 == major && 1 == minor && 0 != (mStart & 1) && mStart < mEnd) mStart++;

            for (int m = mStart; m <= mEnd; m++)
            {
                int side = m * minor / major;
                int x = (ax >= ay ? m : side) * (dir.x < 0 ? -1 : 1);
                int y = (ax >= ay ? side : m) * (dir.y < 0 ? -1 : 1);
                int gx = c + x;
                int gy = c - y; // 줄 0이 맨 위
                if (gx < 0 || gy < 0 || gx >= ImpactLineCell || gy >= ImpactLineCell) continue;
                grid[gy][gx] = (m == mEnd && mEnd - mStart >= 2) ? 'L' : 'W';
            }
        }

        string[] lines = new string[ImpactLineCell];
        for (int y = 0; y < ImpactLineCell; y++) lines[y] = new string(grid[y]);
        return lines;
    }

    // ───────────────────────── 계열별 구성 ─────────────────────────

    // 보석 나무(황금·다이아·무지개 단계)가 최종으로 쓰러질 때. 나무 종류와 상관없이 보석 단계별로 한 쌍. "쨍그랑, 짤랑짤랑"
    //  수관: 보석 조각이 반짝임을 달고 사방으로 튀었다 떨어지며 작아지고, 임팩트(섬광·충격선·반짝 팝 10개)가 보석 빛으로 터지고,
    //        큰 반짝이 별 세 개가 차례로 번쩍이며, 작은 보석 가루가 분수처럼 솟았다 반짝이며 쏟아진다.
    //  밑동: 보석 부스러기가 튀고, 보석 빛 실선 충격파 고리가 번지며, 기존 밑동의 큰 연기 덩이가 함께 피어오른다.
    //  색은 실행 중에 덮어쓰지 않는다(_palette 그라디언트에서 입자마다 랜덤. 무지개는 여러 색을 섞는다).
    private static StageSpec BuildGemSpec(string _key, string[] _palette, string _flashTint, string _lineColor, string _lineTint, string _sparkleColor, string _sparkleTint)
    {
        GradientColorKey[] paletteKeys = new GradientColorKey[_palette.Length];
        for (int i = 0; i < _palette.Length; i++) paletteKeys[i] = Key((float)i / (_palette.Length - 1), _palette[i]);
        Gradient palette = Grad(paletteKeys, new[] { Alpha(0.0f, 1.0f), Alpha(1.0f, 1.0f) });
        bool bRainbow = _palette.Length > 3;

        // ── 수관 ──
        // 1) 보석 조각(루트): 강하게 튀었다가 떨어지며 계단식으로 작아진다. 반짝임 칸은 하얗게 빛나다 식는다.
        SystemSpec shard = new SystemSpec
        {
            name = "TopShard",
            rows = new[]
            {
                new[] { GemL, GemA, Gem3, Gem2, DotA },
                new[] { GemA, GemA, Gem3, Gem2, DotA },
                new[] { GemB, GemB, Gem3, Gem2, DotL },
                new[] { GemC, GemC, Gem2, Gem2, DotA },
            },
            frameStarts = new[] { 0.0f, 0.45f, 0.68f, 0.84f, 0.94f },
            count = 14,
            lifetime = new Vector2(0.55f, 0.75f),
            speed = new Vector2(2.2f, 3.6f),
            shapeRadius = 0.22f,
            gravity = 0.5f,
            drag = 2.2f,
            rise = new Vector2(0.5f, 1.0f),
            randomStartGradient = palette,
            overLifetime = Grad(new[] { Key(0.0f, "FFFFFF"), Key(1.0f, "E8E8E8") }, new[] { Alpha(0.0f, 0.4f), Alpha(1.0f, 0.0f) }),
            accentColor = Color.white,
            accentGlow = new Vector2(1.4f, 3.2f),
            sortingOffset = OrderBody,
        };

        SystemSpec[] impact = BuildImpactSpecs(_flashTint, _lineColor, _lineTint, _sparkleColor, _sparkleTint, 10);
        if (bRainbow) impact[2].randomStartGradient = palette;

        // 2) 큰 반짝이 별: 수관 둘레에서 세 개가 차례로 커졌다 작아지며 번쩍인다(앞쪽 빈 프레임으로 박자를 어긋낸다).
        SystemSpec glint = new SystemSpec
        {
            name = "TopGlint",
            cellPx = 9,
            rows = new[]
            {
                new[] { Plus3, Star7, Star9, Star7, Plus3, Dot, Empty, Empty },
                new[] { Empty, Empty, Plus3, Star7, Star9, Star7, Plus3, Dot },
                new[] { Empty, Empty, Empty, Empty, Plus3, Star7, Star9, Plus3 },
            },
            count = 3,
            lifetime = new Vector2(0.55f, 0.6f),
            speed = new Vector2(0.6f, 1.2f),
            shapeRadius = 0.35f,
            drag = 5.0f,
            colorA = Color.white,
            colorB = Color.white,
            overLifetime = Grad(new[] { Key(0.0f, "FFFFFF"), Key(1.0f, _lineTint) }, new[] { Alpha(0.0f, 1.0f), Alpha(1.0f, 0.6f) }),
            sortingOffset = OrderFlash,
        };

        // 3) 보석 가루: 분수처럼 높이 솟았다가 반짝이며 쏟아진다.
        SystemSpec rain = new SystemSpec
        {
            name = "TopRain",
            rows = new[]
            {
                new[] { Diamond3, Dot, Diamond3, DotL, Diamond3, Dot },
                new[] { Dot, Diamond3, DotL, Diamond3, Dot, Diamond3 },
            },
            count = 12,
            lifetime = new Vector2(0.75f, 0.95f),
            speed = new Vector2(0.5f, 1.2f),
            shapeRadius = 0.2f,
            gravity = 0.65f,
            drag = 0.5f,
            rise = new Vector2(1.8f, 2.8f),
            randomStartGradient = palette,
            overLifetime = Grad(new[] { Key(0.0f, "FFFFFF"), Key(1.0f, "F0F0F0") }, new[] { Alpha(0.0f, 0.9f), Alpha(1.0f, 0.3f) }),
            sortingOffset = OrderGlow,
            randomStartFrame = true,
        };

        // ── 밑동 ──
        SystemSpec chip = BuildChipSpec(false, Color.white, 8);
        chip.randomStartGradient = palette;
        SystemSpec ring = BuildRingSpec(ERingStyle.Solid, 0.32f, Grad(new[] { Key(0.0f, "FFFFFF"), Key(1.0f, _lineTint) }, new[] { Alpha(0.0f, 1.0f), Alpha(1.0f, 0.1f) }));

        return new StageSpec
        {
            key = _key,
            top = Concat(new[] { shard }, Concat(impact, new[] { glint, rain })),
            bottom = new[] { chip, ring },
        };
    }

    // 1스테이지(참나무·소나무·자작나무): 기존 잎 폭발·밑동 폭발은 그대로 두고, 그 위에 수관 임팩트만 더한다.
    // 루트(섬광)는 색을 덮어쓰지 않고 재생한다(InDungeonVFXManager.PlayTreeDeadVFX).
    private static StageSpec BuildLeafSpec()
    {
        return new StageSpec
        {
            key = "Leaf",
            top = BuildImpactSpecs("E9FFB8", "FFF8D0", "C8F07A", "FFE45C", "FFB52E"),
            bottom = new SystemSpec[0],
        };
    }

    private static StageSpec BuildSporeSpec()
    {
        // ── 수관 ──
        // 1) 솜뭉치: 퐁 하고 퍼진 뒤 강한 감속으로 공중에 머물며 살짝 떠오르다 한 단계씩 작아진다. 일부는 민트빛 갓을 얹고 있다.
        SystemSpec puff = new SystemSpec
        {
            name = "TopPuff",
            rows = new[]
            {
                new[] { PuffCap6, Puff5, Puff4, Puff3, Block2 },
                new[] { Cluster7, Puff5, Puff4, Puff3, Block2 },
                new[] { PuffCap5, Puff5, Puff4, Puff3, Block2 },
                new[] { Puff6, Puff5, Puff4, Puff3, Block2 },
            },
            frameStarts = new[] { 0.0f, 0.35f, 0.55f, 0.72f, 0.86f },
            count = 12,
            lifetime = new Vector2(0.6f, 0.85f),
            speed = new Vector2(1.6f, 3.0f),
            shapeRadius = 0.18f,
            drag = 6.0f,
            rise = new Vector2(0.1f, 0.25f),
            colorA = Hex("A38EDD"),
            colorB = Hex("DDD1FF"),
            overLifetime = Grad(new[] { Key(0.0f, "FFFFFF"), Key(1.0f, "E6E6F0") }, new[] { Alpha(0.0f, 0.0f), Alpha(1.0f, 0.0f) }),
            bodyGlow = false,
            accentColor = Hex("C9FFF6"),
            accentGlow = new Vector2(1.25f, 1.25f),
            sortingOffset = OrderBody,
        };

        // 2) 포자 갓: 떨어져 나온 민트빛 덩이가 둥실 떠오르며 흔들리다 작아진다(옅게 빛난다).
        SystemSpec cap = new SystemSpec
        {
            name = "TopCap",
            rows = new[]
            {
                new[] { Cap5, Cap4, Cap3, Dot },
                new[] { Cap4, Cap4, Cap3, Dot },
            },
            frameStarts = new[] { 0.0f, 0.45f, 0.7f, 0.88f },
            count = 4,
            lifetime = new Vector2(0.9f, 1.2f),
            speed = new Vector2(0.8f, 1.4f),
            shapeRadius = 0.22f,
            drag = 3.5f,
            rise = new Vector2(0.3f, 0.45f),
            noiseStrength = 0.25f,
            noiseFrequency = 1.2f,
            noiseScroll = 0.6f,
            colorA = Hex("D2FFF7"),
            colorB = Hex("A6F1E6"),
            overLifetime = Grad(new[] { Key(0.0f, "FFFFFF"), Key(0.6f, "F2FFFC"), Key(1.0f, "B9D9D6") }, new[] { Alpha(0.0f, 0.55f), Alpha(0.6f, 0.35f), Alpha(1.0f, 0.1f) }),
            sortingOffset = OrderGlow,
        };

        // 3) 포자 티끌: `+` 와 `.` 를 오가며 반짝이고, 바람에 흔들리며 천천히 떠오른다.
        SystemSpec mote = new SystemSpec
        {
            name = "TopMote",
            rows = new[]
            {
                new[] { Plus3Soft, Dot, Plus3Soft, Dot, DotL },
                new[] { Dot, Plus3Soft, Dot, Dot, DotL },
            },
            count = 10,
            lifetime = new Vector2(0.9f, 1.4f),
            speed = new Vector2(0.3f, 0.9f),
            shapeRadius = 0.3f,
            drag = 2.0f,
            rise = new Vector2(0.3f, 0.6f),
            noiseStrength = 0.35f,
            noiseFrequency = 1.5f,
            noiseScroll = 0.8f,
            colorA = Hex("FFFFFF"),
            colorB = Hex("B5FFF0"),
            overLifetime = Grad(new[] { Key(0.0f, "FFFFFF"), Key(1.0f, "C8D8E0") }, new[] { Alpha(0.0f, 0.7f), Alpha(1.0f, 0.2f) }),
            sortingOffset = OrderBack,
            randomStartFrame = true,
        };

        // ── 밑동 ──
        // 1) 껍질 부스러기: 낮게 튀었다 떨어진다.
        SystemSpec chip = BuildChipSpec(false, Color.white, 7);

        // 2) 솜먼지: 옅은 솜뭉치가 바닥을 따라 납작하게 퍼지며 작아진다.
        SystemSpec dust = new SystemSpec
        {
            name = "BottomDust",
            rows = new[]
            {
                new[] { Puff4, Puff4, Puff3, Block2 },
                new[] { Puff5, Puff4, Puff3, Block2 },
            },
            frameStarts = new[] { 0.0f, 0.4f, 0.65f, 0.85f },
            count = 6,
            lifetime = new Vector2(0.4f, 0.6f),
            speed = new Vector2(1.2f, 2.0f),
            shapeRadius = 0.1f,
            shapeYScale = 0.35f,
            drag = 4.5f,
            rise = new Vector2(0.05f, 0.15f),
            colorA = Hex("F1EAFF"),
            colorB = Hex("DDFBF5"),
            overLifetime = Grad(new[] { Key(0.0f, "FFFFFF"), Key(1.0f, "E0E0EC") }, new[] { Alpha(0.0f, 0.0f), Alpha(1.0f, 0.0f) }),
            sortingOffset = OrderBody,
        };

        // 3) 바닥 고리: 민트빛 실선 고리가 번지다가 마지막에 점선으로 풀린다.
        SystemSpec ring = BuildRingSpec(ERingStyle.Solid, 0.32f, Grad(new[] { Key(0.0f, "E9FFFA"), Key(1.0f, "A6E6DA") }, new[] { Alpha(0.0f, 0.9f), Alpha(1.0f, 0.0f) }));

        return new StageSpec
        {
            key = "Spore",
            top = Concat(new[] { puff, cap, mote }, BuildImpactSpecs("DFFFF8", "F4EEFF", "A6E6DA", "C9FFF6", "B9A6F0")),
            bottom = new[] { chip, dust, ring },
        };
    }

    private static StageSpec BuildStarSpec()
    {
        // ── 수관 ──
        // 1) 밤하늘 조각: 별점이 박힌 수관 조각이 포물선으로 떨어지며 작아진다(별점은 반짝이다 식는다).
        SystemSpec shard = new SystemSpec
        {
            name = "TopShard",
            rows = new[]
            {
                new[] { Shard3, ShardSmall, DotL },
                new[] { Shard32, ShardSmall, DotL },
                new[] { Shard23, Block2Dark, DotM },
            },
            frameStarts = new[] { 0.0f, 0.55f, 0.82f },
            count = 13,
            lifetime = new Vector2(0.45f, 0.7f),
            speed = new Vector2(2.0f, 3.4f),
            shapeRadius = 0.2f,
            gravity = 0.35f,
            drag = 2.6f,
            colorA = Hex("2C59A2"),
            colorB = Hex("5098CF"),
            overLifetime = Grad(new[] { Key(0.0f, "FFFFFF"), Key(1.0f, "C0C8E0") }, new[] { Alpha(0.0f, 1.0f), Alpha(1.0f, 0.0f) }),
            bodyGlow = false,
            accentColor = Hex("FFFFFF"),
            accentGlow = new Vector2(0.75f, 2.0f),
            sortingOffset = OrderBody,
        };

        // 2) 별 섬광: 쓰러지는 순간 수관 한가운데서 큰 도트 별이 번쩍이고 계단식으로 줄어든다.
        SystemSpec flash = new SystemSpec
        {
            name = "TopFlash",
            cellPx = 12,
            rows = new[] { new[] { Star9, Star7, Plus5, Plus3, Dot } },
            frameStarts = new[] { 0.0f, 0.25f, 0.5f, 0.72f, 0.88f },
            count = 1,
            lifetime = new Vector2(0.2f, 0.2f),
            colorA = Hex("FFFFFF"),
            colorB = Hex("FFFFFF"),
            overLifetime = Grad(new[] { Key(0.0f, "FFFFFF"), Key(1.0f, "BFEFFF") }, new[] { Alpha(0.0f, 1.0f), Alpha(1.0f, 0.7f) }),
            sortingOffset = OrderFlash,
        };

        // 3) 풀려난 별: 수관에 박혀 있던 별(셋 중 하나는 초승달)이 퍼져 나가 머물며 반짝이다가 푸르게 식는다.
        SystemSpec freed = new SystemSpec
        {
            name = "TopFreed",
            rows = new[]
            {
                new[] { Star5, Plus3, Dot, Plus3, Star5, Plus3, Dot },
                new[] { Plus3, Star5, Plus3, Dot, Plus3, Dot, Dot },
                new[] { Crescent, Crescent, Crescent, Crescent, CrescentSmall, CrescentSmall, Dot },
            },
            count = 5,
            lifetime = new Vector2(0.8f, 1.15f),
            speed = new Vector2(1.0f, 1.8f),
            shapeRadius = 0.15f,
            gravity = 0.03f,
            drag = 3.5f,
            colorA = Hex("FFFFFF"),
            colorB = Hex("FFF0A8"),
            overLifetime = Grad(new[] { Key(0.0f, "FFFFFF"), Key(0.6f, "F0F6FF"), Key(1.0f, "8CA8FF") }, new[] { Alpha(0.0f, 1.0f), Alpha(0.6f, 0.6f), Alpha(1.0f, 0.2f) }),
            sortingOffset = OrderGlow,
        };

        // 4) 별가루: 깜빡이며 밑동 높이까지 사르르 내려앉는다.
        SystemSpec dust = new SystemSpec
        {
            name = "TopDust",
            rows = new[]
            {
                new[] { Dot, Dot, Empty, Dot, DotL, Empty },
                new[] { Dot, Empty, Dot, Dot, Empty, DotL },
            },
            count = 14,
            lifetime = new Vector2(0.9f, 1.3f),
            speed = new Vector2(0.4f, 1.0f),
            shapeRadius = 0.3f,
            gravity = 0.06f,
            drag = 2.5f,
            rise = new Vector2(-0.4f, -0.25f),
            colorA = Hex("FFFFFF"),
            colorB = Hex("9EDBFF"),
            overLifetime = Grad(new[] { Key(0.0f, "FFFFFF"), Key(1.0f, "8FA6FF") }, new[] { Alpha(0.0f, 0.75f), Alpha(1.0f, 0.15f) }),
            sortingOffset = OrderBack,
            randomStartFrame = true,
        };

        // ── 밑동 ──
        SystemSpec chip = BuildChipSpec(false, Color.white, 6);

        // 2) 바닥 별: 밑동 둘레 바닥에서 작은 별이 반짝이다 꺼진다.
        SystemSpec sparkle = new SystemSpec
        {
            name = "BottomSparkle",
            rows = new[]
            {
                new[] { Plus3, Dot, Plus3, Dot, Empty },
                new[] { Dot, Plus3, Dot, Dot, Empty },
            },
            count = 5,
            lifetime = new Vector2(0.5f, 0.8f),
            speed = new Vector2(0.6f, 1.2f),
            shapeRadius = 0.25f,
            shapeYScale = 0.45f,
            drag = 4.0f,
            colorA = Hex("FFFFFF"),
            colorB = Hex("BFEFFF"),
            overLifetime = Grad(new[] { Key(0.0f, "FFFFFF"), Key(1.0f, "9FB8FF") }, new[] { Alpha(0.0f, 0.8f), Alpha(1.0f, 0.2f) }),
            sortingOffset = OrderGlow,
            randomStartFrame = true,
        };

        // 3) 바닥 고리: 점선 별빛 고리가 번진다.
        SystemSpec ring = BuildRingSpec(ERingStyle.Dotted, 0.36f, Grad(new[] { Key(0.0f, "FFFFFF"), Key(0.5f, "CFE8FF"), Key(1.0f, "8FA6FF") }, new[] { Alpha(0.0f, 1.0f), Alpha(1.0f, 0.1f) }));

        return new StageSpec
        {
            key = "Star",
            // 별빛만 임팩트 섬광이 맨 아래다(머티리얼을 계열마다 따로 쓰던 때 실제로 그려지던 순서)
            centerDrawOrder = new[] { "ImpactFlash", "TopFlash", "ImpactLines" },
            top = Concat(new[] { shard, flash, freed, dust }, BuildImpactSpecs("D8F4FF", "FFFFFF", "8FB4FF", "FFF6C8", "CCFDFC")),
            bottom = new[] { chip, sparkle, ring },
        };
    }

    private static StageSpec BuildMagmaSpec()
    {
        // ── 수관 ──
        // 1) 결정 조각: 모서리가 달아오른 조각이 무겁게 튀어 올랐다 떨어지며, 모서리가 주황에서 검붉게 식는다.
        SystemSpec shard = new SystemSpec
        {
            name = "TopShard",
            rows = new[]
            {
                new[] { CrystalA, CrystalA2, HotChip2, DotL },
                new[] { CrystalB, CrystalB2, HotChip2, DotL },
                new[] { CrystalC, CrystalA2, Block2Dark, DotM },
            },
            frameStarts = new[] { 0.0f, 0.5f, 0.72f, 0.88f },
            count = 12,
            lifetime = new Vector2(0.5f, 0.7f),
            speed = new Vector2(2.1f, 3.4f),
            shapeRadius = 0.2f,
            gravity = 0.45f,
            drag = 2.0f,
            rise = new Vector2(0.6f, 1.2f),
            colorA = Hex("290000"),
            colorB = Hex("6C0400"),
            overLifetime = Grad(new[] { Key(0.0f, "FFFFFF"), Key(1.0f, "C8C0C0") }, new[] { Alpha(0.0f, 1.0f), Alpha(1.0f, 0.0f) }),
            bodyGlow = false,
            accentColor = Hex("FF7A2E"),
            accentGlow = new Vector2(0.55f, 2.0f),
            sortingOffset = OrderBody,
        };

        // 2) 용암 방울: 흰빛으로 튀어 포물선을 그리며 주황 -> 빨강 -> 검붉게 식는다.
        SystemSpec drop = new SystemSpec
        {
            name = "TopDrop",
            rows = new[]
            {
                new[] { Block2, Block2, Dot, Dot },
                new[] { Teardrop, Block2, Dot, Dot },
            },
            frameStarts = new[] { 0.0f, 0.4f, 0.65f, 0.85f },
            count = 8,
            lifetime = new Vector2(0.45f, 0.65f),
            speed = new Vector2(0.6f, 1.2f),
            shapeRadius = 0.15f,
            gravity = 0.6f,
            rise = new Vector2(1.6f, 2.8f),
            colorA = Hex("FFFFFF"),
            colorB = Hex("FFFFFF"),
            overLifetime = Grad(
                new[] { Key(0.0f, "FFF4B8"), Key(0.2f, "FFC046"), Key(0.5f, "F0602A"), Key(0.8f, "8E2414"), Key(1.0f, "4A1712") },
                new[] { Alpha(0.0f, 1.0f), Alpha(0.3f, 0.8f), Alpha(0.6f, 0.35f), Alpha(0.85f, 0.0f) }),
            sortingOffset = OrderGlow,
        };

        // 3) 깨짐 섬광: 들쭉날쭉한 도트 폭발이 한 번 번쩍인다.
        SystemSpec flash = new SystemSpec
        {
            name = "TopFlash",
            rows = new[] { new[] { Burst7, Diamond5, Plus3, Dot } },
            frameStarts = new[] { 0.0f, 0.3f, 0.6f, 0.82f },
            count = 1,
            lifetime = new Vector2(0.16f, 0.16f),
            colorA = Hex("FFE9A8"),
            colorB = Hex("FFE9A8"),
            overLifetime = Grad(new[] { Key(0.0f, "FFFFFF"), Key(1.0f, "FF9A5A") }, new[] { Alpha(0.0f, 1.0f), Alpha(1.0f, 0.7f) }),
            sortingOffset = OrderFlash,
        };

        // 4) 그을음: 잿빛 덩이가 흔들리며 피어오르다 작아진다(발광 없음).
        SystemSpec ash = new SystemSpec
        {
            name = "TopAsh",
            rows = new[]
            {
                new[] { Ash4, Puff3, Block2Dark, DotM },
                new[] { Puff3, Puff3, Block2Dark, DotM },
            },
            frameStarts = new[] { 0.0f, 0.45f, 0.7f, 0.88f },
            count = 5,
            lifetime = new Vector2(0.75f, 1.05f),
            speed = new Vector2(0.4f, 0.8f),
            shapeRadius = 0.18f,
            drag = 2.5f,
            rise = new Vector2(0.45f, 0.65f),
            noiseStrength = 0.2f,
            noiseFrequency = 1.0f,
            noiseScroll = 0.5f,
            colorA = Hex("4D3F49"),
            colorB = Hex("66545E"),
            overLifetime = Grad(new[] { Key(0.0f, "FFFFFF"), Key(1.0f, "C8C8C8") }, new[] { Alpha(0.0f, 0.0f), Alpha(1.0f, 0.0f) }),
            sortingOffset = OrderBack,
        };

        // 5) 불티: 1px 불티가 깜빡이며 위로 날리다 식는다.
        SystemSpec cinder = new SystemSpec
        {
            name = "TopCinder",
            rows = new[]
            {
                new[] { Dot, Dot, Empty, Dot, Dot, Empty },
                new[] { Dot, Empty, Dot, Dot, Empty, Dot },
            },
            count = 5,
            lifetime = new Vector2(0.55f, 0.85f),
            speed = new Vector2(0.3f, 0.7f),
            shapeRadius = 0.2f,
            drag = 1.5f,
            rise = new Vector2(0.7f, 1.1f),
            noiseStrength = 0.4f,
            noiseFrequency = 2.0f,
            noiseScroll = 1.0f,
            colorA = Hex("FFFFFF"),
            colorB = Hex("FFFFFF"),
            overLifetime = Grad(
                new[] { Key(0.0f, "FFD27A"), Key(0.4f, "FF7A2E"), Key(0.8f, "B8321A"), Key(1.0f, "4A1A12") },
                new[] { Alpha(0.0f, 0.85f), Alpha(0.4f, 0.6f), Alpha(0.8f, 0.2f), Alpha(1.0f, 0.0f) }),
            sortingOffset = OrderGlow,
            randomStartFrame = true,
        };

        // ── 밑동 ──
        // 1) 숯 부스러기: 한 픽셀이 달아올라 있다가 식는다.
        SystemSpec chip = BuildChipSpec(true, Hex("FF7A2E"), 7);

        // 2) 그을음: 바닥을 따라 낮게 깔린다.
        SystemSpec soot = new SystemSpec
        {
            name = "BottomSoot",
            rows = new[]
            {
                new[] { Ash4, Puff3, Block2Dark, DotM },
                new[] { Puff3, Puff3, Block2Dark, DotM },
            },
            frameStarts = new[] { 0.0f, 0.45f, 0.7f, 0.88f },
            count = 4,
            lifetime = new Vector2(0.45f, 0.65f),
            speed = new Vector2(0.8f, 1.4f),
            shapeRadius = 0.1f,
            shapeYScale = 0.4f,
            drag = 4.0f,
            rise = new Vector2(0.1f, 0.3f),
            colorA = Hex("3E3239"),
            colorB = Hex("55464E"),
            overLifetime = Grad(new[] { Key(0.0f, "FFFFFF"), Key(1.0f, "C8C8C8") }, new[] { Alpha(0.0f, 0.0f), Alpha(1.0f, 0.0f) }),
            sortingOffset = OrderBack,
        };

        // 3) 바닥 고리: 끊어진 불고리가 번지며 식는다.
        SystemSpec ring = BuildRingSpec(ERingStyle.Broken, 0.3f, Grad(
            new[] { Key(0.0f, "FFE08A"), Key(0.35f, "FF8A3A"), Key(0.75f, "C8401E"), Key(1.0f, "6A1C12") },
            new[] { Alpha(0.0f, 1.0f), Alpha(0.5f, 0.4f), Alpha(1.0f, 0.0f) }));

        return new StageSpec
        {
            key = "Magma",
            top = Concat(new[] { shard, drop, flash, ash, cinder }, BuildImpactSpecs("FFB45A", "FFE9A8", "FF6A2A", "FFD27A", "FF7A2E")),
            bottom = new[] { chip, soot, ring },
        };
    }

    // 밑동 껍질 부스러기(루트, 실행 중 나무 bottom 색). _hot이면 한 픽셀이 달아올라 있다가 식는다.
    private static SystemSpec BuildChipSpec(bool _hot, Color _accent, int _count)
    {
        return new SystemSpec
        {
            name = "BottomChip",
            rows = _hot
                ? new[]
                {
                    new[] { HotChip2, HotChip2, DotA },
                    new[] { HotChip21, HotChip21, DotA },
                    new[] { HotChip12, Chip12, DotM },
                }
                : new[]
                {
                    new[] { Chip2, Chip2, DotL },
                    new[] { Chip21, Chip21, DotL },
                    new[] { Chip12, Chip12, DotM },
                },
            frameStarts = new[] { 0.0f, 0.6f, 0.85f },
            count = _count,
            lifetime = new Vector2(0.35f, 0.55f),
            speed = new Vector2(1.6f, 2.8f),
            shapeRadius = 0.12f,
            shapeYScale = 0.4f,
            gravity = 0.4f,
            drag = 2.6f,
            rise = new Vector2(0.6f, 1.0f),
            colorA = Hex("442E47"),
            colorB = Hex("7A5380"),
            overLifetime = Grad(new[] { Key(0.0f, "FFFFFF"), Key(1.0f, "D0D0D0") }, new[] { Alpha(0.0f, _hot ? 1.0f : 0.0f), Alpha(1.0f, 0.0f) }),
            bodyGlow = false,
            accentColor = _accent,
            accentGlow = _hot ? new Vector2(0.55f, 1.8f) : Vector2.one,
            sortingOffset = OrderBody,
        };
    }

    // 바닥 충격파 고리(밑동 자리에 1개). 가로 반지름 5 -> 19px로 처음엔 빠르게, 끝으로 갈수록 느리게 번진다.
    // 앞 세 프레임은 2px 굵기로 묵직하게 터지고, 마지막 두 프레임은 성글게 풀린다.
    private static SystemSpec BuildRingSpec(ERingStyle _style, float _lifetime, Gradient _overLifetime)
    {
        int[] radii = { 5, 9, 13, 16, 18, 19 };
        string[][] frames = new string[radii.Length][];
        for (int i = 0; i < radii.Length; i++)
        {
            // 마지막 두 프레임은 한 단계 더 성글게(실선 -> 점선, 점선/끊김 -> 더 성긴 점) 풀어서 사라지게 한다.
            int sparse = (i >= radii.Length - 2) ? (i - (radii.Length - 3)) : 0;
            frames[i] = (i < 3) ? BuildThickRing(radii[i], _style) : BuildRing(radii[i], _style, sparse);
        }

        return new SystemSpec
        {
            name = "BottomRing",
            cellPx = RingCell,
            rows = new[] { frames },
            frameStarts = new[] { 0.0f, 0.1f, 0.24f, 0.42f, 0.64f, 0.84f },
            count = 1,
            lifetime = new Vector2(_lifetime, _lifetime),
            colorA = Color.white,
            colorB = Color.white,
            overLifetime = _overLifetime,
            ground = true,
        };
    }

    // _over를 _base 가운데에 겹친다(_base의 빈 칸에만 칠한다). 둘 다 가로·세로가 홀수라 정확히 가운데에 놓인다.
    private static string[] OverlayCentered(string[] _base, string[] _over)
    {
        int offsetX = (_base[0].Length - _over[0].Length) / 2;
        int offsetY = (_base.Length - _over.Length) / 2;
        string[] result = new string[_base.Length];
        for (int y = 0; y < _base.Length; y++)
        {
            char[] row = _base[y].ToCharArray();
            int oy = y - offsetY;
            if (0 <= oy && oy < _over.Length)
            {
                for (int x = 0; x < _over[oy].Length; x++)
                {
                    if ('.' != _over[oy][x] && '.' == row[offsetX + x]) row[offsetX + x] = _over[oy][x];
                }
            }

            result[y] = new string(row);
        }

        return result;
    }

    // 가로 반지름 a, 세로 반지름 a/2인 1px 타원 고리(가로 2a+1, 세로 2(a/2)+1 홀수 크기). 꽉 찬 타원의 외곽 한 줄에서 겹 픽셀을 뺀다.
    // 앞쪽(아래) 반은 W, 뒤쪽(위) 반은 M으로 칠해 바닥에 누운 고리처럼 보이게 한다(한 색 계열 2단).
    // _sparse: 0 그대로, 1 이상이면 둘레 순서에서 그만큼 더 성글게 남긴다(사라지는 프레임).
    private static string[] BuildRing(int _a, ERingStyle _style, int _sparse)
    {
        bool[,] ring = OutlineMask(FilledEllipseMask(_a, _a / 2));
        int step;
        switch (_style)
        {
            case ERingStyle.Dotted:
                // 한 칸 건너 하나, 풀릴 때는 네 칸·여섯 칸에 하나
                step = 0 == _sparse ? 2 : 2 * (_sparse + 1);
                break;
            case ERingStyle.Broken:
                ring = KeepBrokenSegments(ring);
                step = 0 == _sparse ? 1 : 2 * _sparse;
                break;
            default:
                step = 0 == _sparse ? 1 : (1 == _sparse ? 2 : 4);
                break;
        }

        return MaskToShape(KeepEvery(ring, step), 'W', 'M');
    }

    // 굵은 바닥 고리: 가로 반지름 a의 꽉 찬 타원에서 가로 반지름 a - 2 타원을 뺀다(가로 2px·세로 1px 두께로 등각 바닥에 맞는다).
    // 점선 계열은 굵게 하지 않고 1px 점선 그대로 쓴다.
    private static string[] BuildThickRing(int _a, ERingStyle _style)
    {
        int b = _a / 2;
        if (ERingStyle.Dotted == _style) return MaskToShape(KeepEvery(OutlineMask(FilledEllipseMask(_a, b)), 2), 'W', 'M');

        bool[,] ring = FilledEllipseMask(_a, b);
        int ia = _a - 2;
        int ib = ia / 2;
        bool[,] inner = FilledEllipseMask(ia, ib);
        for (int y = 0; y < inner.GetLength(0); y++)
        {
            for (int x = 0; x < inner.GetLength(1); x++)
            {
                if (inner[y, x]) ring[y + (b - ib), x + (_a - ia)] = false;
            }
        }

        if (ERingStyle.Broken == _style) ring = KeepBrokenSegments(ring);
        return MaskToShape(ring, 'W', 'M');
    }

    // 둘레를 12토막으로 나눠 세 토막마다 한 토막을 비운다(끊어진 불고리).
    private static bool[,] KeepBrokenSegments(bool[,] _ring)
    {
        int h = _ring.GetLength(0);
        int w = _ring.GetLength(1);
        float cx = (w - 1) * 0.5f;
        float cy = (h - 1) * 0.5f;
        float aspect = (float)w / h;
        bool[,] result = new bool[h, w];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (false == _ring[y, x]) continue;
                float angle = Mathf.Atan2((cy - y) * aspect, x - cx) + Mathf.PI;
                int segment = Mathf.FloorToInt(angle / (2.0f * Mathf.PI) * 12.0f) % 12;
                result[y, x] = 2 != segment % 3;
            }
        }

        return result;
    }

    // ───────────────────────── 그림 규칙 강제(모든 시스템) ─────────────────────────
    // 빌드 직전에 모든 계열·보석·기본·용암 폭발 시스템에 한 번 적용한다. 모양 문자열을 손으로 고칠 때 규칙을 어겨도 결과물은 늘 규칙을 지킨다.
    //  - 너무 작은 입자(가로·세로 모두 SmallMaxPx 이하): 칠한 픽셀을 전부 W 한 색으로(그라데이션·명암 없음).
    //  - 큰 입자: 명암 최대 3단(W/L/M). 가장 어두운 D는 M으로, 다른 계열 색인 강조 픽셀 A는 몸통 W로 바꾼다.
    //  - 수명 동안의 색: 작은 프레임이 하나라도 있으면 색·밝기 고정, 발광 배율 1(블룸 문턱 아래)로 둔다.
    //    큰 입자만 있으면 3색 이내 계단(Fixed) 그라디언트만 그대로 두고, 나머지는 한 시점(수명 25%) 값으로 고정한다.
    private const int SmallMaxPx = 3;
    private const float LifetimeSampleTime = 0.25f;

    private static bool EnforcePixelRules(StageSpec[] _stages)
    {
        for (int i = 0; i < _stages.Length; i++)
        {
            if (null == _stages[i])
            {
                Debug.LogError("[TreeDeadStageVfxBuilder] 구성 중 빠진 계열이 있습니다.");
                return false;
            }

            foreach (SystemSpec spec in _stages[i].top) EnforceSystemRules(spec);
            foreach (SystemSpec spec in _stages[i].bottom) EnforceSystemRules(spec);
        }

        return true;
    }

    private static void EnforceSystemRules(SystemSpec _spec)
    {
        bool hasSmall = false;
        string[][][] rows = new string[_spec.rows.Length][][];
        for (int r = 0; r < _spec.rows.Length; r++)
        {
            rows[r] = new string[_spec.rows[r].Length][];
            for (int f = 0; f < _spec.rows[r].Length; f++) rows[r][f] = EnforceShapeTones(_spec.rows[r][f], ref hasSmall);
        }

        _spec.rows = rows;
        _spec.overLifetime = EnforceLifetimeColor(_spec.overLifetime, hasSmall);

        // 강조 픽셀이 없어졌으므로 강조 설정을 기본값으로 둔다(속성이 같아진 머티리얼끼리 하나를 같이 쓴다).
        _spec.accentColor = Color.white;
        _spec.accentGlow = Vector2.one;
    }

    private static string[] EnforceShapeTones(string[] _shape, ref bool _hasSmall)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (int y = 0; y < _shape.Length; y++)
        {
            for (int x = 0; x < _shape[y].Length; x++)
            {
                if ('.' == _shape[y][x]) continue;
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }
        }

        if (maxX < 0) return _shape; // 빈 프레임

        bool small = maxX - minX + 1 <= SmallMaxPx && maxY - minY + 1 <= SmallMaxPx;
        if (small) _hasSmall = true;

        string[] result = new string[_shape.Length];
        for (int y = 0; y < _shape.Length; y++)
        {
            char[] row = _shape[y].ToCharArray();
            for (int x = 0; x < row.Length; x++)
            {
                if ('.' == row[x]) continue;
                if (small) row[x] = 'W';
                else if ('D' == row[x]) row[x] = 'M';
                else if ('A' == row[x]) row[x] = 'W';
            }

            result[y] = new string(row);
        }

        return result;
    }

    private static Gradient EnforceLifetimeColor(Gradient _gradient, bool _hasSmall)
    {
        if (false == _hasSmall && null != _gradient && GradientMode.Fixed == _gradient.mode && _gradient.colorKeys.Length <= 3)
        {
            return _gradient;
        }

        Color sample = null != _gradient ? _gradient.Evaluate(LifetimeSampleTime) : Color.white;
        float glow = (_hasSmall || null == _gradient) ? 0.0f : sample.a;
        return Grad(new[] { new GradientColorKey(sample, 0.0f), new GradientColorKey(sample, 1.0f) }, new[] { Alpha(0.0f, glow), Alpha(1.0f, glow) });
    }

    // ───────────────────────── 기본 한 쌍(1스테이지 나무, 계열 없는 나무) ─────────────────────────
    // 기존 잎 폭발(VFX_LeafExplosion)·밑동 폭발(VFX_TreeExplosion)과 같은 구성·수·움직임을 규칙대로 다시 그린다.
    // 기존은 입자 크기를 0.1~1 사이 아무 값으로 키우고(비정수 배율 -> 겹 픽셀·계단 깨짐) 0~360도로 돌리며(재기) 반투명으로 사라졌다.
    // 여기서는 크기 = 칸 크기(배율 1)·회전 없음·모양이 줄어들며 사라짐이다. 잎은 돌리는 대신 기존 잎 시트의 네 방향 그림을 차례로 넘겨 팔랑인다.
    //  수관: 잎 부스러기 점(70) + 팔랑이는 잎(30). 실행 중 나무 top 색으로 루트·자식 모두 덮어쓴다(나무 데이터 overrideChildrenColor).
    //  밑동: 나무 부스러기 점(120, 나무 bottom 색) + 큰 연기 덩이(3) + 흙먼지 동그라미(5) + 바닥 임팩트(1, 한 계열 3색 계단).
    private const string DustSheetPath = "Assets/Graphics/VFX/Resources/Basic/Dust.png";

    // 기존 잎 시트(Leaf.png, 8px 네 칸)의 그림을 명암 문자로 옮긴 것(흰색 W, 밝은 회청 L, 어두운 회청 M).
    private static readonly string[] LeafA = { ".....L", ".WWLLM", "WLLMM.", ".MM..." };
    private static readonly string[] LeafB = { "W..", "ML.", ".ML" };
    private static readonly string[] LeafC = { "MM.", "WLM", ".WM", "WM." };
    private static readonly string[] LeafD = { ".M.", "ML.", "MLW", ".ML" };

    private static StageSpec BuildBasicSpec()
    {
        // ── 수관 ──
        // 1) 잎 부스러기 점(루트): 상자 안에서 좌우로 흩어지며 떠올랐다가 떨어진다(기존 잎 폭발 루트와 같은 움직임).
        SystemSpec dot = new SystemSpec
        {
            name = "TopDot",
            cellPx = 4,
            rows = new[]
            {
                new[] { Block2, Dot, Dot, Dot },
                new[] { Dot, Dot, Dot, Dot },
            },
            frameStarts = new[] { 0.0f, 0.3f, 0.6f, 0.85f },
            count = 70,
            lifetime = new Vector2(0.05f, 0.7f),
            boxSize = new Vector2(0.5f, 0.7f),
            gravity = 0.4f,
            driftX = new Vector2(-2.0f, 2.0f),
            rise = new Vector2(0.0f, 2.0f),
            noiseStrength = 0.2f,
            noiseFrequency = 5.0f,
            colorA = Hex("1AA100"),
            colorB = Hex("A2E786"),
            sortingOffset = OrderBack,
        };

        // 2) 팔랑이는 잎: 네 방향 그림을 차례로 넘기며(회전 대신) 흔들리며 떠올랐다 떨어진다.
        SystemSpec leaf = new SystemSpec
        {
            name = "TopLeaf",
            cellPx = 8,
            rows = new[]
            {
                new[] { LeafA, LeafB, LeafC, LeafD, LeafA, LeafB, LeafC, LeafD },
                new[] { LeafC, LeafD, LeafA, LeafB, LeafC, LeafD, LeafA, LeafB },
            },
            count = 30,
            lifetime = new Vector2(0.3f, 0.7f),
            boxSize = new Vector2(0.5f, 0.7f),
            gravity = 0.25f,
            driftX = new Vector2(-1.0f, 1.0f),
            rise = new Vector2(1.0f, 2.0f),
            noiseStrength = 0.45f,
            noiseFrequency = 1.0f,
            colorA = Hex("1AA100"),
            colorB = Hex("A2E786"),
            sortingOffset = OrderBack,
            randomStartFrame = true,
        };

        // ── 밑동 ──
        // 1) 나무 부스러기 점(루트, 나무 bottom 색): 밑동 위쪽 반원에서 솟구쳤다가 떨어진다.
        SystemSpec chip = new SystemSpec
        {
            name = "BottomDot",
            cellPx = 4,
            rows = new[]
            {
                new[] { Block2, Block2, Dot },
                new[] { Block2, Dot, Dot },
                new[] { Dot, Dot, Dot },
            },
            frameStarts = new[] { 0.0f, 0.5f, 0.8f },
            count = 120,
            lifetime = new Vector2(0.05f, 0.8f),
            speed = new Vector2(0.1f, 2.0f),
            shapeRadius = 0.5f,
            shapeYScale = 1.0f,
            shapeArc = 180.0f,
            gravity = 1.0f,
            driftX = new Vector2(-1.0f, 1.0f),
            rise = new Vector2(3.0f, 3.0f),
            colorA = Hex("785726"),
            colorB = Hex("B07F3B"),
            sortingOffset = OrderBack,
        };

        // 2) 흙먼지 동그라미: 밑동 둘레에서 점 -> 큰 동그라미로 부풀었다가 사라진다(크기 세 갈래는 행으로 낸다).
        string[][] dust = LoadSheetShapes(DustSheetPath, 16, 5);
        SystemSpec puff = new SystemSpec
        {
            name = "BottomPuff",
            cellPx = 16,
            rows = new[]
            {
                new[] { dust[4], dust[3], dust[2], dust[1], dust[0] },
                new[] { dust[4], dust[3], dust[2], dust[1], dust[1] },
                new[] { dust[4], dust[3], dust[2], dust[2], dust[2] },
            },
            count = 5,
            lifetime = new Vector2(0.1f, 0.3f),
            shapeRadius = 0.5f,
            shapeYScale = 1.0f,
            shapeEdge = true,
            colorA = Color.white,
            colorB = Color.white,
            sortingOffset = OrderBack,
        };

        // 3) 바닥 임팩트: 기존 임팩트 시트 그림(터짐 -> 고리 -> 가는 고리 -> 끊긴 고리)을 한 색으로 옮기고,
        //    색은 주황 계열 세 단을 프레임에 맞춰 계단식으로 바꾼다(노랑빛 주황 -> 주황 -> 짙은 주황, 보간 없음).
        //    기존의 0~360도 회전 대신 상하·좌우 뒤집기 네 갈래(행) 중 하나를 고른다.
        string[][] impactFrames = LoadSheetShapes(ImpactSheetPath, 32, 5);
        Gradient impactColor = new Gradient();
        impactColor.mode = GradientMode.Fixed;
        impactColor.SetKeys(new[] { Key(0.4f, "FFD860"), Key(0.6f, "FFA238"), Key(1.0f, "D8641E") }, new[] { Alpha(0.0f, 0.0f), Alpha(1.0f, 0.0f) });
        SystemSpec impact = new SystemSpec
        {
            name = "BottomImpact",
            cellPx = 32,
            rows = new[] { impactFrames, FlipAll(impactFrames, true, false), FlipAll(impactFrames, false, true), FlipAll(impactFrames, true, true) },
            count = 1,
            lifetime = new Vector2(0.25f, 0.4f),
            colorA = Color.white,
            colorB = Color.white,
            overLifetime = impactColor,
            sortingOffset = OrderBack,
        };

        return new StageSpec
        {
            key = "Basic",
            topTag = "TreeDeadEffect_Top",
            bottomTag = "TreeDeadEffect_Bottom",
            initialPoolSize = 20,
            top = new[] { dot, leaf },
            bottom = new[] { chip, BuildSmokeSpec(), puff, impact },
        };
    }

    // 큰 연기 덩이(기존 VFX_ExplosionSmokes와 같은 모양·수·자리): 꽉 찬 둥근 덩이가 초승달 -> 가는 호로 깎이며 사라진다.
    // 한 색(흰색)·고정 불투명도 0.55(기존 시작 투명도)로 그린다. 기본·계열·보석 밑동이 같이 쓴다.
    private static SystemSpec BuildSmokeSpec()
    {
        return new SystemSpec
        {
            name = "BottomSmoke",
            cellPx = 32,
            rows = new[] { LoadSheetShapes(SmokeSheetPath, 32, 5) },
            count = 3,
            lifetime = new Vector2(0.1f, 0.75f),
            boxSize = new Vector2(1.25f, 1.0f),
            colorA = Color.white,
            colorB = Color.white,
            opacity = 0.55f,
            sortingOffset = OrderBack,
        };
    }

    // 기존 이펙트 시트(PNG, 가로로 _frames칸)를 칸마다 모양 문자열로 읽는다(불투명 픽셀은 W). 칸 전체 크기를 그대로 둔다.
    private static string[][] LoadSheetShapes(string _path, int _cell, int _frames)
    {
        if (false == File.Exists(_path)) throw new FileNotFoundException("[TreeDeadStageVfxBuilder] 시트를 찾지 못했습니다: " + _path);

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.LoadImage(File.ReadAllBytes(_path));
        Color32[] pixels = texture.GetPixels32();
        int width = texture.width;
        int height = texture.height;
        Object.DestroyImmediate(texture);

        string[][] shapes = new string[_frames][];
        for (int f = 0; f < _frames; f++)
        {
            shapes[f] = new string[_cell];
            for (int line = 0; line < _cell; line++)
            {
                int y = height - 1 - line; // 텍스처 y는 아래가 0
                char[] row = new char[_cell];
                for (int x = 0; x < _cell; x++) row[x] = 128 <= pixels[y * width + f * _cell + x].a ? 'W' : '.';
                shapes[f][line] = new string(row);
            }
        }

        return shapes;
    }

    // 모양을 좌우(_x)·상하(_y)로 뒤집는다(90도 단위 방향 바꾸기라 픽셀이 깨지지 않는다).
    private static string[][] FlipAll(string[][] _shapes, bool _x, bool _y)
    {
        string[][] result = new string[_shapes.Length][];
        for (int f = 0; f < _shapes.Length; f++)
        {
            string[] shape = _shapes[f];
            result[f] = new string[shape.Length];
            for (int line = 0; line < shape.Length; line++)
            {
                char[] row = shape[_y ? shape.Length - 1 - line : line].ToCharArray();
                if (_x) System.Array.Reverse(row);
                result[f][line] = new string(row);
            }
        }

        return result;
    }

    // ───────────────────────── 아틀라스 ─────────────────────────

    private const int AtlasWidth = 256;
    private const string AtlasName = "TreeDead_Sheet";

    // 모든 계열이 같이 쓰는 시트 하나. 시스템마다 (프레임 수 × 칸) x (행 수 × 칸) 블록을 만들되, 모양이 똑같은 블록
    // (계열마다 같은 임팩트, 보석 3종의 같은 조각 등)은 한 번만 그린다. 블록은 높이 순으로 선반(가로 줄)에 채운다.
    // 블록 안에서 0행이 맨 위다(텍스처 시트 애니메이션 규약). 영역은 (가로 비율, 세로 비율, 시작 u, 시작 v)로 셰이더 _Region에 들어간다.
    // 블록은 정수 픽셀 위치에 놓이므로 계열별 시트일 때와 같은 텍셀을 읽는다.
    private static Texture2D BuildAtlas(System.Collections.Generic.List<SystemSpec> _systems, System.Collections.Generic.List<string> _owners, out Vector4[] _regions, string _folder, string _atlasName)
    {
        int count = _systems.Count;
        int[] blockOf = new int[count];
        System.Collections.Generic.List<int> uniqueSystems = new System.Collections.Generic.List<int>(count);
        System.Collections.Generic.Dictionary<string, int> blockByShape = new System.Collections.Generic.Dictionary<string, int>(count);
        for (int i = 0; i < count; i++)
        {
            string shapeKey = ShapeKey(_systems[i]);
            int block;
            if (false == blockByShape.TryGetValue(shapeKey, out block))
            {
                block = uniqueSystems.Count;
                blockByShape.Add(shapeKey, block);
                uniqueSystems.Add(i);
            }

            blockOf[i] = block;
        }

        // 선반 채우기: 높이가 큰 블록부터 왼쪽 -> 오른쪽으로 놓고, 폭이 넘치면 아래 선반으로 내린다.
        int blockCount = uniqueSystems.Count;
        int[] blockW = new int[blockCount];
        int[] blockH = new int[blockCount];
        int[] order = new int[blockCount];
        for (int b = 0; b < blockCount; b++)
        {
            SystemSpec spec = _systems[uniqueSystems[b]];
            blockW[b] = FrameCount(spec) * spec.cellPx;
            blockH[b] = spec.rows.Length * spec.cellPx;
            order[b] = b;
            if (blockW[b] > AtlasWidth)
            {
                Debug.LogError("[TreeDeadStageVfxBuilder] 블록이 시트 폭보다 넓습니다: " + _owners[uniqueSystems[b]] + "/" + spec.name);
                _regions = null;
                return null;
            }
        }

        System.Array.Sort(order, (x, y) => blockH[y] != blockH[x] ? blockH[y].CompareTo(blockH[x]) : x.CompareTo(y));
        int[] blockX = new int[blockCount];
        int[] blockY = new int[blockCount]; // 위에서부터 잰 블록 위쪽 경계
        int shelfX = 0;
        int shelfY = 0;
        int shelfH = 0;
        for (int k = 0; k < blockCount; k++)
        {
            int b = order[k];
            if (shelfX + blockW[b] > AtlasWidth)
            {
                shelfY += shelfH;
                shelfX = 0;
                shelfH = 0;
            }

            blockX[b] = shelfX;
            blockY[b] = shelfY;
            shelfX += blockW[b];
            shelfH = Mathf.Max(shelfH, blockH[b]);
        }

        int height = shelfY + shelfH;
        Color32[] pixels = new Color32[AtlasWidth * height];
        for (int b = 0; b < blockCount; b++)
        {
            SystemSpec s = _systems[uniqueSystems[b]];
            int frames = FrameCount(s);
            int blockTop = height - blockY[b]; // 텍스처 y는 아래가 0
            for (int r = 0; r < s.rows.Length; r++)
            {
                for (int f = 0; f < frames; f++)
                {
                    // 행마다 프레임 수가 다르면 마지막 프레임을 이어 쓴다
                    string[] shape = s.rows[r][Mathf.Min(f, s.rows[r].Length - 1)];
                    if (false == PaintShape(pixels, AtlasWidth, shape, blockX[b] + f * s.cellPx, blockTop - r * s.cellPx, s.cellPx, _owners[uniqueSystems[b]] + "/" + s.name))
                    {
                        // 그리지 못한 프레임이 빈칸으로 남은 채 프리팹·풀까지 연결되지 않게 여기서 멈춘다
                        _regions = null;
                        return null;
                    }
                }
            }
        }

        _regions = new Vector4[count];
        for (int i = 0; i < count; i++)
        {
            int b = blockOf[i];
            int blockBottom = height - blockY[b] - blockH[b];
            _regions[i] = new Vector4((float)blockW[b] / AtlasWidth, (float)blockH[b] / height, (float)blockX[b] / AtlasWidth, (float)blockBottom / height);
        }

        Texture2D texture = new Texture2D(AtlasWidth, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply();

        string path = _folder + "/" + _atlasName + ".png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = false;
        importer.sRGBTexture = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.isReadable = false;
        importer.SaveAndReimport();

        Debug.Log("[TreeDeadStageVfxBuilder] 시트 " + AtlasWidth + "x" + height + ", 시스템 " + count + "개 중 고유 모양 블록 " + blockCount + "개");
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // 시스템의 모양 블록을 그대로 나타내는 키(칸 크기 + 행·프레임별 모양). 키가 같으면 시트에 그려지는 픽셀이 같다.
    private static string ShapeKey(SystemSpec _spec)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
        sb.Append(_spec.cellPx).Append('#');
        int frames = FrameCount(_spec);
        for (int r = 0; r < _spec.rows.Length; r++)
        {
            for (int f = 0; f < frames; f++)
            {
                string[] shape = _spec.rows[r][Mathf.Min(f, _spec.rows[r].Length - 1)];
                sb.Append(string.Join("/", shape)).Append('|');
            }

            sb.Append(';');
        }

        return sb.ToString();
    }

    // _cellTop: 이 칸의 위쪽 경계(텍스처 y, 제외). 모양은 칸 가운데에 놓는다. 모양이 칸보다 크면 false.
    private static bool PaintShape(Color32[] _pixels, int _width, string[] _shape, int _cellLeft, int _cellTop, int _cellPx, string _owner)
    {
        int h = _shape.Length;
        int w = 0;
        for (int i = 0; i < h; i++) w = Mathf.Max(w, _shape[i].Length);

        if (w > _cellPx || h > _cellPx)
        {
            Debug.LogError("[TreeDeadStageVfxBuilder] 모양이 칸보다 큽니다: " + _owner + " (" + w + "x" + h + " > " + _cellPx + ")");
            return false;
        }

        int offsetX = (_cellPx - w) / 2;
        int offsetY = (_cellPx - h) / 2;

        for (int line = 0; line < h; line++)
        {
            string row = _shape[line];
            int y = _cellTop - 1 - offsetY - line;
            for (int x = 0; x < row.Length; x++)
            {
                Color32 color;
                switch (row[x])
                {
                    case 'W': color = new Color32(ShadeW, ShadeW, ShadeW, 255); break;
                    case 'L': color = new Color32(ShadeL, ShadeL, ShadeL, 255); break;
                    case 'M': color = new Color32(ShadeM, ShadeM, ShadeM, 255); break;
                    case 'D': color = new Color32(ShadeD, ShadeD, ShadeD, 255); break;
                    case 'A': color = new Color32(255, 255, 0, 255); break;
                    default: continue;
                }

                _pixels[y * _width + _cellLeft + offsetX + x] = color;
            }
        }

        return true;
    }

    // ───────────────────────── 머티리얼 ─────────────────────────

    // 시스템별 머티리얼. 속성(시트 영역·몸통 발광·강조색·강조 발광)이 모두 같은 시스템끼리는 머티리얼 하나를 같이 쓴다
    // (이름은 처음 쓰는 계열·시스템 이름을 따른다). 반환 배열은 _systems와 같은 순서다.
    private static Material[] BuildMaterials(System.Collections.Generic.List<SystemSpec> _systems, System.Collections.Generic.List<string> _owners, Shader _shader, Texture2D _atlas, Vector4[] _regions, string _folder)
    {
        Material[] materials = new Material[_systems.Count];
        System.Collections.Generic.Dictionary<string, Material> byProps = new System.Collections.Generic.Dictionary<string, Material>(_systems.Count);
        for (int i = 0; i < _systems.Count; i++)
        {
            SystemSpec s = _systems[i];
            Vector4 region = _regions[i];
            string propsKey = region.x.ToString("R") + "," + region.y.ToString("R") + "," + region.z.ToString("R") + "," + region.w.ToString("R")
                + "|" + (s.bodyGlow ? 1 : 0)
                + "|" + ColorUtility.ToHtmlStringRGBA(s.accentColor)
                + "|" + s.accentGlow.x.ToString("R") + "," + s.accentGlow.y.ToString("R")
                + "|" + s.opacity.ToString("R");

            Material material;
            if (byProps.TryGetValue(propsKey, out material))
            {
                materials[i] = material;
                continue;
            }

            string path = _folder + "/M_TreeDead_" + _owners[i] + "_" + s.name + ".mat";
            material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (null == material)
            {
                material = new Material(_shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != _shader)
            {
                material.shader = _shader;
            }

            material.SetTexture("_MainTex", _atlas);
            material.SetVector("_Region", region);
            material.SetFloat("_GlowMin", 1.0f);
            material.SetFloat("_GlowMax", GlowMax);
            material.SetFloat("_BodyGlow", s.bodyGlow ? 1.0f : 0.0f);
            material.SetColor("_AccentColor", s.accentColor);
            material.SetFloat("_AccentGlowMin", s.accentGlow.x);
            material.SetFloat("_AccentGlowMax", s.accentGlow.y);
            material.SetFloat("_PixelsPerUnit", PixelsPerUnit);
            material.SetFloat("_Opacity", s.opacity);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            byProps.Add(propsKey, material);
            materials[i] = material;
        }

        return materials;
    }

    // 이 빌더 전용 폴더에서 이번 빌드가 쓰지 않는 시트·머티리얼(예전 계열별 시트, 합쳐진 머티리얼)을 지운다.
    private static void DeleteStaleAssets(Texture2D _atlas, Material[] _materials, string _textureFolder, string _materialFolder)
    {
        System.Collections.Generic.HashSet<string> keep = new System.Collections.Generic.HashSet<string>();
        keep.Add(AssetDatabase.GetAssetPath(_atlas));
        for (int i = 0; i < _materials.Length; i++) keep.Add(AssetDatabase.GetAssetPath(_materials[i]));

        string[] folders = { _textureFolder, _materialFolder };
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D t:Material", folders))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (keep.Contains(path)) continue;
            AssetDatabase.DeleteAsset(path);
        }
    }

    // ───────────────────────── 프리팹 ─────────────────────────

    private static ParticleSystem BuildPrefab(string _name, SystemSpec[] _systems, Material[] _materials, ParticleSystemRenderer _reference)
    {
        string path = PrefabFolder + "/" + _name + ".prefab";

        // 매번 새로 만든다(이전 구성의 자식/모듈 값이 남지 않게). 같은 경로에 덮어써서 GUID는 유지된다.
        GameObject root = new GameObject(_name);
        try
        {
            float maxLifetime = 0.0f;
            for (int i = 0; i < _systems.Length; i++) maxLifetime = Mathf.Max(maxLifetime, _systems[i].lifetime.y);

            for (int i = 0; i < _systems.Length; i++)
            {
                GameObject target = root;
                if (0 < i)
                {
                    target = new GameObject(_systems[i].name);
                    target.transform.SetParent(root.transform, false);
                }

                // 풀 경로에서는 루트가 멈추면 풀로 돌아가므로, 루트의 재생 길이를 자식 중 가장 긴 수명까지 늘려 둔다.
                float duration = (0 == i) ? maxLifetime : 0.05f;
                ConfigureSystem(target.AddComponent<ParticleSystem>(), _systems[i], _materials[i], _reference, duration);
            }

            if (null == PrefabUtility.SaveAsPrefabAsset(root, path))
            {
                Debug.LogError("[TreeDeadStageVfxBuilder] 프리팹 저장 실패: " + path);
                return null;
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        ParticleSystem prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<ParticleSystem>();
        if (false == PresentationLayer.VFX.SharedBurstEmitter.IsCompatible(prefab))
        {
            Debug.LogWarning("[TreeDeadStageVfxBuilder] 공유 이미터 조건에 맞지 않아 풀 경로로 재생됩니다: " + path);
        }

        return prefab;
    }

    private static void ConfigureSystem(ParticleSystem _ps, SystemSpec _spec, Material _material, ParticleSystemRenderer _reference, float _duration)
    {
        _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = _ps.main;
        main.duration = _duration;
        main.loop = false;
        main.prewarm = false;
        main.startDelay = 0.0f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(_spec.lifetime.x, _spec.lifetime.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(_spec.speed.x, _spec.speed.y);
        main.startSize3D = false;
        main.startSize = _spec.cellPx / PixelsPerUnit; // 입자 크기 = 시트 칸 크기(정수 픽셀)
        main.startRotation3D = false;
        main.startRotation = 0.0f;
        main.flipRotation = 0.0f;
        if (null != _spec.randomStartGradient)
        {
            ParticleSystem.MinMaxGradient randomColor = new ParticleSystem.MinMaxGradient(_spec.randomStartGradient);
            randomColor.mode = ParticleSystemGradientMode.RandomColor;
            main.startColor = randomColor;
        }
        else
        {
            main.startColor = new ParticleSystem.MinMaxGradient(_spec.colorA, _spec.colorB);
        }
        main.gravityModifier = _spec.gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.simulationSpeed = 1.0f;
        main.useUnscaledTime = false;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.playOnAwake = true;
        main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Transform;
        main.maxParticles = Mathf.Max(8, _spec.count * 4);
        main.stopAction = ParticleSystemStopAction.None;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.ringBufferMode = ParticleSystemRingBufferMode.Disabled;

        ParticleSystem.EmissionModule emission = _ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0.0f;
        emission.rateOverDistance = 0.0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0.0f, (short)_spec.count) });

        ParticleSystem.ShapeModule shape = _ps.shape;
        bool bBox = 0.0f < _spec.boxSize.x && 0.0f < _spec.boxSize.y;
        bool bPoint = false == bBox && _spec.shapeRadius <= 0.0f;
        shape.enabled = false == bPoint;
        if (bBox)
        {
            // 상자 안에서 낸다. 상자의 처음 속도는 화면 밖(Z) 방향이라 화면에서의 움직임은 driftX·rise·중력만 정한다(기존 잎 폭발과 같다).
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.boxThickness = Vector3.zero;
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;
            shape.scale = new Vector3(_spec.boxSize.x, _spec.boxSize.y, 0.0f);
            shape.randomDirectionAmount = 0.0f;
            shape.alignToDirection = false;
        }
        else if (false == bPoint)
        {
            shape.shapeType = ParticleSystemShapeType.Circle; // XY 평면(화면) 원
            shape.radius = _spec.shapeRadius;
            shape.radiusThickness = _spec.shapeEdge ? 0.0f : 1.0f;
            shape.arc = _spec.shapeArc;
            shape.arcMode = ParticleSystemShapeMultiModeValue.Random;
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;
            shape.scale = new Vector3(1.0f, _spec.shapeYScale, 1.0f);
            shape.randomDirectionAmount = 0.0f;
            shape.alignToDirection = false;
        }

        ParticleSystem.VelocityOverLifetimeModule velocity = _ps.velocityOverLifetime;
        bool bRise = 0.0f != _spec.rise.x || 0.0f != _spec.rise.y || 0.0f != _spec.driftX.x || 0.0f != _spec.driftX.y;
        velocity.enabled = bRise;
        if (bRise)
        {
            // 세 축의 곡선 모드가 같아야 한다
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(_spec.driftX.x, _spec.driftX.y);
            velocity.y = new ParticleSystem.MinMaxCurve(_spec.rise.x, _spec.rise.y);
            velocity.z = new ParticleSystem.MinMaxCurve(0.0f, 0.0f);
        }

        ParticleSystem.LimitVelocityOverLifetimeModule limit = _ps.limitVelocityOverLifetime;
        limit.enabled = 0.0f < _spec.drag;
        if (0.0f < _spec.drag)
        {
            limit.separateAxes = false;
            limit.limit = 100.0f; // 속도 상한은 쓰지 않고 선형 감속(drag)만 쓴다
            limit.dampen = 0.0f;
            limit.drag = _spec.drag;
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = false;
        }

        ParticleSystem.NoiseModule noise = _ps.noise;
        noise.enabled = 0.0f < _spec.noiseStrength;
        if (0.0f < _spec.noiseStrength)
        {
            noise.separateAxes = false;
            noise.strength = _spec.noiseStrength;
            noise.frequency = _spec.noiseFrequency;
            noise.scrollSpeed = _spec.noiseScroll;
            noise.damping = true;
            noise.octaveCount = 1;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.positionAmount = 1.0f;
            noise.rotationAmount = 0.0f;
            noise.sizeAmount = 0.0f;
        }

        ParticleSystem.ColorOverLifetimeModule col = _ps.colorOverLifetime;
        col.enabled = null != _spec.overLifetime;
        if (null != _spec.overLifetime) col.color = new ParticleSystem.MinMaxGradient(_spec.overLifetime);

        int frames = FrameCount(_spec);
        ParticleSystem.TextureSheetAnimationModule sheet = _ps.textureSheetAnimation;
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Grid;
        sheet.numTilesX = frames;
        sheet.numTilesY = _spec.rows.Length;
        sheet.animation = ParticleSystemAnimationType.SingleRow;
        sheet.rowMode = ParticleSystemAnimationRowMode.Random;
        sheet.timeMode = ParticleSystemAnimationTimeMode.Lifetime;
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1.0f, BuildFrameCurve(_spec.frameStarts, frames));
        // 시작 프레임은 한 바퀴에 대한 비율(0~1)이다. 깜빡임 시스템만 입자마다 한 바퀴 안에서 랜덤하게 어긋난다.
        sheet.startFrame = _spec.randomStartFrame ? new ParticleSystem.MinMaxCurve(0.0f, 0.999f) : new ParticleSystem.MinMaxCurve(0.0f);
        sheet.cycleCount = 1;
        sheet.uvChannelMask = UnityEngine.Rendering.UVChannelFlags.UV0;

        // 쓰지 않는 모듈은 꺼 둔다
        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = _ps.sizeOverLifetime;
        sizeOverLifetime.enabled = false;
        ParticleSystem.RotationOverLifetimeModule rotationOverLifetime = _ps.rotationOverLifetime;
        rotationOverLifetime.enabled = false;
        ParticleSystem.SubEmittersModule subEmitters = _ps.subEmitters;
        subEmitters.enabled = false;
        ParticleSystem.TrailModule trails = _ps.trails;
        trails.enabled = false;
        ParticleSystem.InheritVelocityModule inherit = _ps.inheritVelocity;
        inherit.enabled = false;

        ParticleSystemRenderer renderer = _ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sharedMaterial = _material;
        renderer.sortMode = ParticleSystemSortMode.None;
        renderer.minParticleSize = 0.0f;
        renderer.maxParticleSize = 10.0f;
        renderer.pivot = Vector3.zero;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        if (_spec.ground)
        {
            renderer.sortingLayerName = GroundSortingLayer;
            renderer.sortingOrder = 0;
        }
        else
        {
            renderer.sortingLayerID = _reference.sortingLayerID;
            renderer.sortingOrder = _reference.sortingOrder + _spec.sortingOffset;
        }
        renderer.sortingFudge = _spec.sortingFudge;
    }

    // 프레임 i가 frameStarts[i]에 정확히 시작하도록 꺾은선 곡선을 만든다(프레임 번호 = floor(값 × 프레임 수)).
    private static AnimationCurve BuildFrameCurve(float[] _starts, int _frames)
    {
        AnimationCurve curve = new AnimationCurve();
        for (int i = 0; i < _frames; i++)
        {
            float t = (null != _starts) ? _starts[i] : (float)i / _frames;
            curve.AddKey(new Keyframe(t, (float)i / _frames));
        }

        curve.AddKey(new Keyframe(1.0f, (_frames - 0.01f) / _frames));

        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }

        return curve;
    }

    // ───────────────────────── GameInstaller ─────────────────────────

    // _initialSizes: 태그별로 미리 만들 인스턴스 수(기본 한 쌍은 기존 사망 이펙트의 값을 이어받고, 나머지는 0).
    private static bool WireInstaller(string[] _tags, ParticleSystem[] _prefabs, int[] _initialSizes)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(InstallerPrefabPath);

        try
        {
            // 기존 나무 사망 이펙트(TreeDeadEffect_Top)가 등록된 VFXComponent에 같이 등록한다
            VFXComponent target = null;
            VFXComponent[] components = root.GetComponentsInChildren<VFXComponent>(true);
            for (int i = 0; i < components.Length && null == target; i++)
            {
                SerializedProperty list = new SerializedObject(components[i]).FindProperty("vfxPoolDataList");
                if (null == list) continue;
                for (int j = 0; j < list.arraySize; j++)
                {
                    if (ReferenceTag == list.GetArrayElementAtIndex(j).FindPropertyRelative("vfxTag").stringValue)
                    {
                        target = components[i];
                        break;
                    }
                }
            }

            if (null == target)
            {
                Debug.LogError("[TreeDeadStageVfxBuilder] GameInstaller에서 " + ReferenceTag + "가 등록된 VFXComponent를 찾지 못했습니다.");
                return false;
            }

            SerializedObject so = new SerializedObject(target);
            SerializedProperty poolList = so.FindProperty("vfxPoolDataList");
            for (int i = 0; i < _tags.Length; i++)
            {
                // 실제 재생은 공유 이미터가 맡으므로 풀은 공유 조건이 깨졌을 때의 예비 경로다. 상한은 기존 사망 이펙트와 같다.
                // 미리 만들 인스턴스는 0개: 나무 루트는 회전하지 않아 항상 공유 이미터로 재생되고, 풀 경로는 만일을 위한 대비라
                // 필요할 때만 늘어나면 된다. 상한(70)은 공유 이미터의 처음 입자 용량 계산에만 쓰인다(TryGetPoolConfig).
                SetPoolEntry(poolList, _tags[i], _prefabs[i], _initialSizes[i], 70);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, InstallerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return true;
    }

    private static void SetPoolEntry(SerializedProperty _list, string _tag, ParticleSystem _prefab, int _initial, int _max)
    {
        SerializedProperty entry = null;
        for (int i = 0; i < _list.arraySize; i++)
        {
            SerializedProperty candidate = _list.GetArrayElementAtIndex(i);
            if (_tag == candidate.FindPropertyRelative("vfxTag").stringValue)
            {
                entry = candidate;
                break;
            }
        }

        if (null == entry)
        {
            _list.InsertArrayElementAtIndex(_list.arraySize);
            entry = _list.GetArrayElementAtIndex(_list.arraySize - 1);
        }

        entry.FindPropertyRelative("vfxTag").stringValue = _tag;
        entry.FindPropertyRelative("effectPrefab").objectReferenceValue = _prefab;
        entry.FindPropertyRelative("initialPoolSize").intValue = _initial;
        entry.FindPropertyRelative("allowDynamicExpansion").boolValue = true;
        entry.FindPropertyRelative("maxPoolSize").intValue = _max;
        entry.FindPropertyRelative("uiParticleScale").floatValue = 0.0f;
    }

    // ───────────────────────── 도우미 ─────────────────────────

    private static ParticleSystemRenderer LoadReferenceRenderer()
    {
        GameObject reference = AssetDatabase.LoadAssetAtPath<GameObject>(ReferenceTopPrefabPath);
        return null != reference ? reference.GetComponent<ParticleSystemRenderer>() : null;
    }

    private static int FrameCount(SystemSpec _spec)
    {
        int frames = 0;
        for (int r = 0; r < _spec.rows.Length; r++) frames = Mathf.Max(frames, _spec.rows[r].Length);
        return frames;
    }

    private static SystemSpec[] Concat(SystemSpec[] _a, SystemSpec[] _b)
    {
        SystemSpec[] all = new SystemSpec[_a.Length + _b.Length];
        System.Array.Copy(_a, 0, all, 0, _a.Length);
        System.Array.Copy(_b, 0, all, _a.Length, _b.Length);
        return all;
    }

    private static void EnsureFolder(string _path)
    {
        if (AssetDatabase.IsValidFolder(_path)) return;
        string parent = Path.GetDirectoryName(_path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(_path));
    }

    private static Color Hex(string _hex)
    {
        Color color;
        ColorUtility.TryParseHtmlString("#" + _hex, out color);
        return color;
    }

    private static GradientColorKey Key(float _time, string _hex)
    {
        return new GradientColorKey(Hex(_hex), _time);
    }

    private static GradientAlphaKey Alpha(float _time, float _alpha)
    {
        return new GradientAlphaKey(_alpha, _time);
    }

    private static Gradient Grad(GradientColorKey[] _colors, GradientAlphaKey[] _alphas)
    {
        Gradient gradient = new Gradient();
        gradient.mode = GradientMode.Blend;
        gradient.SetKeys(_colors, _alphas);
        return gradient;
    }
}
#endif
