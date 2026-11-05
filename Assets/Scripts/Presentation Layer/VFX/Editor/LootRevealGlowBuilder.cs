#if UNITY_EDITOR
using System.Collections.Generic;
using PresentationLayer.VFX;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전리품 상세 HUD 발광(UI_LootRevealGlow) 에셋 구성기. 여러 번 실행해도 같은 결과로 맞춘다. 마을 기둥 빛 빌더를 먼저 실행해 둬야 한다(원반·빛 줄기 텍스처 공유).
///  1) 텍스처(쌍선형): 원반 4장 추가(감쇠 지수 1.3 / 1.6 / 1.7 / 2.0), 광선 개선판 12줄, 4갈래 별빛
///  2) 후광 아틀라스 T_LootRevealHalo + LootRevealGlowData: 성장 단계 확대 스프라이트(64x64) 실루엣을 시그마 4.4px로 번지게 구움(여백 16px)
///  3) 머티리얼 5개: 덮기 / 가산 / 백열(덮기, rgb 2배) — UIGlow, 빛 줄기 심 — UIBeamFlow, 픽셀 입자 — UIPixelGlow
///  4) HUD_LootReveal.prefab: IMG_Pillar 아래 IMG_Loot 앞에 RevealGlowBack(자식 Canvas, Image 15개 + 입자), 뒤에 RevealGlowFront(자식 Canvas, 반짝임),
///     IMG_Loot 아래 RevealFlash(자식 Canvas, 백열)를 넣고 HUD_LootReveal.revealGlow를 연결한다. 기존 VFX_Radial은 끄고 auraEffect 참조를 비운다.
///     발광 그래픽은 모두 maskable = false다(사용자 결정 B안: DimBG 띠 마스크 밖으로 빛 줄기·입자·오라가 올라가 보이게).
/// </summary>
public static class LootRevealGlowBuilder
{
    private const string TextureFolder = "Assets/Graphics/VFX/LootReveal";
    private const string PillarTextureFolder = "Assets/Graphics/VFX/LootPillar";
    private const string MaterialFolder = "Assets/Graphics/VFX/Materials";
    private const string GrowthDataPath = "Assets/Scriptable Obj/LootData/LootGrowthVisualData.asset";
    private const string HudPrefabPath = "Assets/Prefabs/UI/LootInfo/HUD_LootReveal.prefab";
    private const string BackName = "RevealGlowBack";
    private const string FrontName = "RevealGlowFront";
    private const string FlashName = "RevealFlash";

    private const int DiscSize = 64;
    private const int ShapeSize = 256;
    private const float GlowMax = 2.5f;
    private const float FlashTintScale = 2.0f;

    private static readonly string[] SlotNames =
    {
        "PoolTint", "PoolAdd", "BeamTint", "BeamCore", "CoreTint", "CoreAdd",
        "BurstTint", "BurstAdd", "BurstCore", "Rays", "LensWide", "LensCore", "LensV", "Star", "Halo",
    };

