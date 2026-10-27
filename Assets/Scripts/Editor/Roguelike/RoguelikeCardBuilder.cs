#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그라이크 카드 UI 프리팹 구성기. 여러 번 실행해도 같은 결과로 덮어쓴다.
///
/// 계층(모든 자식은 앵커·피벗 왼쪽 아래, 위치는 프레임 왼쪽 아래 기준 정수 픽셀, 크기는 스프라이트 원본 픽셀):
///   UI_RoguelikeCard (50x81, 프레임 크기)
///    ├ HitBox   50x87  투명, 마우스 판정. 호버로 올라간 그림 범위(+6px)까지 덮고 움직이지 않는다
///    ├ Shadow   40x5   카드 밑 납작한 그림자. 카드가 올라가면 연해지고 좁아지며 아래로 내려간다(제자리 - Visual 밖)
///    ├ Visual   50x81  호버 때 위로 올라가는 부분. 아래 그림은 모두 raycastTarget 끔
///       └ FlipRoot 50x81  뒤집힘 연출 때 폭이 줄었다 느는 부분(피벗 가운데 아래)
///          ├ Frame   CardFrame_0   (0, 0)
///          ├ GradeFrame 50x81  프레임을 등급 색으로 바꿔 그리는 층(Card_FrameFull = 프레임 복사본, 셰이더 UI/CardGradeFrame 팔레트 스왑). 희귀 카드에서만 켜지고 이때 Frame은 꺼진다
///          ├ Profile CardProfile_0 (8, 38)  - 같은 64x96 캔버스에서 잘린 스프라이트라 캔버스 좌표 (15,47)-(7,9)
///          ├ Icon    스킬 아이콘 (스크립트가 프로필 칸 중앙에 배치)
///          ├ Level   I~VI, MAX (스크립트가 하단 중앙에 배치)
///          └ Flash   선택·앞면 공개 순간 프레임 모양으로 하얗게 번쩍이는 층(평소에는 꺼져 있다)
///       ├ EdgeLine  4x77  카드가 옆으로 선 순간 보이는 카드 두께(앞면 테두리 색. FlipRoot 밖이라 폭이 변하지 않는다. 평소에는 꺼져 있다)
///       └ Aura     50x81  희귀 카드의 별가루·불씨·글린트·공개 스파크(UI_RoguelikeCardAura, 불투명 . + x 픽셀). 일반 카드는 꺼짐
///    └ Fx       50x81  UI_RoguelikePixelFx. 맨 위에 그려지는 입자 풀(Particle_0~53, 입자 하나가 연속한 이미지 3장 = 머리 + 잔상 둘을 쓴다). 평소에는 모두 꺼져 있다
///
/// 프레임과 프로필은 같은 캔버스(64x96)에서 잘려 나온 스프라이트라 서로의 위치가 잘린 사각형 좌표 차이로 정해진다.
/// </summary>
public static class RoguelikeCardBuilder
{
    private const string CardDir = "Assets/Graphics/Roguelike/card/";
    private const string ShadowPath = CardDir + "Card_Shadow.png";
    private const string BackPath = CardDir + "Card_Back.png";
    private const string FrameFullPath = CardDir + "Card_FrameFull.png";
    private const string MaterialDir = "Assets/Materials/UI/Roguelike";
    private const string FrameShaderPath = "Assets/Shaders/UI/UI_CardGradeFrame.shader";
    private const string EdgeStripPath = CardDir + "Fx_EdgeStrip.png";
    private const string Dot1Path = CardDir + "Fx_Dot1.png";
    private const string Dot2Path = CardDir + "Fx_Dot2.png";
    private const string Bar2Path = CardDir + "Fx_Bar2.png";
    private const string PlusSmallPath = CardDir + "Fx_PlusS.png";
    private const string PlusLargePath = CardDir + "Fx_PlusL.png";
    private const int EdgeStripWidth = 4;
    private const int ParticlePoolSize = 54;
    public const string PrefabPath = "Assets/Prefabs/Roguelike/UI/UI_RoguelikeCard.prefab";

