#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그라이크 희귀 카드 등장 연출(UI_RoguelikeRareRevealFx) 에셋 구성기. 여러 번 실행해도 같은 결과로 맞춘다(이름으로 찾아 재사용, 텍스처는 같은 값으로 덮어씀).
/// 전리품 상세 HUD 발광 빌더(LootRevealGlowBuilder)를 먼저 실행해 둬야 한다(머티리얼 3종 공유).
///  1) 텍스처(쌍선형, 알파만 쓴다) → Assets/Graphics/VFX/RareCardReveal/
///     Dot(빛 점) · DiscLinear(프리즘 덩이·백열 심) · HaloCore/Main/Deep(후광 3겹) · Rays0~5(회전 빛살 12줄을 6장에 2줄씩) ·
///     GhostWide/Core(카드 실루엣 블러 2장) · EdgeRing/ShockMain/ShockCore/MeetHalo/MeetLine(카드 둘레 빛 테두리, 9-slice)
///  2) 머티리얼은 새로 만들지 않는다: 가산 M_UILootGlowAdd, 덮기(실루엣) M_UILootGlowTint, 픽셀 입자 M_UILootParticles.
///  3) UIView_RoguelikeCardSelect.prefab: Content의 맨 앞에 RareFxBack_0~3(자식 Canvas + UI_RoguelikeRareRevealFx), 맨 뒤에 RareFxFront_0~3(자식 Canvas)을 만들고
///     Slot_i의 rareFx에 RareFxBack_i를 연결한다(프리팹 인스턴스 오버라이드). 기존 자식 순서와 슬롯 프리팹은 건드리지 않는다.
/// 모든 이미지는 raycastTarget = false, maskable = false다. 값 계산은 UI_RoguelikeRareRevealFx가 매 프레임 위치·크기·색만 쓴다.
/// </summary>
public static class RareCardRevealBuilder
{
    private const string TextureFolder = "Assets/Graphics/VFX/RareCardReveal";
    private const string MaterialFolder = "Assets/Graphics/VFX/Materials";
    private const string ViewPrefabPath = "Assets/Prefabs/Roguelike/UI/UIView_RoguelikeCardSelect.prefab";
    private const string CardBackPath = "Assets/Graphics/Roguelike/card/Card_Back.png";
    private const string BackName = "RareFxBack_";
    private const string FrontName = "RareFxFront_";
    private const float CanvasPixelsPerUnit = 32.0f;   // Canvas.prefab의 referencePixelsPerUnit. 9-slice는 이 값으로 굽는다(텍스처 1칸 = 캔버스 1px).

    private const int DotSize = 64;
    private const int HaloSize = 128;
    private const int GroupCount = 6;
    private const int RayCount = 12;
    private const int RaySuperSample = 3;

    // 시안 RAYS[deg, 폭deg, 길이배]
    private static readonly float[] RayDeg = { 0.0f, 27.0f, 58.0f, 86.0f, 112.0f, 141.0f, 172.0f, 205.0f, 232.0f, 262.0f, 291.0f, 322.0f };
    private static readonly float[] RayWidthDeg = { 9.0f, 5.0f, 11.0f, 4.0f, 8.0f, 6.0f, 10.0f, 4.0f, 9.0f, 6.0f, 11.0f, 5.0f };
    private static readonly float[] RayLengthMul = { 1.0f, 0.7f, 0.95f, 0.6f, 0.85f, 0.75f, 1.0f, 0.55f, 0.9f, 0.7f, 0.95f, 0.65f };

    private struct RingSpec
    {
        public string name;
        public float offset;       // 선 중심 ~ 이미지 가장자리(px)
        public int border;         // 9-slice 테두리(px)
        public float cornerRadius;
        public float lineWidth;
        public float sigma;
        public bool bLine;
        public bool bGlow;
    }

