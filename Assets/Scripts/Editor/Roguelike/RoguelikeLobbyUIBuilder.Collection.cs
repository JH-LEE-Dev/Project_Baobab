#if UNITY_EDITOR
using System.IO;
using PresentationLayer.VFX;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 카드 도감 창 구성기(RoguelikeLobbyUIBuilder의 partial). 여러 번 실행해도 같은 결과로 덮어쓴다.
///   LumberBoy/UI/Build Roguelike Collection     UIView_RoguelikeCollection.prefab (딤 · 640x360 SimRoot{ 판(진행도 · 머리줄 4 · 칸 26 · 상세 패널 · 뒤로 버튼) · 완성 연출 뒤 층 · 제목판 · 완성 연출 앞 층 })
///   LumberBoy/UI/Validate Roguelike Collection  18개 언어 x 도감 문구의 폭 검수(콘솔 표)
/// 좌표는 시뮬레이션(Docs/Roguelike/_Work/CardCollection/template.html, mock.py layout_a)과 같은 640x360 좌표(왼쪽 위 원점, 아래쪽 +)다.
///
/// [그림] 이 구성기가 굽는 그림(있어도 같은 값으로 덮어쓴다):
///   records/Slot_Back.png(36x36 뒷면 칸 = Panel_Info 9분할 + 가운데 금 마름모 문양, 시뮬레이션 mock.py slot_back과 같은 픽셀) · Slot_Outline.png(1px 테두리) · Slot_BackSpark.png(가운데 d ≤ 2 마름모)
///   owned/Title_Plate_Gold.png · Title_Plate_White.png (Title_Plate 색 바꿈 / 흰 실루엣, 9분할 테두리 4)
///   VFX/CardCollection/T_CollectionPlate_GhostWide.png · GhostCore.png (제목판 실루엣 가우시안 블러, 레어 등장 연출의 잔상과 같은 방식)
/// 빛 텍스처(T_RareCard_*)와 머티리얼(M_UILootGlowAdd · M_UILootParticles)은 레어 카드 등장 연출 구성기가 만든 것을 그대로 쓴다.
/// </summary>
public static partial class RoguelikeLobbyUIBuilder
{
    private const string CollectionPrefabPath = "Assets/Prefabs/Roguelike/UI/UIView_RoguelikeCollection.prefab";
    private const string CollectionVfxDir = "Assets/Graphics/VFX/CardCollection";
    private const string RareRevealDir = "Assets/Graphics/VFX/RareCardReveal/";
    private const string GlowAddMaterialPath = "Assets/Graphics/VFX/Materials/M_UILootGlowAdd.mat";
    private const string ParticleMaterialPath = "Assets/Graphics/VFX/Materials/M_UILootParticles.mat";
    private const string CardPrefabPath = "Assets/Prefabs/Roguelike/UI/UI_RoguelikeCard.prefab";
    private const string SlotBackPath = RecordsDir + "Slot_Back.png";
    private const string SlotOutlinePath = RecordsDir + "Slot_Outline.png";
    private const string SlotSparkPath = RecordsDir + "Slot_BackSpark.png";
    private const string PlateGoldPath = OwnedDir + "Title_Plate_Gold.png";
    private const string PlateWhitePath = OwnedDir + "Title_Plate_White.png";
    private const string PlateSourcePath = OwnedDir + "Title_Plate.png";
    private const string InfoSourcePath = OwnedDir + "Panel_Info.png";
    private const string BadgeNewPath = RecordsDir + "BadgeNew.png";
    private const string SpotRingsPath = RecordsDir + "SpotRings.png";

    // 도감 순서 26장(기획서 부록 C 카드 도감 계획 4-1): 액티브 7 · 패시브 10 · 프리즘 6 · 루비 3. 메인메뉴가 분류별로 다시 묶는다.
    private static readonly string[] CollectionCardIds =
    {
        "active_offroad", "active_spin", "active_shockwave", "active_drone", "active_boomerang", "active_stella", "active_sporebomb",
        "passive_range", "passive_damage", "passive_attackspeed", "passive_duration", "passive_dot", "passive_dot_speed", "passive_movespeed", "passive_stamina_recover", "passive_max_stamina", "passive_grace",
        "combo_spin_shockwave", "combo_spin_boomerang", "combo_spore_drone", "combo_spore_offroad", "combo_spore_shockwave", "combo_stella_drone",
        "ruby_glass_cannon", "ruby_more_the_merrier", "ruby_patience",
    };

    private const int CollectionSlotCount = 26;
    private const int CollectionHeaderCount = 4;
    private const int SlotSize = 36;
    private const int PlateWidth = 272;
    private const int PlateHeight24 = 24;
    private const int SimPanelX = 56;
    private const int SimPanelY = 18;
    private const int DetailX = 298;
    private const int DetailY = 40;
    private const int DetailW = 218;
    private const int DetailH = 250;

    // 시뮬레이션 색
    private static readonly Color32 SimMuted = new Color32(0x9A, 0x82, 0x6A, 255);
    private static readonly Color32 SimGold = new Color32(0xF3, 0xCC, 0x59, 255);
    private static readonly Color32 SimCream = new Color32(0xF1, 0xE4, 0xC8, 255);
    private static readonly Color32 SimLine = new Color32(0x3A, 0x29, 0x1F, 255);
    private static readonly Color32 SimSheen = new Color32(0xFF, 0xF6, 0xC8, 255);