    [MenuItem("LumberBoy/VFX/Build Loot Reveal Glow")]
    public static void Build()
    {
        LootPillarGlowBuilder.EnsureFolder(TextureFolder);

        Sprite disc12 = AssetDatabase.LoadAssetAtPath<Sprite>(PillarTextureFolder + "/T_LootPillarDisc12.png");
        Sprite disc14 = AssetDatabase.LoadAssetAtPath<Sprite>(PillarTextureFolder + "/T_LootPillarDisc14.png");
        Sprite disc15 = AssetDatabase.LoadAssetAtPath<Sprite>(PillarTextureFolder + "/T_LootPillarDisc15.png");
        Sprite disc22 = AssetDatabase.LoadAssetAtPath<Sprite>(PillarTextureFolder + "/T_LootPillarDisc22.png");
        Sprite beamTint = AssetDatabase.LoadAssetAtPath<Sprite>(PillarTextureFolder + "/T_LootPillarBeamTint.png");
        Sprite beamCore = AssetDatabase.LoadAssetAtPath<Sprite>(PillarTextureFolder + "/T_LootPillarBeamCore.png");
        if (null == disc12 || null == disc14 || null == disc15 || null == disc22 || null == beamTint || null == beamCore)
        {
            Debug.LogError("[LootRevealGlowBuilder] 마을 기둥 빛 텍스처가 없습니다. LumberBoy/VFX/Build Loot Pillar Glow를 먼저 실행하세요.");
            return;
        }

        Sprite disc13 = BuildDisc("T_LootRevealDisc13", 1.3f);
        Sprite disc16 = BuildDisc("T_LootRevealDisc16", 1.6f);
        Sprite disc17 = BuildDisc("T_LootRevealDisc17", 1.7f);
        Sprite disc20 = BuildDisc("T_LootRevealDisc20", 2.0f);
        // 광선 개선판: 12줄(짝수 = 긴 줄 70px, 홀수 = 짧은 줄 44px), 반지름 70px = 텍스처 반 폭
        Sprite rays = BuildWedges("T_LootRevealRays", 12, 1.0f, 44.0f / 70.0f, 0.085f, 0.06f, 0.0f, 140.0f);
        // 4갈래 별빛: 긴 4줄(58px) + 45도 짧은 4줄(26px)
        Sprite star = BuildWedges("T_LootRevealStar", 8, 1.0f, 26.0f / 58.0f, 0.06f, 0.08f, 0.0f, 116.0f);
        if (null == disc13 || null == disc16 || null == disc17 || null == disc20 || null == rays || null == star) return;

        Material tint = BuildUiGlow("M_UILootGlowTint", 0.0f, 1.0f, 5.0f, 10.0f);
        Material add = BuildUiGlow("M_UILootGlowAdd", 1.0f, 1.0f, 5.0f, 1.0f);
        Material flash = BuildUiGlow("M_UILootGlowFlash", 0.0f, FlashTintScale, 5.0f, 10.0f);
        Material beam = LootPillarGlowBuilder.BuildMaterial("M_UILootBeamFlow", "Custom/UI/UIBeamFlow");
        Material particles = LootPillarGlowBuilder.BuildMaterial("M_UILootParticles", "Custom/UI/UIPixelGlow");
        if (null == tint || null == add || null == flash || null == beam || null == particles) return;

        beam.SetFloat("_GlowMax", GlowMax);
        beam.SetFloat("_HeightPx", VFX_LootPillarGlow.BeamHeightPx);
        beam.SetFloat("_BottomH", -VFX_LootPillarGlow.BeamBelowPx / VFX_LootPillarGlow.BeamHeightPx);
        particles.SetFloat("_GlowMax", GlowMax);
        EditorUtility.SetDirty(beam);
        EditorUtility.SetDirty(particles);

        LootPillarGlowData data = BuildData();
        if (null == data) return;

        AssetDatabase.SaveAssets();

        Sprite[] slotSprites =
        {
            disc12, disc14, beamTint, beamCore, disc15, disc22,
            disc14, disc17, disc20, rays, disc16, disc13, disc13, star, 0 < data.Count ? data.Get(0).halo : null,
        };
        Material[] slotMaterials = { tint, add, tint, beam, tint, add, tint, add, add, add, add, add, add, add, add };

        WireHud(slotSprites, slotMaterials, particles, flash, data);
        AssetDatabase.SaveAssets();
        Debug.Log("[LootRevealGlowBuilder] 완료: " + HudPrefabPath);
    }

    // ---------- 텍스처 ----------

    private static Sprite BuildDisc(string _name, float _power)
    {
        Color32[] pixels = new Color32[DiscSize * DiscSize];
        for (int y = 0; y < DiscSize; y++)
        {
            for (int x = 0; x < DiscSize; x++)
            {
                float nx = (x + 0.5f) / DiscSize * 2.0f - 1.0f;
                float ny = (y + 0.5f) / DiscSize * 2.0f - 1.0f;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                float alpha = 1.0f > d ? Mathf.Pow(1.0f - d, _power) : 0.0f;
                pixels[y * DiscSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255.0f));
            }
        }