    [MenuItem("LumberBoy/VFX/Build Rare Card Reveal")]
    public static void Build()
    {
        Material add = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/M_UILootGlowAdd.mat");
        Material tint = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/M_UILootGlowTint.mat");
        Material pixel = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/M_UILootParticles.mat");
        if (null == add || null == tint || null == pixel)
        {
            Debug.LogError("[RareCardRevealBuilder] M_UILootGlowAdd / M_UILootGlowTint / M_UILootParticles가 없습니다. LumberBoy/VFX/Build Loot Reveal Glow를 먼저 실행하세요.");
            return;
        }

        LootPillarGlowBuilder.EnsureFolder(TextureFolder);

        Sprite dot = BuildProfileSprite("T_RareCard_Dot", DotSize, ProfileDot);
        Sprite linear = BuildProfileSprite("T_RareCard_DiscLinear", DotSize, ProfileLinear);
        Sprite haloCore = BuildProfileSprite("T_RareCard_HaloCore", HaloSize, ProfileHaloCore);
        Sprite haloMain = BuildProfileSprite("T_RareCard_HaloMain", HaloSize, ProfileHaloMain);
        Sprite haloDeep = BuildProfileSprite("T_RareCard_HaloDeep", HaloSize, ProfileHaloDeep);

        Sprite[] rays = new Sprite[GroupCount];
        for (int g = 0; g < GroupCount; g++) rays[g] = BuildRays("T_RareCard_Rays" + g, g);

        float[] cardAlpha = ReadCardAlpha();
        if (null == cardAlpha) return;
        Sprite ghostWide = BuildGhost("T_RareCard_GhostWide", cardAlpha, Mathf.RoundToInt(UI_RoguelikeRareRevealFx.GhostWideWidth), Mathf.RoundToInt(UI_RoguelikeRareRevealFx.GhostWideHeight), 2.4f);
        Sprite ghostCore = BuildGhost("T_RareCard_GhostCore", cardAlpha, Mathf.RoundToInt(UI_RoguelikeRareRevealFx.GhostCoreWidth), Mathf.RoundToInt(UI_RoguelikeRareRevealFx.GhostCoreHeight), 0.5f);

        // 카드 둘레 빛 테두리: 시안의 strokeRect + shadowBlur(= 가우시안 시그마 blur/2)를 선 굵기·시그마로 옮겼다.
        Sprite edgeRing = BuildRing(new RingSpec { name = "T_RareCard_EdgeRing", offset = UI_RoguelikeRareRevealFx.EdgeCentreOffset, border = 18, cornerRadius = 0.0f, lineWidth = 1.0f, sigma = 3.5f, bLine = true, bGlow = true });
        Sprite shockMain = BuildRing(new RingSpec { name = "T_RareCard_ShockMain", offset = UI_RoguelikeRareRevealFx.ShockCentreOffset, border = 35, cornerRadius = 8.0f, lineWidth = 3.0f, sigma = 7.0f, bLine = true, bGlow = true });
        Sprite shockCore = BuildRing(new RingSpec { name = "T_RareCard_ShockCore", offset = UI_RoguelikeRareRevealFx.ShockCentreOffset, border = 35, cornerRadius = 8.0f, lineWidth = 1.2f, sigma = 2.0f, bLine = true, bGlow = true });
        Sprite meetHalo = BuildRing(new RingSpec { name = "T_RareCard_MeetHalo", offset = UI_RoguelikeRareRevealFx.MeetCentreOffset, border = 39, cornerRadius = 0.0f, lineWidth = 2.0f, sigma = 8.0f, bLine = false, bGlow = true });
        Sprite meetLine = BuildRing(new RingSpec { name = "T_RareCard_MeetLine", offset = UI_RoguelikeRareRevealFx.MeetCentreOffset, border = 39, cornerRadius = 0.0f, lineWidth = 2.0f, sigma = 2.0f, bLine = true, bGlow = false });

        AssetDatabase.SaveAssets();
        Texture dotTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/T_RareCard_Dot.png");

        if (null == dot || null == linear || null == haloCore || null == haloMain || null == haloDeep || null == ghostWide || null == ghostCore
            || null == edgeRing || null == shockMain || null == shockCore || null == meetHalo || null == meetLine || null == dotTexture)
        {
            Debug.LogError("[RareCardRevealBuilder] 텍스처 생성에 실패했습니다.");
            return;
        }

        WireView(dot, dotTexture, linear, haloCore, haloMain, haloDeep, rays, ghostWide, ghostCore, edgeRing, shockMain, shockCore, meetHalo, meetLine, add, tint, pixel);
        AssetDatabase.SaveAssets();
        Debug.Log("[RareCardRevealBuilder] 완료: " + ViewPrefabPath);
    }

    // ---------- 텍스처: 방사형 프로필 ----------

    private delegate float RadialProfile(float _r);