    public const int FrameWidth = 50;
    public const int FrameHeight = 81;
    private const int HoverLift = UI_RoguelikeCard.HoverLiftPixels;
    private const int ShadowWidth = 40;
    private const int ShadowHeight = 5;
    private static readonly Vector2Int ProfilePosition = new Vector2Int(8, 38);

    [MenuItem("LumberBoy/UI/Build Roguelike Card")]
    public static void Build()
    {
        Sprite frame = LoadSprite(CardDir + "CardFrame.png");
        Sprite profile = LoadSprite(CardDir + "CardProfile.png");
        Sprite[] levels =
        {
            LoadSprite(CardDir + "I.png"), LoadSprite(CardDir + "II.png"), LoadSprite(CardDir + "III.png"),
            LoadSprite(CardDir + "IV.png"), LoadSprite(CardDir + "V.png"), LoadOptionalSprite(CardDir + "VI.png"),
        };
        Sprite maxLevel = LoadOptionalSprite(CardDir + "MAX.png");

        // I~V와 프레임·프로필은 필수. VI와 MAX는 리소스가 아직 없을 수 있어(null이면 슬롯만 비워 둔다) 필수에서 뺀다.
        if (null == frame || null == profile || System.Array.IndexOf(levels, null, 0, 5) >= 0)
        {
            Debug.LogError("[RoguelikeCardBuilder] 스프라이트를 찾지 못했습니다(CardFrame, CardProfile, I~V).");
            return;
        }

        Sprite shadow = EnsureSpriteFromPixels(ShadowPath, BuildShadowPixels(ShadowWidth, ShadowHeight), ShadowWidth, ShadowHeight);
        Sprite edgeStrip = EnsureSpriteFromPixels(EdgeStripPath, BuildEdgeStripPixels(EdgeStripWidth, FrameHeight - 4), EdgeStripWidth, FrameHeight - 4);
        Sprite dot1 = EnsureSpriteFromPixels(Dot1Path, BuildFilledPixels(1, 1), 1, 1);
        Sprite dot2 = EnsureSpriteFromPixels(Dot2Path, BuildFilledPixels(2, 2), 2, 2);
        Sprite bar2 = EnsureSpriteFromPixels(Bar2Path, BuildFilledPixels(2, 1), 2, 1);
        Sprite plusSmall = EnsureSpriteFromPixels(PlusSmallPath, BuildPlusPixels(3), 3, 3);
        Sprite plusLarge = EnsureSpriteFromPixels(PlusLargePath, BuildPlusPixels(5), 5, 5);
        // 희귀 등급 층용 단일 스프라이트(셰이더가 UV로 픽셀 좌표를 구하므로 시트에서 잘린 조각이 아니라 한 장짜리여야 한다): 프레임 그림 그대로의 복사본.
        Sprite frameFull = EnsureSpriteFromPixels(FrameFullPath, ReadSpritePixels(frame, Mathf.RoundToInt(frame.rect.width), Mathf.RoundToInt(frame.rect.height)), Mathf.RoundToInt(frame.rect.width), Mathf.RoundToInt(frame.rect.height));
        Material[] frameMaterials = BuildGradeFrameMaterials(Mathf.RoundToInt(frame.rect.width), Mathf.RoundToInt(frame.rect.height));
        Sprite back = EnsureSpriteFromPixels(BackPath, BuildBackPixels(frame), Mathf.RoundToInt(frame.rect.width), Mathf.RoundToInt(frame.rect.height));

        GameObject root = new GameObject("UI_RoguelikeCard", typeof(RectTransform));
        root.layer = LayerMask.NameToLayer("UI");
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.sizeDelta = new Vector2(FrameWidth, FrameHeight);

        Image hitBox = CreateImage("HitBox", rootRect, null, Vector2Int.zero, new Vector2(FrameWidth, FrameHeight + HoverLift));
        hitBox.color = new Color(1f, 1f, 1f, 0f);
        hitBox.raycastTarget = true;

        // 그림자: 카드 아래(Visual보다 먼저 그려진다). 카드 아래 가장자리에 2px 걸치게 놓고, 투명도는 스크립트가 정한다.
        Image shadowImage = CreateImage("Shadow", rootRect, shadow, new Vector2Int((FrameWidth - ShadowWidth) / 2, -3), shadow.rect.size);
        shadowImage.color = new Color(0f, 0f, 0f, 0.35f);

        RectTransform visual = CreateRect("Visual", rootRect, Vector2Int.zero, new Vector2(FrameWidth, FrameHeight));

        // 피벗이 가운데 아래라 폭 스케일이 가운데를 기준으로 줄어든다. 앵커는 왼쪽 아래, 위치는 폭의 절반.
        RectTransform flipRoot = CreateRect("FlipRoot", visual, new Vector2Int(FrameWidth / 2, 0), new Vector2(FrameWidth, FrameHeight));
        flipRoot.pivot = new Vector2(0.5f, 0f);

        Image frameImage = CreateImage("Frame", flipRoot, frame, Vector2Int.zero, frame.rect.size);
        Image gradeFrameImage = CreateImage("GradeFrame", flipRoot, frameFull, Vector2Int.zero, frameFull.rect.size);
        gradeFrameImage.enabled = false;
        Image profileImage = CreateImage("Profile", flipRoot, profile, ProfilePosition, profile.rect.size);
        Image iconImage = CreateImage("Icon", flipRoot, null, Vector2Int.zero, Vector2.zero);
        iconImage.enabled = false;
        Image levelImage = CreateImage("Level", flipRoot, levels[0], Vector2Int.zero, levels[0].rect.size);

        Image flashImage = CreateImage("Flash", flipRoot, frame, Vector2Int.zero, frame.rect.size);
        flashImage.color = new Color(1f, 1f, 1f, 0f);
        flashImage.enabled = false;

        // 옆면(카드 두께): FlipRoot 밖(폭이 줄어도 4px 그대로)에서 가운데에 놓는다. 앞면 테두리 색을 그대로 쓰므로 색은 흰색(틴트 없음)으로 두고 알파만 바꾼다.
        // 프레임 모서리 밖으로 나가지 않게 위아래를 2px씩 안쪽으로.
        Image edgeLineImage = CreateImage("EdgeLine", visual, edgeStrip, new Vector2Int(FrameWidth / 2 - EdgeStripWidth / 2, 2), edgeStrip.rect.size);
        edgeLineImage.color = new Color(1f, 1f, 1f, 0f);
        edgeLineImage.enabled = false;

        UI_RoguelikeCardAura aura = CreateAura("Aura", visual);

        // 입자 이펙트: 카드 맨 위. 입자 풀. 위치와 크기는 스크립트가 정한다.
        RectTransform fxRect = CreateRect("Fx", rootRect, Vector2Int.zero, new Vector2(FrameWidth, FrameHeight));
        Image[] particleImages = new Image[ParticlePoolSize];
        for (int i = 0; i < ParticlePoolSize; i++)
        {
            particleImages[i] = CreateImage("Particle_" + i, fxRect, dot1, Vector2Int.zero, dot1.rect.size);
            particleImages[i].enabled = false;
        }

        UI_RoguelikePixelFx pixelFx = fxRect.gameObject.AddComponent<UI_RoguelikePixelFx>();
        SerializedObject fxSo = new SerializedObject(pixelFx);
        fxSo.FindProperty("dot1Sprite").objectReferenceValue = dot1;
        fxSo.FindProperty("dot2Sprite").objectReferenceValue = dot2;
        fxSo.FindProperty("bar2Sprite").objectReferenceValue = bar2;
        fxSo.FindProperty("plusSmallSprite").objectReferenceValue = plusSmall;
        fxSo.FindProperty("plusLargeSprite").objectReferenceValue = plusLarge;

        SerializedProperty particleProperty = fxSo.FindProperty("particleImages");
        particleProperty.arraySize = particleImages.Length;
        for (int i = 0; i < particleImages.Length; i++) particleProperty.GetArrayElementAtIndex(i).objectReferenceValue = particleImages[i];


        fxSo.ApplyModifiedPropertiesWithoutUndo();

        UI_RoguelikeCard card = root.AddComponent<UI_RoguelikeCard>();
        SerializedObject so = new SerializedObject(card);
        so.FindProperty("visual").objectReferenceValue = visual;
        so.FindProperty("flipRoot").objectReferenceValue = flipRoot;
        so.FindProperty("frameImage").objectReferenceValue = frameImage;
        so.FindProperty("profileImage").objectReferenceValue = profileImage;
        so.FindProperty("iconImage").objectReferenceValue = iconImage;
        so.FindProperty("levelImage").objectReferenceValue = levelImage;
        so.FindProperty("shadowImage").objectReferenceValue = shadowImage;
        so.FindProperty("flashImage").objectReferenceValue = flashImage;
        so.FindProperty("edgeLineImage").objectReferenceValue = edgeLineImage;
        so.FindProperty("pixelFx").objectReferenceValue = pixelFx;
        so.FindProperty("gradeFrameImage").objectReferenceValue = gradeFrameImage;
        so.FindProperty("aura").objectReferenceValue = aura;

        SerializedProperty frameMaterialProperty = so.FindProperty("gradeFrameMaterials");
        frameMaterialProperty.arraySize = frameMaterials.Length;
        for (int i = 0; i < frameMaterials.Length; i++) frameMaterialProperty.GetArrayElementAtIndex(i).objectReferenceValue = frameMaterials[i];

        so.FindProperty("backSprite").objectReferenceValue = back;

        SerializedProperty levelSprites = so.FindProperty("levelSprites");
        levelSprites.arraySize = levels.Length;
        for (int i = 0; i < levels.Length; i++) levelSprites.GetArrayElementAtIndex(i).objectReferenceValue = levels[i];
        so.FindProperty("maxLevelSprite").objectReferenceValue = maxLevel;

        so.ApplyModifiedPropertiesWithoutUndo();

        // 미리보기용: 레벨 I 위치를 스크립트와 같은 규칙으로 맞춰 둔다.
        card.SetCard(null, ECardKind.Active, ECardDisplayGrade.Bronze, 1, 7, false);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[RoguelikeCardBuilder] 완료: " + PrefabPath);
    }