    [MenuItem("LumberBoy/UI/Build Roguelike Collection")]
    public static void BuildCollection()
    {
        Assets a = LoadAssets();
        if (null == a) return;

        Sprite slotBack = BakeSlotBack();
        Sprite slotOutline = BakeSlotOutline();
        Sprite slotSpark = BakeSlotSpark();
        Sprite plateGold = BakePlateGold();
        Sprite plateWhite = BakePlateWhite();
        BakePlateGhosts(out Sprite ghostWide, out Sprite ghostCore);

        Material glowAdd = AssetDatabase.LoadAssetAtPath<Material>(GlowAddMaterialPath);
        Material particleMaterial = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
        GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        Sprite badgeNew = AssetDatabase.LoadAssetAtPath<Sprite>(BadgeNewPath);
        Sprite spotRings = AssetDatabase.LoadAssetAtPath<Sprite>(SpotRingsPath);
        Texture softTexture = AssetDatabase.LoadAssetAtPath<Texture>(RareRevealDir + "T_RareCard_Dot.png");
        if (null == slotBack || null == slotOutline || null == slotSpark || null == plateGold || null == plateWhite || null == ghostWide || null == ghostCore
            || null == glowAdd || null == particleMaterial || null == cardPrefab || null == badgeNew || null == spotRings || null == softTexture)
        {
            Debug.LogError("[RoguelikeLobbyUIBuilder] 카드 도감에 필요한 그림 · 머티리얼 · 카드 프리팹을 찾지 못했습니다. 레어 카드 등장 연출 구성기(LumberBoy/VFX/Build Rare Card Reveal)와 카드 구성기를 먼저 실행하십시오.");
            return;
        }

        bool bNew = null == AssetDatabase.LoadAssetAtPath<GameObject>(CollectionPrefabPath);
        GameObject root;
        if (true == bNew)
        {
            root = new GameObject("UIView_RoguelikeCollection", typeof(RectTransform));
            root.layer = LayerMask.NameToLayer("UI");
            root.AddComponent<UIView_RoguelikeCollection>();
        }
        else
        {
            root = PrefabUtility.LoadPrefabContents(CollectionPrefabPath);
        }

        try
        {
            UIView_RoguelikeCollection view = root.GetComponent<UIView_RoguelikeCollection>();
            if (null == view) { Debug.LogError("[RoguelikeLobbyUIBuilder] UIView_RoguelikeCollection 컴포넌트가 없습니다."); return; }

            RectTransform rootRect = ClearChildren(root);
            if (null == root.GetComponent<Canvas>())
            {
                Canvas rootCanvas = root.AddComponent<Canvas>();
                rootCanvas.overrideSorting = false;
            }

            if (null == root.GetComponent<GraphicRaycaster>()) root.AddComponent<GraphicRaycaster>();

            if (true == bNew)
            {
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;

                UIView_RoguelikeRecords records = AssetDatabase.LoadAssetAtPath<UIView_RoguelikeRecords>(RecordsPrefabPath);
                if (null != records)
                {
                    SerializedObject source = new SerializedObject(records);
                    SerializedObject target = new SerializedObject(view);
                    target.FindProperty("layer").intValue = source.FindProperty("layer").intValue;
                    target.FindProperty("bCloseableByESC").boolValue = source.FindProperty("bCloseableByESC").boolValue;
                    target.FindProperty("bOverlay").boolValue = source.FindProperty("bOverlay").boolValue;
                    target.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // 딤: 시뮬레이션 rgba(8, 6, 10, 150/255). 알파 페이드는 CanvasGroup이 한다.
            Image dim = Stretch("Dim", rootRect, new Color32(8, 6, 10, 150));
            dim.raycastTarget = true;
            CanvasGroup dimGroup = dim.gameObject.AddComponent<CanvasGroup>();
            Button dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.navigation = new Navigation { mode = Navigation.Mode.None };

            // 640x360 좌표 루트(왼쪽 위 원점). 자식은 모두 왼쪽 위 앵커 · 피벗이고 위치 = (x, -y)다.
            RectTransform simRoot = Centered("SimRoot", rootRect, 640f, 360f);

            // ---- 판 ----
            RectTransform panel = TopLeft("Panel", simRoot, 528f, 300f);
            panel.anchoredPosition = new Vector2(SimPanelX, -SimPanelY);
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.sprite = a.bookPanel;
            panelImage.type = Image.Type.Sliced;
            panelImage.raycastTarget = true;
            CanvasGroup panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
            Button panelButton = panel.gameObject.AddComponent<Button>();
            panelButton.transition = Selectable.Transition.None;
            panelButton.navigation = new Navigation { mode = Navigation.Mode.None };

            TMP_Text progressLabel = Text("ProgressLabel", panel, a.font, TextAlignmentOptions.TopLeft, SimMuted);
            TMP_Text progressCount = Text("ProgressCount", panel, a.font, TextAlignmentOptions.TopLeft, SimGold);
            TMP_Text progressPercent = Text("ProgressPercent", panel, a.font, TextAlignmentOptions.TopLeft, SimGold);
            UI_LootGlowParticles progressBar = CreateGlowGraphic("ProgressBar", panel, null);

            SerializedObject viewSo = new SerializedObject(view);
            SerializedProperty headersProperty = viewSo.FindProperty("headers");
            headersProperty.arraySize = CollectionHeaderCount;
            for (int i = 0; CollectionHeaderCount > i; i++)
            {
                BuildCollectionHeader(panel, a, i, headersProperty.GetArrayElementAtIndex(i));
            }

            UI_RoguelikeCollectionSlot[] slots = new UI_RoguelikeCollectionSlot[CollectionSlotCount];
            Image[] flashImages = new Image[CollectionSlotCount];
            Image[] outlineImages = new Image[CollectionSlotCount];
            for (int i = 0; CollectionSlotCount > i; i++)
            {
                slots[i] = BuildCollectionSlot(panel, a, i, slotSpark, badgeNew);
            }

            // ---- 상세 패널 ----
            RectTransform detailPanel = TopLeft("DetailPanel", panel, DetailW, DetailH);
            detailPanel.anchoredPosition = new Vector2(DetailX, -DetailY);
            Image detailImage = detailPanel.gameObject.AddComponent<Image>();
            detailImage.sprite = a.infoPanel;
            detailImage.type = Image.Type.Sliced;
            detailImage.raycastTarget = false;
            CanvasGroup detailGroup = detailPanel.gameObject.AddComponent<CanvasGroup>();
            RectTransform detailContent = StretchRect("Content", detailPanel);

            RectTransform rings = IconRect("SpotRings", detailContent, spotRings, spotRings.rect.width, spotRings.rect.height);
            rings.anchoredPosition = new Vector2(17f, -41f);

            // 카드 견본: 루트(50x81)의 왼쪽 아래 = 시뮬레이션 카드 캔버스(64x96) 왼쪽 위에서 (+7, +87). 상세 패널 기준 (84, 95).
            GameObject cardInstance = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab, detailContent);
            cardInstance.name = "DetailCard";
            RectTransform cardRect = (RectTransform)cardInstance.transform;
            cardRect.anchorMin = new Vector2(0f, 1f);
            cardRect.anchorMax = new Vector2(0f, 1f);
            cardRect.pivot = new Vector2(0f, 0f);
            cardRect.anchoredPosition = new Vector2(84f, -95f);
            UI_RoguelikeCard detailCard = cardInstance.GetComponent<UI_RoguelikeCard>();

            RectTransform divider = Solid("Divider", detailContent, SimLine);
            divider.anchorMin = new Vector2(0f, 1f);
            divider.anchorMax = new Vector2(0f, 1f);
            divider.pivot = new Vector2(0f, 1f);
            divider.sizeDelta = new Vector2(DetailW - 16f, 1f);
            divider.anchoredPosition = new Vector2(8f, -112f);

            RectTransform detailMask = TopLeft("TextMask", detailContent, DetailW - 20f, DetailH - 112f);
            detailMask.anchoredPosition = new Vector2(10f, -112f);
            detailMask.gameObject.AddComponent<RectMask2D>();
            TMP_Text detailKind = Text("Kind", detailMask, a.font, TextAlignmentOptions.TopLeft, SimCream);
            TMP_Text detailName = Text("Name", detailMask, a.font, TextAlignmentOptions.TopLeft, SimCream);
            TMP_Text detailDesc = Text("Desc", detailMask, a.font, TextAlignmentOptions.TopLeft, SimCream);
            detailDesc.textWrappingMode = TextWrappingModes.Normal;
            detailName.textWrappingMode = TextWrappingModes.Normal;
            RectTransform badgeRect = IconRect("MaxBadge", detailMask, a.slotLevelMax, a.slotLevelMax.rect.width, a.slotLevelMax.rect.height);
            Image maxBadge = badgeRect.GetComponent<Image>();
            TMP_Text maxLabel = Text("MaxLabel", detailMask, a.font, TextAlignmentOptions.TopLeft, SimGold);

            // ---- 뒤로 버튼 (판 아래 왼쪽, 시뮬레이션 (56, 323)) ----
            UI_OptionButton backButton = CreateBackButton(panel, a, out UI_KeyboardImage backKey);
            if (null == backButton) return;
            ((RectTransform)backButton.transform).anchoredPosition = new Vector2(0f, -5f);

            // ---- E9 창 안 어둡게 · 칸 반응 층 · E4 입자 ----
            Image panelDim = Stretch("PanelDim", panel, Color.clear);
            panelDim.sprite = a.bookPanel;
            panelDim.type = Image.Type.Sliced;
            panelDim.enabled = false;
            RectTransform flashLayer = StretchRect("FlashLayer", panel);
            for (int i = 0; CollectionSlotCount > i; i++)
            {
                flashImages[i] = CreateFlashImage("Flash" + i, flashLayer, a.infoPanel, true);
                outlineImages[i] = CreateFlashImage("Outline" + i, flashLayer, slotOutline, false);
            }

            UI_LootGlowParticles burstGraphic = CreateGlowGraphic("Burst", panel, null);

            for (int i = 0; CollectionSlotCount > i; i++)
            {
                SerializedObject slotSo = new SerializedObject(slots[i]);
                Set(slotSo, "flashImage", flashImages[i]);
                Set(slotSo, "outlineImage", outlineImages[i]);
                slotSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- 완성 연출 뒤 층 · 제목판 · 앞 층 ----
            RectTransform fxBack = NewTopLeftLayer("FxBack", simRoot);
            RectTransform plate = TopLeft("TitlePlate", simRoot, PlateWidth, PlateHeight24);
            plate.anchoredPosition = new Vector2(184f, -12f);
            Image plateImage = plate.gameObject.AddComponent<Image>();
            plateImage.sprite = a.titlePlate;
            plateImage.type = Image.Type.Sliced;
            plateImage.raycastTarget = false;
            CanvasGroup plateGroup = plate.gameObject.AddComponent<CanvasGroup>();
            TMP_Text plateText = Text("Title", plate, a.font, TextAlignmentOptions.TopLeft, SimCream);
            RectTransform fxFront = NewTopLeftLayer("FxFront", simRoot);

            UI_RoguelikeCollectionPlateFx plateFx = simRoot.gameObject.AddComponent<UI_RoguelikeCollectionPlateFx>();
            SerializedObject fxSo = new SerializedObject(plateFx);
            Sprite discLinear = LoadRare("T_RareCard_DiscLinear");
            Set(fxSo, "haloCoreImage", FxImage("HaloCore", fxBack, LoadRare("T_RareCard_HaloCore"), glowAdd, false));
            Set(fxSo, "haloMainImage", FxImage("HaloMain", fxBack, LoadRare("T_RareCard_HaloMain"), glowAdd, false));
            Set(fxSo, "haloDeepImage", FxImage("HaloDeep", fxBack, LoadRare("T_RareCard_HaloDeep"), glowAdd, false));
            Set(fxSo, "discCoreImage", FxImage("DiscCore", fxBack, discLinear, glowAdd, false));
            Image[] blobs = new Image[7];
            for (int i = 0; blobs.Length > i; i++) blobs[i] = FxImage("Blob" + i, fxBack, discLinear, glowAdd, false);
            SetArrayOf(fxSo, "blobImages", blobs);
            Set(fxSo, "edgeRingImage", FxImage("EdgeRing", fxFront, LoadRare("T_RareCard_EdgeRing"), glowAdd, true));
            SetArrayOf(fxSo, "ghostWideImages", new[] { FxImage("GhostWide0", fxFront, ghostWide, glowAdd, false), FxImage("GhostWide1", fxFront, ghostWide, glowAdd, false) });
            SetArrayOf(fxSo, "ghostCoreImages", new[] { FxImage("GhostCore0", fxFront, ghostCore, glowAdd, false), FxImage("GhostCore1", fxFront, ghostCore, glowAdd, false) });
            Set(fxSo, "shockMainImage", FxImage("ShockMain", fxFront, LoadRare("T_RareCard_ShockMain"), glowAdd, true));
            Set(fxSo, "shockCoreImage", FxImage("ShockCore", fxFront, LoadRare("T_RareCard_ShockCore"), glowAdd, true));
            Set(fxSo, "meetHaloImage", FxImage("MeetHalo", fxFront, LoadRare("T_RareCard_MeetHalo"), glowAdd, true));
            Set(fxSo, "meetLineImage", FxImage("MeetLine", fxFront, LoadRare("T_RareCard_MeetLine"), glowAdd, true));
            Set(fxSo, "softGraphic", FxParticles("SoftParticles", fxFront, glowAdd, softTexture));
            Set(fxSo, "pixelGraphic", FxParticles("PixelParticles", fxFront, particleMaterial, softTexture));
            fxSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 뷰 연결 ----
            SerializedProperty ids = viewSo.FindProperty("collectionCardIds");
            ids.arraySize = CollectionCardIds.Length;
            for (int i = 0; CollectionCardIds.Length > i; i++) ids.GetArrayElementAtIndex(i).stringValue = CollectionCardIds[i];

            Set(viewSo, "dimGroup", dimGroup);
            Set(viewSo, "dimButton", dimButton);
            Set(viewSo, "panelRoot", panel);
            Set(viewSo, "panelGroup", panelGroup);
            Set(viewSo, "panelButton", panelButton);
            Set(viewSo, "panelDim", panelDim);
            Set(viewSo, "plateRoot", plate);
            Set(viewSo, "plateGroup", plateGroup);
            Set(viewSo, "plateImage", plateImage);
            Set(viewSo, "plateText", plateText);
            Set(viewSo, "plateBrownSprite", a.titlePlate);
            Set(viewSo, "plateGoldSprite", plateGold);
            Set(viewSo, "plateWhiteSprite", plateWhite);
            Set(viewSo, "plateFx", plateFx);
            Set(viewSo, "progressLabel", progressLabel);
            Set(viewSo, "progressCount", progressCount);
            Set(viewSo, "progressPercent", progressPercent);
            Set(viewSo, "progressBar", progressBar);
            SetArray(viewSo, "slots", slots);
            Set(viewSo, "burstGraphic", burstGraphic);
            Set(viewSo, "detailGroup", detailGroup);
            Set(viewSo, "detailContent", detailContent);
            Set(viewSo, "ringsRect", rings);
            Set(viewSo, "detailCard", detailCard);
            Set(viewSo, "detailMask", detailMask);
            Set(viewSo, "detailKind", detailKind);
            Set(viewSo, "detailName", detailName);
            Set(viewSo, "detailDesc", detailDesc);
            Set(viewSo, "detailMaxBadge", maxBadge);
            Set(viewSo, "detailMaxLabel", maxLabel);
            Set(viewSo, "backButton", backButton);
            SetArray(viewSo, "padOnlyItems", backKey.transform);
            SetArray(viewSo, "keyGuideImages", backKey);
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            // 슬롯의 칸 그림 · 배지 스프라이트 연결
            for (int i = 0; CollectionSlotCount > i; i++)
            {
                SerializedObject slotSo = new SerializedObject(slots[i]);
                Set(slotSo, "normalFrameSprite", a.cardSlotNormal);
                Set(slotSo, "maxFrameSprite", a.cardSlotMax);
                Set(slotSo, "prismFrameSprite", a.cardSlotPrism);
                Set(slotSo, "rubyFrameSprite", a.cardSlotRuby);
                Set(slotSo, "backFrameSprite", slotBack);
                Set(slotSo, "maxBadgeSprite", a.slotLevelMax);
                Set(slotSo, "rubyBadgeSprite", a.slotLevelRuby);
                slotSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // 분류 머리줄 4개의 문구 · 위치는 런타임이 채운다. 층 순서: 판 · FxBack · 제목판 · FxFront(= 만든 순서).
            PrefabUtility.SaveAsPrefabAsset(root, CollectionPrefabPath);
            Debug.Log("[RoguelikeLobbyUIBuilder] 완료: " + CollectionPrefabPath);
        }
        finally
        {
            if (true == bNew) UnityEngine.Object.DestroyImmediate(root);
            else PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
    }

    // // 부품

    private static Sprite LoadRare(string _name)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(RareRevealDir + _name + ".png");
    }

    private static void SetArrayOf(SerializedObject _so, string _property, Image[] _values)
    {
        SerializedProperty property = _so.FindProperty(_property);
        property.arraySize = _values.Length;
        for (int i = 0; _values.Length > i; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = _values[i];
    }

    /// <summary>크기 0 · 왼쪽 위 앵커 · 피벗의 자식 Canvas 층(자식 위치 = SimRoot 좌표 (x, -y)). 연출이 자기 Canvas로 격리되어 판 전체가 다시 그려지지 않는다.</summary>
    private static RectTransform NewTopLeftLayer(string _name, RectTransform _parent)
    {
        RectTransform rect = TopLeft(_name, _parent, 0f, 0f);
        Canvas canvas = rect.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = false;
        return rect;
    }

    /// <summary>완성 연출용 빛 이미지: 가산 머티리얼 · 왼쪽 위 앵커 · 피벗 · 마스크 · 마우스 판정 없음 · 처음엔 꺼짐. 링은 9분할(스프라이트 테두리)이다.</summary>
    private static Image FxImage(string _name, RectTransform _parent, Sprite _sprite, Material _material, bool _bSliced)
    {
        RectTransform rect = TopLeft(_name, _parent, _sprite.rect.width, _sprite.rect.height);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = _sprite;
        image.material = _material;
        image.type = true == _bSliced ? Image.Type.Sliced : Image.Type.Simple;
        image.fillCenter = true;
        image.raycastTarget = false;
        image.maskable = false;
        image.enabled = false;
        return image;
    }

    /// <summary>완성 연출 입자 그래픽: SimRoot 크기(640x360), 왼쪽 위 앵커 · 피벗(좌표 원점 = SimRoot 왼쪽 위).</summary>
    private static UI_RoguelikeRareRevealParticles FxParticles(string _name, RectTransform _parent, Material _material, Texture _softTexture)
    {
        RectTransform rect = TopLeft(_name, _parent, 640f, 360f);
        UI_RoguelikeRareRevealParticles graphic = rect.gameObject.AddComponent<UI_RoguelikeRareRevealParticles>();
        graphic.material = _material;
        graphic.raycastTarget = false;
        graphic.maskable = false;
        SerializedObject so = new SerializedObject(graphic);
        so.FindProperty("softTexture").objectReferenceValue = _softTexture;
        so.ApplyModifiedPropertiesWithoutUndo();
        return graphic;
    }

    private static Image CreateFlashImage(string _name, RectTransform _parent, Sprite _sprite, bool _bSliced)
    {
        RectTransform rect = TopLeft(_name, _parent, SlotSize, SlotSize);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = _sprite;
        image.type = true == _bSliced ? Image.Type.Sliced : Image.Type.Simple;
        image.raycastTarget = false;
        image.color = Color.clear;
        image.enabled = false;
        return image;
    }

    private static RectTransform SolidTopLeft(string _name, RectTransform _parent, Color32 _color)
    {
        RectTransform rect = TopLeft(_name, _parent, 1f, 1f);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = _color;
        image.raycastTarget = false;
        return rect;
    }

    /// <summary>분류 머리줄(280x14) 하나. 문구 · 위치는 런타임이 채우고, 광택 복사본(Sheen*)은 띠(RectMask2D) 안에서만 보인다.</summary>
    private static void BuildCollectionHeader(RectTransform _panel, Assets _a, int _index, SerializedProperty _property)
    {
        RectTransform root = TopLeft("Header" + _index, _panel, 280f, 14f);
        CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        TMP_Text title = Text("Title", root, _a.font, TextAlignmentOptions.TopLeft, SimMuted);
        TMP_Text count = Text("Count", root, _a.font, TextAlignmentOptions.TopLeft, SimMuted);
        RectTransform line = SolidTopLeft("Line", root, SimLine);

        RectTransform sheenMask = TopLeft("SheenMask", root, 6f, 14f);
        sheenMask.gameObject.AddComponent<RectMask2D>();
        RectTransform sheenContent = TopLeft("SheenContent", sheenMask, 280f, 14f);
        TMP_Text sheenTitle = Text("Title", sheenContent, _a.font, TextAlignmentOptions.TopLeft, SimSheen);
        TMP_Text sheenCount = Text("Count", sheenContent, _a.font, TextAlignmentOptions.TopLeft, SimSheen);
        RectTransform sheenLine = SolidTopLeft("Line", sheenContent, SimSheen);
        sheenMask.gameObject.SetActive(false);

        _property.FindPropertyRelative("root").objectReferenceValue = root;
        _property.FindPropertyRelative("group").objectReferenceValue = group;
        _property.FindPropertyRelative("title").objectReferenceValue = title;
        _property.FindPropertyRelative("count").objectReferenceValue = count;
        _property.FindPropertyRelative("line").objectReferenceValue = line;
        _property.FindPropertyRelative("sheenMask").objectReferenceValue = sheenMask;
        _property.FindPropertyRelative("sheenContent").objectReferenceValue = sheenContent;
        _property.FindPropertyRelative("sheenTitle").objectReferenceValue = sheenTitle;
        _property.FindPropertyRelative("sheenCount").objectReferenceValue = sheenCount;
        _property.FindPropertyRelative("sheenLine").objectReferenceValue = sheenLine;
    }

    /// <summary>
    /// 도감 칸 하나(36x36): 판정(Slot, 움직이지 않음) / 그림(Visual, 정수 픽셀로 움직임) / 면(Face, 폭 배율로 뒤집힘) / NEW 배지.
    /// 흰 실루엣 · 금빛(Flash)과 금 테두리(Outline)는 판 위 별도 층에 따로 만들어 연결한다(E9).
    /// </summary>
    private static UI_RoguelikeCollectionSlot BuildCollectionSlot(RectTransform _panel, Assets _a, int _index, Sprite _slotSpark, Sprite _badgeNew)
    {
        RectTransform slotRect = TopLeft("Slot" + _index, _panel, SlotSize, SlotSize);
        Image hit = slotRect.gameObject.AddComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f);
        hit.raycastTarget = true;
        Button button = slotRect.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hit;
        button.navigation = new Navigation { mode = Navigation.Mode.Explicit };

        RectTransform visual = TopLeft("Visual", slotRect, SlotSize, SlotSize);
        CanvasGroup visualGroup = visual.gameObject.AddComponent<CanvasGroup>();
        visualGroup.blocksRaycasts = false;

        // 면: 피벗 가운데(가로 배율이 가운데 기준), 앵커 왼쪽 위
        RectTransform face = TopLeft("Face", visual, SlotSize, SlotSize);
        face.pivot = new Vector2(0.5f, 0.5f);
        face.anchoredPosition = new Vector2(SlotSize * 0.5f, -SlotSize * 0.5f);

        RectTransform frame = StretchRect("Frame", face);
        Image frameImage = frame.gameObject.AddComponent<Image>();
        frameImage.sprite = _a.infoPanel;
        frameImage.type = Image.Type.Sliced;
        frameImage.raycastTarget = false;

        RectTransform icon = TopLeft("Icon", face, SlotSize, SlotSize);
        Image iconImage = icon.gameObject.AddComponent<Image>();
        iconImage.raycastTarget = false;
        iconImage.enabled = false;

        RectTransform level = TopLeft("Level", face, 8f, 8f);
        Image levelImage = level.gameObject.AddComponent<Image>();
        levelImage.raycastTarget = false;
        levelImage.enabled = false;

        RectTransform spark = StretchRect("BackSpark", face);
        Image sparkImage = spark.gameObject.AddComponent<Image>();
        sparkImage.sprite = _slotSpark;
        sparkImage.raycastTarget = false;
        sparkImage.enabled = false;

        RectTransform badge = TopLeft("NewBadge", visual, _badgeNew.rect.width, _badgeNew.rect.height);
        badge.anchoredPosition = new Vector2(UI_RoguelikeCollectionSlot.NewBadgeX, -UI_RoguelikeCollectionSlot.NewBadgeY);
        Image badgeImage = badge.gameObject.AddComponent<Image>();
        badgeImage.sprite = _badgeNew;
        badgeImage.raycastTarget = false;
        badgeImage.enabled = false;

        UI_RoguelikeCollectionSlot slot = slotRect.gameObject.AddComponent<UI_RoguelikeCollectionSlot>();
        SerializedObject so = new SerializedObject(slot);
        Set(so, "visual", visual);
        Set(so, "visualGroup", visualGroup);
        Set(so, "face", face);
        Set(so, "frameImage", frameImage);
        Set(so, "iconImage", iconImage);
        Set(so, "levelImage", levelImage);
        Set(so, "backSparkImage", sparkImage);
        Set(so, "newBadgeImage", badgeImage);
        Set(so, "button", button);
        so.ApplyModifiedPropertiesWithoutUndo();
        return slot;
    }

    // // 그림 굽기

    private static Color32[] ReadPixels(string _path, out int _width, out int _height)
    {
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
        texture.LoadImage(File.ReadAllBytes(Path.GetFullPath(_path)));
        _width = texture.width;
        _height = texture.height;
        Color32[] pixels = texture.GetPixels32();
        UnityEngine.Object.DestroyImmediate(texture);
        return pixels;
    }

    /// <summary>mock.py nine(): 모서리 _border 고정, 변 · 가운데는 가장 가까운 점(PIL NEAREST)으로 늘인다. 픽셀은 아래 행부터(Unity 순서).</summary>
    private static Color32[] NineSlice(Color32[] _source, int _sourceSize, int _border, int _width, int _height)
    {
        Color32[] result = new Color32[_width * _height];
        for (int y = 0; _height > y; y++)
        {
            int sy = MapNine(y, _height, _sourceSize, _border);
            for (int x = 0; _width > x; x++)
            {
                int sx = MapNine(x, _width, _sourceSize, _border);
                // 위에서부터 센 좌표를 Unity의 아래 행 순서로 바꾼다.
                result[(_height - 1 - y) * _width + x] = _source[(_sourceSize - 1 - sy) * _sourceSize + sx];
            }
        }

        return result;
    }

    private static int MapNine(int _d, int _dstSize, int _srcSize, int _border)
    {
        if (_border > _d) return _d;
        if (_d >= _dstSize - _border) return _srcSize - (_dstSize - _d);

        int srcSpan = _srcSize - 2 * _border;
        int dstSpan = _dstSize - 2 * _border;
        int local = _d - _border;
        return _border + Mathf.Min(srcSpan - 1, Mathf.FloorToInt((local + 0.5f) * srcSpan / dstSpan));
    }

    private static void Put(Color32[] _pixels, int _width, int _height, int _x, int _yFromTop, Color32 _color)
    {
        _pixels[(_height - 1 - _yFromTop) * _width + _x] = _color;
    }

    private static Sprite WritePixelSprite(string _path, Color32[] _pixels, int _width, int _height, int _border, bool _bSrgb)
    {
        Texture2D texture = new Texture2D(_width, _height, TextureFormat.RGBA32, false, false);
        texture.SetPixels32(_pixels);
        texture.Apply(false);
        File.WriteAllBytes(Path.GetFullPath(_path), texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(_path, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(_path) as TextureImporter;
        if (null == importer) return null;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 32f;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.sRGBTexture = _bSrgb;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spriteBorder = new Vector4(_border, _border, _border, _border);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(_path);
    }

    private static Color32 Hex(int _r, int _g, int _b)
    {
        return new Color32((byte)_r, (byte)_g, (byte)_b, 255);
    }

    /// <summary>뒷면 칸: Panel_Info 9분할 36x36 위에, 칸 안쪽(4..32)에 마름모 거리 d = |x-18| + |y-18|로 문양과 격자를 그린다(mock.py slot_back과 같은 규칙).</summary>
    private static Sprite BakeSlotBack()
    {
        Color32[] info = ReadPixels(InfoSourcePath, out int infoWidth, out _);
        Color32[] pixels = NineSlice(info, infoWidth, 4, SlotSize, SlotSize);
        Color32 colorA = Hex(0x39, 0x25, 0x19);
        Color32 colorB = Hex(0x46, 0x2D, 0x1F);
        Color32 colorC = Hex(0xD9, 0xA6, 0x40);
        Color32 colorD = Hex(0x2A, 0x1B, 0x13);
        Color32 colorE = Hex(0xF3, 0xCC, 0x59);
        for (int y = 4; 33 > y; y++)
        {
            for (int x = 4; 33 > x; x++)
            {
                int d = Mathf.Abs(x - 18) + Mathf.Abs(y - 18);
                Color32 color;
                if (2 >= d) color = colorE;
                else if (5 >= d) color = colorD;
                else if (7 >= d) color = colorC;
                else if (8 == d) color = colorA;
                else if (0 == (((x - y) % 8) + 8) % 8 || 4 == (((x + y) % 8) + 8) % 8) color = colorB;
                else color = colorA;
                Put(pixels, SlotSize, SlotSize, x, y, color);
            }
        }

        return WritePixelSprite(SlotBackPath, pixels, SlotSize, SlotSize, 4, true);
    }

    /// <summary>E9 금 테두리: 36x36 흰 1px 사각 테두리(색은 Image 색으로 입힌다).</summary>
    private static Sprite BakeSlotOutline()
    {
        Color32[] pixels = new Color32[SlotSize * SlotSize];
        Color32 white = new Color32(255, 255, 255, 255);
        for (int i = 0; SlotSize > i; i++)
        {
            Put(pixels, SlotSize, SlotSize, i, 0, white);
            Put(pixels, SlotSize, SlotSize, i, SlotSize - 1, white);
            Put(pixels, SlotSize, SlotSize, 0, i, white);
            Put(pixels, SlotSize, SlotSize, SlotSize - 1, i, white);
        }

        return WritePixelSprite(SlotOutlinePath, pixels, SlotSize, SlotSize, 0, true);
    }

    /// <summary>뒷면 칸 착지 반짝: 가운데 d ≤ 2 마름모(13픽셀)만 흰색인 36x36.</summary>
    private static Sprite BakeSlotSpark()
    {
        Color32[] pixels = new Color32[SlotSize * SlotSize];
        Color32 white = new Color32(255, 255, 255, 255);
        for (int dy = -2; 2 >= dy; dy++)
        {
            for (int dx = -2; 2 >= dx; dx++)
            {
                if (2 >= Mathf.Abs(dx) + Mathf.Abs(dy)) Put(pixels, SlotSize, SlotSize, 18 + dx, 18 + dy, white);
            }
        }

        return WritePixelSprite(SlotSparkPath, pixels, SlotSize, SlotSize, 0, true);
    }

    /// <summary>금색 제목판: 갈색 제목판 4색을 금 계열로 바꾼다(663931 → 2a1a12, ca8f73 → f3cc59, 8f563b → a8744a, 5c3c28 → d9a640).</summary>
    private static Sprite BakePlateGold()
    {
        Color32[] pixels = ReadPixels(PlateSourcePath, out int width, out int height);
        for (int i = 0; pixels.Length > i; i++)
        {
            Color32 c = pixels[i];
            if (0 == c.a) continue;

            int key = (c.r << 16) | (c.g << 8) | c.b;
            switch (key)
            {
                case 0x663931: pixels[i] = Hex(0x2A, 0x1A, 0x12); break;
                case 0xCA8F73: pixels[i] = Hex(0xF3, 0xCC, 0x59); break;
                case 0x8F563B: pixels[i] = Hex(0xA8, 0x74, 0x4A); break;
                case 0x5C3C28: pixels[i] = Hex(0xD9, 0xA6, 0x40); break;
            }
        }

        return WritePixelSprite(PlateGoldPath, pixels, width, height, 4, true);
    }

    /// <summary>흰 제목판(E9 흰 실루엣): 불투명 픽셀을 모두 흰색으로.</summary>
    private static Sprite BakePlateWhite()
    {
        Color32[] pixels = ReadPixels(PlateSourcePath, out int width, out int height);
        for (int i = 0; pixels.Length > i; i++)
        {
            if (0 < pixels[i].a) pixels[i] = new Color32(255, 255, 255, pixels[i].a);
        }

        return WritePixelSprite(PlateWhitePath, pixels, width, height, 4, true);
    }

    /// <summary>제목판 실루엣(272x24)을 가운데 놓고 가우시안 블러한 잔상 텍스처: GhostWide 300x52(시그마 2.4) · GhostCore 280x32(시그마 0.5). 레어 카드 연출의 잔상과 같은 방식이다.</summary>
    private static void BakePlateGhosts(out Sprite _wide, out Sprite _core)
    {
        Color32[] plate = ReadPixels(PlateSourcePath, out int plateSize, out _);
        Color32[] silhouette = NineSlice(plate, plateSize, 4, PlateWidth, PlateHeight24);
        LootPillarGlowBuilder.EnsureFolder(CollectionVfxDir);
        _wide = BakeGhost("T_CollectionPlate_GhostWide", silhouette, PlateWidth + 28, PlateHeight24 + 28, 2.4f);
        _core = BakeGhost("T_CollectionPlate_GhostCore", silhouette, PlateWidth + 8, PlateHeight24 + 8, 0.5f);
    }

    private static Sprite BakeGhost(string _name, Color32[] _silhouette, int _width, int _height, float _sigma)
    {
        float[] field = new float[_width * _height];
        int offsetX = (_width - PlateWidth) / 2;
        int offsetY = (_height - PlateHeight24) / 2;
        for (int y = 0; PlateHeight24 > y; y++)
        {
            for (int x = 0; PlateWidth > x; x++) field[(y + offsetY) * _width + x + offsetX] = _silhouette[y * PlateWidth + x].a / 255f;
        }

        float[] blurred = GaussianBlur(field, _width, _height, _sigma);
        Color32[] pixels = new Color32[_width * _height];
        for (int i = 0; pixels.Length > i; i++) pixels[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(blurred[i]) * 255f));
        return LootPillarGlowBuilder.WriteSprite(CollectionVfxDir, _name, _width, _height, pixels, new Vector2(0.5f, 0.5f), _height);
    }

    private static float[] GaussianBlur(float[] _src, int _width, int _height, float _sigma)
    {
        int radius = Mathf.CeilToInt(_sigma * 3f);
        float[] kernel = new float[radius * 2 + 1];
        float sum = 0f;
        for (int i = -radius; radius >= i; i++)
        {
            kernel[i + radius] = Mathf.Exp(-(i * i) / (2f * _sigma * _sigma));
            sum += kernel[i + radius];
        }

        for (int i = 0; kernel.Length > i; i++) kernel[i] /= sum;

        float[] temp = new float[_src.Length];
        float[] dst = new float[_src.Length];
        for (int y = 0; _height > y; y++)
        {
            for (int x = 0; _width > x; x++)
            {
                float acc = 0f;
                for (int k = -radius; radius >= k; k++)
                {
                    int xx = x + k;
                    if (0 > xx || _width <= xx) continue;
                    acc += _src[y * _width + xx] * kernel[k + radius];
                }

                temp[y * _width + x] = acc;
            }
        }

        for (int y = 0; _height > y; y++)
        {
            for (int x = 0; _width > x; x++)
            {
                float acc = 0f;
                for (int k = -radius; radius >= k; k++)
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
}
#endif