    // 시안 glowDot: 0 → 1, 0.35 → 0.55, 1 → 0 (선형 보간)
    private static float ProfileDot(float _r)
    {
        if (1.0f <= _r) return 0.0f;
        return 0.35f > _r ? Mathf.Lerp(1.0f, 0.55f, _r / 0.35f) : Mathf.Lerp(0.55f, 0.0f, (_r - 0.35f) / 0.65f);
    }

    // 프리즘 덩이 · 백열 심: 중심 1 → 가장자리 0
    private static float ProfileLinear(float _r)
    {
        return 1.0f <= _r ? 0.0f : 1.0f - _r;
    }

    // 후광 3겹. 시안 후광 그라데이션(0: core 0.85, 0.3: 주색 0.6, 0.62: deep 0.25, 1: 0)은 정지점마다 색과 알파가 함께 보간되므로
    // 색별 가중치 = (색 보간 비율) x (알파 보간)이다. 각 이미지의 세기는 정지점 알파(0.85h / 0.6h / 0.25h)이고 텍스처는 그 비율로 나눈 모양이다.
    private static float ProfileHaloCore(float _r)
    {
        if (0.3f <= _r) return 0.0f;
        float t = _r / 0.3f;
        return (1.0f - t) * (1.0f + (0.6f / 0.85f - 1.0f) * t);
    }

    private static float ProfileHaloMain(float _r)
    {
        if (0.3f > _r)
        {
            float t = _r / 0.3f;
            return t * ((0.85f / 0.6f) * (1.0f - t) + t);
        }

        if (0.62f > _r)
        {
            float s = (_r - 0.3f) / 0.32f;
            return (1.0f - s) * ((1.0f - s) + (0.25f / 0.6f) * s);
        }

        return 0.0f;
    }

    private static float ProfileHaloDeep(float _r)
    {
        if (0.3f > _r) return 0.0f;
        if (0.62f > _r)
        {
            float s = (_r - 0.3f) / 0.32f;
            return s * ((0.6f / 0.25f) * (1.0f - s) + s);
        }

        return 1.0f > _r ? 1.0f - (_r - 0.62f) / 0.38f : 0.0f;
    }

    private static Sprite BuildProfileSprite(string _name, int _size, RadialProfile _profile)
    {
        Color32[] pixels = new Color32[_size * _size];
        float half = _size * 0.5f;
        for (int y = 0; y < _size; y++)
        {
            for (int x = 0; x < _size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float a = Mathf.Clamp01(_profile(Mathf.Sqrt(dx * dx + dy * dy)));
                pixels[y * _size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255.0f));
            }
        }