    /// <summary>납작한 타원 그림자 픽셀(불투명 검정. 실제 투명도는 Image 색의 알파로 정한다).</summary>
    private static Color32[] BuildShadowPixels(int _w, int _h)
    {
        Color32[] pixels = new Color32[_w * _h];
        float cx = (_w - 1) * 0.5f;
        float cy = (_h - 1) * 0.5f;
        float rx = _w * 0.5f;
        float ry = _h * 0.5f;

        for (int y = 0; y < _h; y++)
        {
            for (int x = 0; x < _w; x++)
            {
                float nx = (x - cx) / rx;
                float ny = (y - cy) / ry;
                if (nx * nx + ny * ny <= 1f) pixels[y * _w + x] = new Color32(0, 0, 0, 255);
            }
        }

        return pixels;
    }

    /// <summary>
    /// 카드용 프레임 팔레트 스왑 머티리얼 3종. 셰이더가 프레임의 5톤(안쪽 그림자·몸통·바깥 링·가운데 링·하이라이트)을 이 팔레트 5톤으로 치환한다.
    /// 에메랄드 = 실버, 프리즘 = 어두운 보랏빛 + 무지개 링, 루비 = 붉은색. 몸통은 레벨 숫자와 아이콘 칸이 잘 읽히게 어둡게 둔다. 여러 번 실행해도 같은 에셋을 덮어쓴다.
    /// </summary>
    private static Material[] BuildGradeFrameMaterials(int _w, int _h)
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(FrameShaderPath);
        Material silver = GetOrCreateMaterial(MaterialDir + "/UI_CardGradeFrame_Silver.mat", shader);
        Material prism = GetOrCreateMaterial(MaterialDir + "/UI_CardGradeFrame_Prism.mat", shader);
        Material ruby = GetOrCreateMaterial(MaterialDir + "/UI_CardGradeFrame_Ruby.mat", shader);
        if (null == silver || null == prism || null == ruby) return new Material[3];