        return LootPillarGlowBuilder.WriteSprite(TextureFolder, _name, DiscSize, DiscSize, pixels, new Vector2(0.5f, 0.5f), DiscSize);
    }

    // 시뮬레이션 drawWedges: 빛살마다 각도 반폭 w(r) = w0 x (1 - 0.7 r/len) + 0.6/r(시뮬레이션 내부 px), 세기 = across² x along^1.2를 더한다.
    // _count줄을 같은 간격으로 두고 짝수 번째는 길이 _longLen·폭 _longWidth, 홀수 번째는 _shortLen·_shortWidth. _diameterSim은 시뮬레이션 지름(px).
    private static Sprite BuildWedges(string _name, int _count, float _longLen, float _shortLen, float _longWidth, float _shortWidth, float _startAngle, float _diameterSim)
    {
        Color32[] pixels = new Color32[ShapeSize * ShapeSize];
        float half = ShapeSize * 0.5f;
        float simPerTex = _diameterSim * 2.0f / ShapeSize;   // 시뮬레이션 내부 해상도(x2) 기준

        for (int y = 0; y < ShapeSize; y++)
        {
            for (int x = 0; x < ShapeSize; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float acc = 0.0f;
                if (1.0f >= r && 0.0001f < r)
                {
                    float a = Mathf.Atan2(dy, dx);
                    float rSim = r * half * simPerTex;
                    for (int i = 0; i < _count; i++)
                    {
                        bool bLong = 0 == (i & 1);
                        float len = true == bLong ? _longLen : _shortLen;
                        if (r > len) continue;

                        float da = Mathf.DeltaAngle(a * Mathf.Rad2Deg, (_startAngle + i * 360.0f / _count)) * Mathf.Deg2Rad;
                        float w = (true == bLong ? _longWidth : _shortWidth) * (1.0f - 0.7f * r / len) + 0.6f / Mathf.Max(rSim, 1.0f);
                        if (Mathf.Abs(da) > w) continue;

                        float across = 1.0f - Mathf.Abs(da) / w;
                        float along = 1.0f - r / len;
                        acc += across * across * Mathf.Pow(along, 1.2f);
                    }
                }

                pixels[y * ShapeSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(acc) * 255.0f));
            }
        }

        return LootPillarGlowBuilder.WriteSprite(TextureFolder, _name, ShapeSize, ShapeSize, pixels, new Vector2(0.5f, 0.5f), ShapeSize);
    }

    // ---------- 머티리얼 ----------

    private static Material BuildUiGlow(string _name, float _mode, float _tintScale, float _srcBlend, float _dstBlend)
    {
        Material material = LootPillarGlowBuilder.BuildMaterial(_name, "Custom/UI/UIGlow");
        if (null == material) return null;

        material.SetFloat("_Mode", _mode);
        material.SetFloat("_GlowMax", GlowMax);
        material.SetFloat("_TintScale", _tintScale);
        material.SetFloat("_SrcBlend", _srcBlend);
        material.SetFloat("_DstBlend", _dstBlend);
        EditorUtility.SetDirty(material);
        return material;
    }

    // ---------- 데이터 ----------

    private static LootPillarGlowData BuildData()
    {
        LootGrowthVisualData growth = AssetDatabase.LoadAssetAtPath<LootGrowthVisualData>(GrowthDataPath);
        if (null == growth || null == growth.EditorTierSprites)
        {
            Debug.LogError("[LootRevealGlowBuilder] 성장 비주얼 데이터를 찾지 못했습니다: " + GrowthDataPath);
            return null;
        }

        List<Sprite> sprites = new List<Sprite>(16);
        List<LootGrowthVisualData.TierSprite> tiers = growth.EditorTierSprites;
        for (int i = 0; i < tiers.Count; i++)
        {
            Sprite sprite = tiers[i].sprite;
            if (null != sprite && false == sprites.Contains(sprite)) sprites.Add(sprite);
        }

        LootPillarGlowBuilder.HaloBakeSettings settings = new LootPillarGlowBuilder.HaloBakeSettings
        {
            atlasPath = TextureFolder + "/T_LootRevealHalo.png",
            dataPath = "Assets/Scriptable Obj/LootData/LootRevealGlowData.asset",
            namePrefix = "LootRevealHalo_",
            pad = 16,
            sigma = 4.4f,
            radius = 12,
            columns = 4,
            label = "확대 스프라이트",
        };

        return LootPillarGlowBuilder.BakeHaloData(sprites, settings);
    }

    // ---------- HUD 연결 ----------

    private static void WireHud(Sprite[] _slotSprites, Material[] _slotMaterials, Material _particles, Material _flash, LootPillarGlowData _data)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(HudPrefabPath);

        try
        {
            HUD_LootReveal hud = root.GetComponent<HUD_LootReveal>();
            if (null == hud)
            {
                Debug.LogError("[LootRevealGlowBuilder] HUD_LootReveal을 찾지 못했습니다.");
                return;
            }

            SerializedObject hudSo = new SerializedObject(hud);
            RectTransform pillar = hudSo.FindProperty("pillarRect").objectReferenceValue as RectTransform;
            Image loot = hudSo.FindProperty("lootItemImage").objectReferenceValue as Image;
            if (null == pillar || null == loot || loot.transform.parent != pillar)
            {
                Debug.LogError("[LootRevealGlowBuilder] IMG_Pillar / IMG_Loot 참조가 없거나 IMG_Loot가 IMG_Pillar의 자식이 아닙니다.");
                return;
            }

            // 이전 실행에서 넣은 오브젝트는 지우고 새로 넣는다
            DestroyChild(pillar, BackName);
            DestroyChild(pillar, FrontName);
            DestroyChild(loot.transform, FlashName);

            int lootIndex = loot.transform.GetSiblingIndex();

            // 뒤 층: IMG_Loot 바로 앞
            RectTransform back = CreateLayer(pillar, BackName);
            back.SetSiblingIndex(lootIndex);
            UI_LootRevealGlow glow = back.gameObject.AddComponent<UI_LootRevealGlow>();

            Image[] slots = new Image[UI_LootRevealGlow.SlotCount];
            for (int i = 0; i < UI_LootRevealGlow.SlotCount; i++)
            {
                GameObject child = new GameObject(SlotNames[i], typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform rt = child.GetComponent<RectTransform>();
                rt.SetParent(back, false);
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = UI_LootRevealGlow.SlotBeamTint == i || UI_LootRevealGlow.SlotBeamCore == i ? new Vector2(0.5f, UI_LootRevealGlow.BeamPivotY) : new Vector2(0.5f, 0.5f);
                rt.sizeDelta = UI_LootRevealGlow.SlotBaseSize[i];
                Image image = child.GetComponent<Image>();
                image.sprite = _slotSprites[i];
                image.material = _slotMaterials[i];
                image.raycastTarget = false;
                image.maskable = false;   // B안: 발광은 DimBG 띠 마스크 밖으로도 보인다
                image.enabled = false;
                slots[i] = image;
            }

            UI_LootGlowParticles motes = CreateParticles(back, "Motes", _particles);

            // 앞 층: IMG_Loot 바로 뒤
            RectTransform front = CreateLayer(pillar, FrontName);
            front.SetSiblingIndex(loot.transform.GetSiblingIndex() + 1);
            UI_LootGlowParticles twinkles = CreateParticles(front, "Twinkles", _particles);

            // 백열: IMG_Loot를 덮는 같은 스프라이트
            GameObject flashObject = new GameObject(FlashName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Canvas));
            RectTransform flashRect = flashObject.GetComponent<RectTransform>();
            flashRect.SetParent(loot.transform, false);
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.offsetMin = Vector2.zero;
            flashRect.offsetMax = Vector2.zero;
            flashRect.pivot = loot.rectTransform.pivot;
            Image flashImage = flashObject.GetComponent<Image>();
            flashImage.sprite = loot.sprite;
            flashImage.preserveAspect = loot.preserveAspect;
            flashImage.material = _flash;
            flashImage.raycastTarget = false;
            flashImage.maskable = false;
            flashImage.enabled = false;

            SerializedObject so = new SerializedObject(glow);
            so.FindProperty("data").objectReferenceValue = _data;
            so.FindProperty("pillarRect").objectReferenceValue = pillar;
            so.FindProperty("lootImage").objectReferenceValue = loot;
            SerializedProperty slotProp = so.FindProperty("slots");
            slotProp.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++) slotProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            so.FindProperty("moteGraphic").objectReferenceValue = motes;
            so.FindProperty("twinkleGraphic").objectReferenceValue = twinkles;
            so.FindProperty("flashImage").objectReferenceValue = flashImage;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 기존 방사형 광선(VFX_Radial)을 끄고 HUD 참조를 바꾼다(코드는 revealGlow가 있으면 그것을 재생한다)
            UnityEngine.Object aura = hudSo.FindProperty("auraEffect").objectReferenceValue;
            if (aura is Component auraComponent) auraComponent.gameObject.SetActive(false);
            hudSo.FindProperty("auraEffect").objectReferenceValue = null;
            hudSo.FindProperty("revealGlow").objectReferenceValue = glow;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // 기둥 Image와 같은 사각형·피벗을 가진 자식 Canvas 층(자식 위치 = 기둥 기준 px)
    private static RectTransform CreateLayer(RectTransform _pillar, string _name)
    {
        GameObject layer = new GameObject(_name, typeof(RectTransform), typeof(Canvas));
        RectTransform rt = layer.GetComponent<RectTransform>();
        rt.SetParent(_pillar, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = _pillar.pivot;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static UI_LootGlowParticles CreateParticles(RectTransform _parent, string _name, Material _material)
    {
        GameObject child = new GameObject(_name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UI_LootGlowParticles));
        RectTransform rt = child.GetComponent<RectTransform>();
        rt.SetParent(_parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = _parent.pivot;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        UI_LootGlowParticles graphic = child.GetComponent<UI_LootGlowParticles>();
        graphic.material = _material;
        graphic.raycastTarget = false;
        graphic.maskable = false;     // B안: 발광은 DimBG 띠 마스크 밖으로도 보인다
        return graphic;
    }

    private static void DestroyChild(Transform _parent, string _name)
    {
        Transform old = _parent.Find(_name);
        if (null != old) UnityEngine.Object.DestroyImmediate(old.gameObject);
    }
}
#endif