        return LootPillarGlowBuilder.WriteSprite(TextureFolder, _name, _size, _size, pixels, new Vector2(0.5f, 0.5f), _size);
    }

    // ---------- 텍스처: 회전 빛살 ----------

    // 시안 popRays: 부채꼴(반폭 = 폭deg/2)을 반지름 (78 + 40 x 길이배)까지, 방사형 그라데이션 중심 6px부터 0.75 → 0.45 지점 0.32 → 끝 0으로 채운다.
    // 12줄을 줄 번호 % 6으로 6장에 2줄씩 굽는다(색은 이미지 색으로 정하므로 흰색). 텍스처 1칸 = 1px, 가운데 = 빛살 중심.
    private static Sprite BuildRays(string _name, int _group)
    {
        int size = Mathf.RoundToInt(UI_RoguelikeRareRevealFx.RayMaxLength) * 2;
        float half = size * 0.5f;
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float acc = 0.0f;
                for (int sy = 0; sy < RaySuperSample; sy++)
                {
                    for (int sx = 0; sx < RaySuperSample; sx++)
                    {
                        float dx = x + (sx + 0.5f) / RaySuperSample - half;
                        float dy = y + (sy + 0.5f) / RaySuperSample - half;
                        acc += RayValue(_group, dx, dy);
                    }
                }

                float a = Mathf.Clamp01(acc / (RaySuperSample * RaySuperSample));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255.0f));
            }
        }

        return LootPillarGlowBuilder.WriteSprite(TextureFolder, _name, size, size, pixels, new Vector2(0.5f, 0.5f), size);
    }

    private static float RayValue(int _group, float _dx, float _dyUp)
    {
        float r = Mathf.Sqrt(_dx * _dx + _dyUp * _dyUp);
        if (0.0001f > r) return 0.75f;

        // 시안은 y가 아래로 증가하고 각도가 화면에서 시계 방향이다. 텍스처는 y가 위로 증가하므로 부호를 뒤집는다.
        float angle = Mathf.Atan2(-_dyUp, _dx) * Mathf.Rad2Deg;
        float result = 0.0f;
        for (int j = _group; j < RayCount; j += GroupCount)
        {
            float length = 78.0f + 40.0f * RayLengthMul[j];
            if (r > length) continue;

            float delta = Mathf.Abs(Mathf.DeltaAngle(angle, RayDeg[j]));
            if (delta > RayWidthDeg[j] * 0.5f) continue;

            float g;
            if (6.0f >= r) g = 0.75f;
            else if (0.45f * length >= r) g = Mathf.Lerp(0.75f, 0.32f, (r - 6.0f) / (0.45f * length - 6.0f));
            else g = Mathf.Lerp(0.32f, 0.0f, (r - 0.45f * length) / (0.55f * length));
            result = Mathf.Max(result, g);
        }

        return result;
    }

    // ---------- 텍스처: 카드 실루엣 블러 ----------

    private static float[] ReadCardAlpha()
    {
        string path = Path.GetFullPath(CardBackPath);
        if (false == File.Exists(path))
        {
            Debug.LogError("[RareCardRevealBuilder] 카드 뒷면 그림을 찾지 못했습니다: " + CardBackPath);
            return null;
        }

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        texture.LoadImage(File.ReadAllBytes(path));
        int width = texture.width;
        int height = texture.height;
        Color32[] source = texture.GetPixels32();
        Object.DestroyImmediate(texture);

        // 앞 두 칸 = 크기(width, height), 이어서 알파(0~1, 아래 행부터)
        float[] result = new float[2 + width * height];
        result[0] = width;
        result[1] = height;
        for (int i = 0; i < source.Length; i++) result[2 + i] = source[i].a / 255.0f;
        return result;
    }

    // 카드 실루엣(뒷면 알파)을 _width x _height 가운데에 놓고 시그마 _sigma로 가우시안 블러한다.
    private static Sprite BuildGhost(string _name, float[] _cardAlpha, int _width, int _height, float _sigma)
    {
        int cardWidth = Mathf.RoundToInt(_cardAlpha[0]);
        int cardHeight = Mathf.RoundToInt(_cardAlpha[1]);
        float[] field = new float[_width * _height];
        int offsetX = (_width - cardWidth) / 2;
        int offsetY = (_height - cardHeight) / 2;
        for (int y = 0; y < cardHeight; y++)
        {
            for (int x = 0; x < cardWidth; x++) field[(y + offsetY) * _width + x + offsetX] = _cardAlpha[2 + y * cardWidth + x];
        }

        // 실루엣은 안쪽 구멍 없이 꽉 찬 모양이다(시안의 tintMask: 카드 알파 그대로)
        float[] blurred = GaussianBlur(field, _width, _height, _sigma);

        Color32[] pixels = new Color32[_width * _height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(blurred[i]) * 255.0f));
        return LootPillarGlowBuilder.WriteSprite(TextureFolder, _name, _width, _height, pixels, new Vector2(0.5f, 0.5f), _height);
    }

    private static float[] GaussianBlur(float[] _src, int _width, int _height, float _sigma)
    {
        int radius = Mathf.CeilToInt(_sigma * 3.0f);
        float[] kernel = new float[radius * 2 + 1];
        float sum = 0.0f;
        for (int i = -radius; i <= radius; i++)
        {
            kernel[i + radius] = Mathf.Exp(-(i * i) / (2.0f * _sigma * _sigma));
            sum += kernel[i + radius];
        }

        for (int i = 0; i < kernel.Length; i++) kernel[i] /= sum;

        float[] temp = new float[_src.Length];
        float[] dst = new float[_src.Length];
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                float acc = 0.0f;
                for (int k = -radius; k <= radius; k++)
                {
                    int xx = x + k;
                    if (0 > xx || _width <= xx) continue;
                    acc += _src[y * _width + xx] * kernel[k + radius];
                }

                temp[y * _width + x] = acc;
            }
        }

        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                float acc = 0.0f;
                for (int k = -radius; k <= radius; k++)
                {
                    int yy = y + k;
                    if (0 > yy || _height <= yy) continue;
                    acc += temp[yy * _width + x] * kernel[k + radius];
                }

                dst[y * _width + x] = acc;
            }
        }

        return dst;
    }

    // ---------- 텍스처: 둥근 사각 빛 테두리(9-slice) ----------

    // 이미지 크기 = 2 x 테두리 + 2. 선(사각 경로)은 가장자리에서 offset 안쪽에 놓고, 선까지의 거리 d로 굵기·번짐을 정한다.
    //   값 = (선 있음 ? 선 굵기 덮임 : 0) + (번짐 있음 ? 선 굵기 / (시그마 x 루트 2π) x exp(-d² / 2 시그마²) : 0)  (시안의 선 + shadowBlur 그림자)
    // 경로 안쪽은 테두리(9-slice 가장자리 칸) 끝에서 0이 되도록 선형으로 줄인다(가운데 늘어나는 칸은 투명).
    private static Sprite BuildRing(RingSpec _spec)
    {
        int size = _spec.border * 2 + 2;
        Color32[] pixels = new Color32[size * size];
        float centre = size * 0.5f;
        float halfExtent = centre - _spec.offset;
        float innerRange = _spec.border - _spec.offset;
        float gaussScale = _spec.lineWidth / (_spec.sigma * Mathf.Sqrt(2.0f * Mathf.PI));

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f - centre;
                float py = y + 0.5f - centre;
                float signed = SignedDistanceRoundedBox(px, py, halfExtent, halfExtent, _spec.cornerRadius);

                float value = 0.0f;
                if (true == _spec.bLine) value += Mathf.Clamp01(_spec.lineWidth * 0.5f - Mathf.Abs(signed) + 0.5f);
                if (true == _spec.bGlow) value += gaussScale * Mathf.Exp(-signed * signed / (2.0f * _spec.sigma * _spec.sigma));

                if (0.0f > signed) value *= Mathf.Clamp01(1.0f - (-signed) / innerRange);

                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255.0f));
            }
        }

        return WriteSlicedSprite(_spec.name, size, pixels, _spec.border);
    }

    private static float SignedDistanceRoundedBox(float _x, float _y, float _halfX, float _halfY, float _radius)
    {
        float qx = Mathf.Abs(_x) - (_halfX - _radius);
        float qy = Mathf.Abs(_y) - (_halfY - _radius);
        float outside = Mathf.Sqrt(Mathf.Max(qx, 0.0f) * Mathf.Max(qx, 0.0f) + Mathf.Max(qy, 0.0f) * Mathf.Max(qy, 0.0f));
        return outside + Mathf.Min(Mathf.Max(qx, qy), 0.0f) - _radius;
    }

    private static Sprite WriteSlicedSprite(string _name, int _size, Color32[] _pixels, int _border)
    {
        string path = TextureFolder + "/" + _name + ".png";
        LootPillarGlowBuilder.WritePng(path, _size, _size, _pixels);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (null == importer) return null;

        LootPillarGlowBuilder.ApplyCommonImport(importer, CanvasPixelsPerUnit);
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = new Vector4(_border, _border, _border, _border);

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ---------- 뷰 프리팹 연결 ----------

    private static void WireView(Sprite _dot, Texture _dotTexture, Sprite _linear, Sprite _haloCore, Sprite _haloMain, Sprite _haloDeep, Sprite[] _rays,
        Sprite _ghostWide, Sprite _ghostCore, Sprite _edgeRing, Sprite _shockMain, Sprite _shockCore, Sprite _meetHalo, Sprite _meetLine,
        Material _add, Material _tint, Material _pixel)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ViewPrefabPath);

        try
        {
            Transform content = root.transform.Find("Content");
            if (null == content)
            {
                Debug.LogError("[RareCardRevealBuilder] Content를 찾지 못했습니다.");
                return;
            }

            UI_RoguelikeCardSlot[] slots = new UI_RoguelikeCardSlot[4];
            for (int i = 0; i < slots.Length; i++)
            {
                Transform slotTransform = content.Find("Slot_" + i);
                slots[i] = null != slotTransform ? slotTransform.GetComponent<UI_RoguelikeCardSlot>() : null;
                if (null == slots[i])
                {
                    Debug.LogError("[RareCardRevealBuilder] Slot_" + i + "를 찾지 못했습니다.");
                    return;
                }
            }

            // 뒤 층: Content의 맨 앞 자식들(모든 카드보다 먼저 그림). 앞 층: 맨 뒤 자식들(모든 카드 위).
            RectTransform[] backs = new RectTransform[slots.Length];
            RectTransform[] fronts = new RectTransform[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                backs[i] = GetOrCreateLayer(content, BackName + i);
                backs[i].SetSiblingIndex(i);
            }

            for (int i = 0; i < slots.Length; i++)
            {
                fronts[i] = GetOrCreateLayer(content, FrontName + i);
                fronts[i].SetAsLastSibling();
            }

            for (int i = 0; i < slots.Length; i++) BuildSlotFx(slots[i], backs[i], fronts[i], _dot, _dotTexture, _linear, _haloCore, _haloMain, _haloDeep, _rays, _ghostWide, _ghostCore, _edgeRing, _shockMain, _shockCore, _meetHalo, _meetLine, _add, _tint, _pixel);

            PrefabUtility.SaveAsPrefabAsset(root, ViewPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void BuildSlotFx(UI_RoguelikeCardSlot _slot, RectTransform _back, RectTransform _front, Sprite _dot, Texture _dotTexture, Sprite _linear,
        Sprite _haloCore, Sprite _haloMain, Sprite _haloDeep, Sprite[] _rays, Sprite _ghostWide, Sprite _ghostCore, Sprite _edgeRing, Sprite _shockMain, Sprite _shockCore,
        Sprite _meetHalo, Sprite _meetLine, Material _add, Material _tint, Material _pixel)
    {
        // 뒤 층(자식 순서 = 그리는 순서)
        Image haloDeep = GetOrCreateImage(_back, "HaloDeep", _haloDeep, _add, new Vector2(HaloSize, HaloSize), false);
        Image haloMain = GetOrCreateImage(_back, "HaloMain", _haloMain, _add, new Vector2(HaloSize, HaloSize), false);
        Image haloCore = GetOrCreateImage(_back, "HaloCore", _haloCore, _add, new Vector2(HaloSize, HaloSize), false);
        Image[] prismBlobs = new Image[GroupCount];
        for (int k = 0; k < GroupCount; k++) prismBlobs[k] = GetOrCreateImage(_back, "PrismBlob_" + k, _linear, _add, new Vector2(DotSize, DotSize), false);
        Image whiteCore = GetOrCreateImage(_back, "WhiteCore", _linear, _add, new Vector2(DotSize, DotSize), false);

        RectTransform raysRoot = GetOrCreateRect(_back, "Rays");
        Vector2 raySize = new Vector2(_rays[0].rect.width, _rays[0].rect.height);
        Image[] rayImages = new Image[GroupCount];
        for (int g = 0; g < GroupCount; g++) rayImages[g] = GetOrCreateImage(raysRoot, "Ray_" + g, _rays[g], _add, raySize, false);

        // 앞 층
        Image edgeGlow = GetOrCreateImage(_front, "EdgeGlow", _edgeRing, _add, new Vector2(64.0f, 64.0f), true);
        Image[] ghostWide = new Image[2];
        Image[] ghostCore = new Image[2];
        for (int i = 0; i < 2; i++) ghostWide[i] = GetOrCreateImage(_front, "GhostWide_" + i, _ghostWide, _add, _ghostWide.rect.size, false);
        for (int i = 0; i < 2; i++) ghostCore[i] = GetOrCreateImage(_front, "GhostCore_" + i, _ghostCore, _add, _ghostCore.rect.size, false);
        Image shockMain = GetOrCreateImage(_front, "ShockMain", _shockMain, _add, new Vector2(96.0f, 96.0f), true);
        Image shockCore = GetOrCreateImage(_front, "ShockCore", _shockCore, _add, new Vector2(96.0f, 96.0f), true);
        Image meetHalo = GetOrCreateImage(_front, "MeetHalo", _meetHalo, _add, new Vector2(96.0f, 96.0f), true);
        Image meetLine = GetOrCreateImage(_front, "MeetLine", _meetLine, _add, new Vector2(96.0f, 96.0f), true);
        UI_RoguelikeRareRevealParticles soft = GetOrCreateParticles(_front, "SoftGlow", _add, _dotTexture);
        UI_RoguelikeRareRevealParticles pixels = GetOrCreateParticles(_front, "Pixels", _pixel, null);

        UI_RoguelikeRareRevealFx fx = _back.GetComponent<UI_RoguelikeRareRevealFx>();
        if (null == fx) fx = _back.gameObject.AddComponent<UI_RoguelikeRareRevealFx>();

        SerializedObject so = new SerializedObject(fx);
        so.FindProperty("backRoot").objectReferenceValue = _back;
        so.FindProperty("haloCoreImage").objectReferenceValue = haloCore;
        so.FindProperty("haloMainImage").objectReferenceValue = haloMain;
        so.FindProperty("haloDeepImage").objectReferenceValue = haloDeep;
        SetArray(so.FindProperty("prismBlobImages"), prismBlobs);
        so.FindProperty("whiteCoreImage").objectReferenceValue = whiteCore;
        so.FindProperty("raysRoot").objectReferenceValue = raysRoot;
        SetArray(so.FindProperty("rayImages"), rayImages);
        so.FindProperty("frontRoot").objectReferenceValue = _front;
        so.FindProperty("edgeGlowImage").objectReferenceValue = edgeGlow;
        SetArray(so.FindProperty("ghostWideImages"), ghostWide);
        SetArray(so.FindProperty("ghostCoreImages"), ghostCore);
        so.FindProperty("shockMainImage").objectReferenceValue = shockMain;
        so.FindProperty("shockCoreImage").objectReferenceValue = shockCore;
        so.FindProperty("meetHaloImage").objectReferenceValue = meetHalo;
        so.FindProperty("meetLineImage").objectReferenceValue = meetLine;
        so.FindProperty("softGraphic").objectReferenceValue = soft;
        so.FindProperty("pixelGraphic").objectReferenceValue = pixels;
        so.FindProperty("silhouetteMaterial").objectReferenceValue = _tint;
        so.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject slotSo = new SerializedObject(_slot);
        slotSo.FindProperty("rareFx").objectReferenceValue = fx;
        slotSo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetArray(SerializedProperty _property, Image[] _images)
    {
        _property.arraySize = _images.Length;
        for (int i = 0; i < _images.Length; i++) _property.GetArrayElementAtIndex(i).objectReferenceValue = _images[i];
    }

    // 중앙 앵커 · 중앙 피벗 · 크기 0의 자식 Canvas 층(자식 위치 = 층 루트 기준 px, 루트 위치는 UI_RoguelikeRareRevealFx가 정한다)
    private static RectTransform GetOrCreateLayer(Transform _parent, string _name)
    {
        RectTransform rt = GetOrCreateRect(_parent, _name);
        if (null == rt.GetComponent<Canvas>())
        {
            Canvas canvas = rt.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = false;
        }

        return rt;
    }

    private static RectTransform GetOrCreateRect(Transform _parent, string _name)
    {
        Transform existing = _parent.Find(_name);
        RectTransform rt;
        if (null != existing)
        {
            rt = (RectTransform)existing;
        }
        else
        {
            GameObject go = new GameObject(_name, typeof(RectTransform));
            rt = go.GetComponent<RectTransform>();
            rt.SetParent(_parent, false);
        }

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    private static Image GetOrCreateImage(Transform _parent, string _name, Sprite _sprite, Material _material, Vector2 _size, bool _bSliced)
    {
        Transform existing = _parent.Find(_name);
        GameObject go;
        if (null != existing)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject(_name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.GetComponent<RectTransform>().SetParent(_parent, false);
        }

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = _size;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;

        Image image = go.GetComponent<Image>();
        image.sprite = _sprite;
        image.material = _material;
        image.type = true == _bSliced ? Image.Type.Sliced : Image.Type.Simple;
        image.fillCenter = true;
        image.color = Color.white;
        image.raycastTarget = false;
        image.maskable = false;
        image.enabled = false;
        return image;
    }

    private static UI_RoguelikeRareRevealParticles GetOrCreateParticles(Transform _parent, string _name, Material _material, Texture _softTexture)
    {
        Transform existing = _parent.Find(_name);
        GameObject go;
        if (null != existing)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject(_name, typeof(RectTransform));
            go.GetComponent<RectTransform>().SetParent(_parent, false);
            go.AddComponent<UI_RoguelikeRareRevealParticles>();
        }

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        UI_RoguelikeRareRevealParticles graphic = go.GetComponent<UI_RoguelikeRareRevealParticles>();
        graphic.material = _material;
        graphic.raycastTarget = false;
        graphic.maskable = false;

        SerializedObject so = new SerializedObject(graphic);
        so.FindProperty("softTexture").objectReferenceValue = _softTexture;
        so.ApplyModifiedPropertiesWithoutUndo();
        return graphic;
    }
}
#endif