        Material[] all = { silver, prism, ruby };
        for (int i = 0; i < all.Length; i++)
        {
            all[i].SetVector("_PixelSize", new Vector4(_w, _h, 0f, 0f));
            // 반짝은 링 모서리: 가장자리에서 2px 안쪽(픽셀 중심 기준 반폭 22.5, 반높이 38).
            all[i].SetVector("_LineHalf", new Vector4(_w * 0.5f - 2.5f, _h * 0.5f - 2.5f, 0f, 0f));
            all[i].SetFloat("_Rainbow", 0f);
        }

        SetTones(silver, new Color(0.20f, 0.23f, 0.29f), new Color(0.30f, 0.34f, 0.41f), new Color(0.50f, 0.55f, 0.63f), new Color(0.74f, 0.79f, 0.86f), new Color(0.95f, 0.97f, 1.00f));
        SetTones(ruby, new Color(0.20f, 0.03f, 0.06f), new Color(0.34f, 0.05f, 0.09f), new Color(0.58f, 0.09f, 0.14f), new Color(0.86f, 0.22f, 0.24f), new Color(1.00f, 0.62f, 0.56f));
        SetTones(prism, new Color(0.14f, 0.12f, 0.22f), new Color(0.22f, 0.19f, 0.34f), new Color(0.55f, 0.50f, 0.80f), new Color(0.75f, 0.70f, 0.95f), new Color(0.95f, 0.93f, 1.00f));
        prism.SetFloat("_Rainbow", 1f);
        prism.SetFloat("_RainbowSaturation", 0.6f);
        prism.SetFloat("_RainbowSteps", 8f);

        for (int i = 0; i < all.Length; i++) EditorUtility.SetDirty(all[i]);
        return all;
    }

    private static void SetTones(Material _material, Color _t0, Color _t1, Color _t2, Color _t3, Color _t4)
    {
        _material.SetColor("_Tone0", _t0);
        _material.SetColor("_Tone1", _t1);
        _material.SetColor("_Tone2", _t2);
        _material.SetColor("_Tone3", _t3);
        _material.SetColor("_Tone4", _t4);
    }

    private static UI_RoguelikeCardAura CreateAura(string _name, RectTransform _parent)
    {
        RectTransform rect = CreateRect(_name, _parent, Vector2Int.zero, new Vector2(FrameWidth, FrameHeight));
        UI_RoguelikeCardAura aura = rect.gameObject.AddComponent<UI_RoguelikeCardAura>();
        aura.raycastTarget = false;

        return aura;
    }

    /// <summary>경로의 머티리얼을 불러오고, 없으면 셰이더로 새로 만든다. 이미 있으면 셰이더만 맞춘다.</summary>
    private static Material GetOrCreateMaterial(string _path, Shader _shader)
    {
        if (null == _shader)
        {
            Debug.LogError("[RoguelikeCardBuilder] 카드 셰이더를 찾지 못했습니다(경로: " + _path + ").");
            return null;
        }

        if (false == AssetDatabase.IsValidFolder(MaterialDir))
        {
            AssetDatabase.CreateFolder("Assets/Materials/UI", "Roguelike");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(_path);
        if (null == material)
        {
            material = new Material(_shader);
            AssetDatabase.CreateAsset(material, _path);
        }
        else if (material.shader != _shader)
        {
            material.shader = _shader;
        }

        return material;
    }

    /// <summary>_w x _h 불투명 흰색 사각형(1px·2px 입자, 2x1 줄기). 색은 스크립트가 틴트로 정한다.</summary>
    private static Color32[] BuildFilledPixels(int _w, int _h)
    {
        Color32[] pixels = new Color32[_w * _h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
        return pixels;
    }

    /// <summary>십자(+) 모양 반짝. 가운데 100%, 팔은 중심에서 멀어질수록 78% → 63% 로 어두워 입체감을 준다(틴트와 곱해진다).</summary>
    private static Color32[] BuildPlusPixels(int _size)
    {
        Color32[] pixels = new Color32[_size * _size];
        int c = _size / 2;

        for (int i = 0; i < _size; i++)
        {
            int distance = Mathf.Abs(i - c);
            byte value = (byte)(0 == distance ? 255 : (1 == distance ? 199 : 160));
            pixels[c * _size + i] = new Color32(value, value, value, 255);
            pixels[i * _size + c] = new Color32(value, value, value, 255);
        }

        return pixels;
    }

    /// <summary>
    /// 카드 옆면(두께). 앞면 프레임의 3px 테두리 링 색 그대로 어두운 갈색(#663931) - 밝은 갈색(#8f563b) - 밝은 갈색 - 어두운 갈색으로 가로 4px,
    /// 위아래 끝은 프레임의 가장 밝은 하이라이트색(#ca8f73)이고 모서리 한 칸은 둥글게 비운다.
    /// </summary>
    private static Color32[] BuildEdgeStripPixels(int _w, int _h)
    {
        Color32 dark = new Color32(0x66, 0x39, 0x31, 255);
        Color32 mid = new Color32(0x8f, 0x56, 0x3b, 255);
        Color32 light = new Color32(0xca, 0x8f, 0x73, 255);
        Color32[] pixels = new Color32[_w * _h];

        for (int y = 0; y < _h; y++)
        {
            bool bEnd = 0 == y || _h - 1 == y;
            bool bNearEnd = 1 == y || _h - 2 == y;

            for (int x = 0; x < _w; x++)
            {
                bool bSide = 0 == x || _w - 1 == x;
                Color32 c = bSide ? dark : mid;

                if (bEnd)
                {
                    if (bSide) continue;
                    c = light;
                }
                else if (bNearEnd)
                {
                    c = bSide ? mid : light;
                }

                pixels[y * _w + x] = c;
            }
        }

        return pixels;
    }

    /// <summary>
    /// 임시 카드 뒷면. 앞면 프레임과 **같은 테두리**를 쓴다: 앞면 프레임에서 몸통색이 아닌 픽셀(3px 테두리 링, 위쪽 하이라이트, 안쪽 그림자)은
    /// 색까지 그대로 복사하고(둥근 모서리도 같다), 몸통 자리에만 마름모 격자와 가운데 금색 마름모 문양을 그린다.
    /// 안쪽 색도 앞면 팔레트에서 파생한다(바탕 = 몸통색을 약간 어둡게, 격자 선 = 몸통색) - 앞면 프레임 이미지가 바뀌면 뒷면도 따라간다.
    /// 디자인 뒷면 이미지가 생기면 같은 이름(Card_Back.png)으로 교체한다.
    /// </summary>
    private static Color32[] BuildBackPixels(Sprite _frame)
    {
        int w = Mathf.RoundToInt(_frame.rect.width);
        int h = Mathf.RoundToInt(_frame.rect.height);
        Color32[] framePixels = ReadSpritePixels(_frame, w, h);
        Color32[] pixels = new Color32[w * h];

        Color32 body = MostCommonOpaqueColor(framePixels);
        Color32 baseColor = Darken(body, 0.82f);
        Color32 emblemFill = Darken(body, 0.6f);
        Color32 lattice = body;
        Color32 gold = new Color32(217, 166, 64, 255);
        Color32 goldBright = new Color32(243, 204, 89, 255);

        float cx = (w - 1) * 0.5f;
        float cy = (h - 1) * 0.5f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color32 front = framePixels[y * w + x];
                if (0 == front.a) continue;

                // 몸통색이 아닌 픽셀은 앞면 그대로(테두리·하이라이트·안쪽 그림자).
                if (front.r != body.r || front.g != body.g || front.b != body.b)
                {
                    pixels[y * w + x] = front;
                    continue;
                }

                float diamond = Mathf.Abs(x - cx) + Mathf.Abs(y - cy);
                Color32 c;
                if (diamond <= 2.5f) c = goldBright;
                else if (diamond <= 7.5f) c = emblemFill;
                else if (diamond <= 9.5f) c = gold;
                else c = 0 == (x + y) % 8 || 0 == (x - y + 80) % 8 ? lattice : baseColor;

                pixels[y * w + x] = c;
            }
        }

        return pixels;
    }

    /// <summary>불투명 픽셀 중 가장 많이 쓰인 색(프레임의 몸통색).</summary>
    private static Color32 MostCommonOpaqueColor(Color32[] _pixels)
    {
        System.Collections.Generic.Dictionary<int, int> counts = new System.Collections.Generic.Dictionary<int, int>(16);
        int bestKey = 0;
        int bestCount = 0;

        for (int i = 0; i < _pixels.Length; i++)
        {
            if (0 == _pixels[i].a) continue;

            int key = (_pixels[i].r << 16) | (_pixels[i].g << 8) | _pixels[i].b;
            counts.TryGetValue(key, out int count);
            counts[key] = ++count;

            if (count > bestCount)
            {
                bestCount = count;
                bestKey = key;
            }
        }

        return new Color32((byte)((bestKey >> 16) & 255), (byte)((bestKey >> 8) & 255), (byte)(bestKey & 255), 255);
    }

    private static Color32 Darken(Color32 _color, float _factor)
    {
        return new Color32((byte)Mathf.RoundToInt(_color.r * _factor), (byte)Mathf.RoundToInt(_color.g * _factor), (byte)Mathf.RoundToInt(_color.b * _factor), 255);
    }

    /// <summary>스프라이트의 텍스처 영역을 RGBA32(아래에서 위 순서)로 읽는다. 읽기 불가 텍스처도 RT를 거쳐 읽는다.</summary>
    private static Color32[] ReadSpritePixels(Sprite _sprite, int _w, int _h)
    {
        Texture2D source = _sprite.texture;
        Rect rect = _sprite.textureRect;

        RenderTexture previous = RenderTexture.active;
        RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(source, rt);
        RenderTexture.active = rt;

        Texture2D read = new Texture2D(_w, _h, TextureFormat.RGBA32, false);
        read.ReadPixels(new Rect(rect.x, rect.y, _w, _h), 0, 0);
        Color32[] pixels = read.GetPixels32();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        UnityEngine.Object.DestroyImmediate(read);
        return pixels;
    }

    /// <summary>
    /// 픽셀 배열로 PNG를 만들고 픽셀 아트 규칙(Point 필터·비압축·Single 스프라이트)으로 가져와 스프라이트를 돌려준다.
    /// 같은 경로에 이미 파일이 있으면 덮어쓰지 않는다 - 디자인 이미지로 바꿀 때는 같은 이름으로 교체하면 된다.
    /// _pixelsPerUnit은 9-슬라이스로 쓰는 그림이 캔버스의 Reference Pixels Per Unit과 같아야 테두리가 원래 픽셀 크기로 그려진다(다르면 테두리가 그 비율만큼 줄거나 는다).
    /// </summary>
    public static Sprite EnsureSpriteFromPixels(string _path, Color32[] _pixels, int _w, int _h, float _pixelsPerUnit = 100f)
    {
        if (false == System.IO.File.Exists(_path))
        {
            Texture2D tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false);
            tex.SetPixels32(_pixels);
            tex.Apply();
            System.IO.File.WriteAllBytes(_path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(_path, ImportAssetOptions.ForceSynchronousImport);

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(_path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = _pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        return LoadSprite(_path);
    }

    /// <summary>없을 수 있는 리소스. 파일이 없으면 null.</summary>
    private static Sprite LoadOptionalSprite(string _path)
    {
        return System.IO.File.Exists(_path) ? LoadSprite(_path) : null;
    }

    /// <summary>경로의 첫 번째 스프라이트(Multiple이면 잘린 첫 조각, Single이면 본체).</summary>
    public static Sprite LoadSprite(string _path)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(_path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite) return sprite;
        }

        return null;
    }

    /// <summary>이름이 _name인 스프라이트(Multiple 시트의 조각).</summary>
    public static Sprite LoadSprite(string _path, string _name)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(_path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite && sprite.name == _name) return sprite;
        }

        return null;
    }

    /// <summary>앵커·피벗이 왼쪽 아래(0, 0)인 자식 RectTransform. 위치는 부모 왼쪽 아래 기준 정수 픽셀.</summary>
    public static RectTransform CreateRect(string _name, RectTransform _parent, Vector2Int _position, Vector2 _size)
    {
        GameObject go = new GameObject(_name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");

        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(_parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.sizeDelta = _size;
        rect.anchoredPosition = _position;
        return rect;
    }

    public static Image CreateImage(string _name, RectTransform _parent, Sprite _sprite, Vector2Int _position, Vector2 _size)
    {
        RectTransform rect = CreateRect(_name, _parent, _position, _size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = _sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.raycastTarget = false;
        return image;
    }
}
#endif
