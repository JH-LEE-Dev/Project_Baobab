using UnityEngine;

/// <summary>
/// 스테이지 분위기 연출(돌풍·물 반짝임·색보정·포자 연출·숲의 숨결)을 켜고 끈다.
/// 전역값은 씬이 바뀌어도 남으므로, 던전 맵을 깔 때(TileMapGenerator.GenerateMap)만 스테이지 데이터대로 켜고
/// 마을·메인메뉴·엔딩 등 던전 씬이 아닌 곳에서는 항상 0(꺼짐)으로 되돌린다.
/// 로그라이크 필드도 같은 생성기로 깔리며 그 지형 컨셉(본편 스테이지)의 연출을 그대로 켠다. 게임 판정과 이어진
/// 나무 열기 차오름·포자막 상태 연출만 빼고, 용암 숲은 거품과 떠다니는 굳은 판을 덜 띄운다.
/// 지형이 바뀔 때는 StageAmbienceFade가 직전 연출을 서서히 끄고 새 연출로 바꾼 뒤 서서히 켠다.
/// 예외로 마을 분위기 연출(돌풍·물 반짝임·색보정)은 마을 Grid가 생길 때(TownTileManager.CreateGrid) 켜고 Grid가 사라질 때 끈다.
/// 0이면 해당 셰이더들은 기존과 같은 결과를 그린다.
/// </summary>
public static class StageAmbienceShaderGlobals
{
    // //내부 의존성
    private const float TilePixelsPerUnit = 32f;
    private const string DungeonSceneName = "DungeonScene";
    private const string TownSceneName = "TownScene";
    private const string RoguelikeSceneName = "RoguelikeScene";

    // 로그라이크 용암 숲은 섬 둘레가 모두 용암 바다라 본편보다 거품·굳은 판이 훨씬 많이 보인다. 그만큼 줄여 띄운다(렌더링 전용).
    // 거품: 같은 넓이에 올라오는 거품 수 비율(칸을 키워 줄인다, 거품 하나의 크기·박자는 그대로).
    private const float RoguelikeLavaBubbleDensity = 0.5f;
    // 떠다니는 굳은 판: 깔리는 비율에 곱하는 값.
    private const float RoguelikeLavaCrustCoverage = 0.3f;
    // 온도 무늬(식은 곳 어두운 빨강·뜨거운 곳 밝은 주황 얼룩): 식은 곳 문턱을 내리고 뜨거운 곳 문턱을 올리는 양(무늬 값 0~1).
    // 두 문턱 사이(원래 수면색)가 넓어져 얼룩이 조금 줄어든다. 얼룩 크기·흐름은 그대로.
    private const float RoguelikeLavaPatternNarrow = 0.04f;

    private static readonly int PixelsPerUnitID = Shader.PropertyToID("_StageAmbiencePixelsPerUnit");
    private static readonly int WindGustAID = Shader.PropertyToID("_WindGustA");
    private static readonly int WindGustBID = Shader.PropertyToID("_WindGustB");
    private static readonly int WindGustCID = Shader.PropertyToID("_WindGustC");
    private static readonly int WindGustDID = Shader.PropertyToID("_WindGustD");
    private static readonly int WindGustEID = Shader.PropertyToID("_WindGustE");
    private static readonly int WaterShimmerAID = Shader.PropertyToID("_WaterShimmerA");
    private static readonly int WaterShimmerBID = Shader.PropertyToID("_WaterShimmerB");
    private static readonly int WaterShimmerSurfaceColorID = Shader.PropertyToID("_WaterShimmerSurfaceColor");
    private static readonly int WaterShimmerFoamColorID = Shader.PropertyToID("_WaterShimmerFoamColor");
    private static readonly int WaterShimmerRimAID = Shader.PropertyToID("_WaterShimmerRimA");
    private static readonly int WaterShimmerRimBID = Shader.PropertyToID("_WaterShimmerRimB");
    private static readonly int WaterShimmerRimColorID = Shader.PropertyToID("_WaterShimmerRimColor");
    private static readonly int WaterShimmerRimGlowColorID = Shader.PropertyToID("_WaterShimmerRimGlowColor");
    private static readonly int SporeGlowAID = Shader.PropertyToID("_SporeGlowA");
    private static readonly int SporeGlowBID = Shader.PropertyToID("_SporeGlowB");
    private static readonly int WaterSporeAID = Shader.PropertyToID("_WaterSporeA");
    private static readonly int WaterSporeBID = Shader.PropertyToID("_WaterSporeB");
    private static readonly int WaterSporeCID = Shader.PropertyToID("_WaterSporeC");
    private static readonly int WaterSporeDID = Shader.PropertyToID("_WaterSporeD");
    private static readonly int WaterSporeColorID = Shader.PropertyToID("_WaterSporeColor");
    private static readonly int WaterSporeColorBID = Shader.PropertyToID("_WaterSporeColorB");
    private static readonly int WaterSporeSurfaceAID = Shader.PropertyToID("_WaterSporeSurfaceA");
    private static readonly int WaterSporeSurfaceBID = Shader.PropertyToID("_WaterSporeSurfaceB");
    private static readonly int WaterBubbleAID = Shader.PropertyToID("_WaterBubbleA");
    private static readonly int WaterBubbleBID = Shader.PropertyToID("_WaterBubbleB");
    private static readonly int WaterBubbleCID = Shader.PropertyToID("_WaterBubbleC");
    private static readonly int WaterBubbleBodyColorID = Shader.PropertyToID("_WaterBubbleBodyColor");
    private static readonly int WaterBubbleEdgeColorID = Shader.PropertyToID("_WaterBubbleEdgeColor");
    private static readonly int WaterBubbleHighlightColorID = Shader.PropertyToID("_WaterBubbleHighlightColor");
    private static readonly int WaterBubbleSurfaceID = Shader.PropertyToID("_WaterBubbleSurface");
    private static readonly int WaterMagmaAID = Shader.PropertyToID("_WaterMagmaA");
    private static readonly int WaterMagmaBID = Shader.PropertyToID("_WaterMagmaB");
    private static readonly int WaterMagmaCoolColorID = Shader.PropertyToID("_WaterMagmaCoolColor");
    private static readonly int WaterMagmaHotColorID = Shader.PropertyToID("_WaterMagmaHotColor");
    private static readonly int WaterMagmaSurfaceID = Shader.PropertyToID("_WaterMagmaSurface");
    private static readonly int WaterMagmaSeaAID = Shader.PropertyToID("_WaterMagmaSeaA");
    private static readonly int WaterMagmaSeaBID = Shader.PropertyToID("_WaterMagmaSeaB");
    private static readonly int WaterMagmaSeaFarColorID = Shader.PropertyToID("_WaterMagmaSeaFarColor");
    private static readonly int WaterMagmaCrustAID = Shader.PropertyToID("_WaterMagmaCrustA");
    private static readonly int WaterMagmaCrustBID = Shader.PropertyToID("_WaterMagmaCrustB");
    private static readonly int WaterMagmaCrustColorAID = Shader.PropertyToID("_WaterMagmaCrustColorA");
    private static readonly int WaterMagmaCrustColorBID = Shader.PropertyToID("_WaterMagmaCrustColorB");
    private static readonly int WaterMagmaCrustRimColorID = Shader.PropertyToID("_WaterMagmaCrustRimColor");
    private static readonly int HideWaterReflectionID = Shader.PropertyToID("_StageHideWaterReflection");
    private static readonly int WaterDepthAID = Shader.PropertyToID("_WaterDepthA");
    private static readonly int WaterDepthBID = Shader.PropertyToID("_WaterDepthB");
    private static readonly int WaterDepthSurfaceID = Shader.PropertyToID("_WaterDepthSurface");
    private static readonly int WaterDepthSideID = Shader.PropertyToID("_WaterDepthSide");
    private static readonly int WaterDepthDeepColorID = Shader.PropertyToID("_WaterDepthDeepColor");
    private static readonly int TreeTwinkleAID = Shader.PropertyToID("_TreeTwinkleA");
    private static readonly int TreeTwinkleBID = Shader.PropertyToID("_TreeTwinkleB");
    private static readonly int TreeTwinkleCID = Shader.PropertyToID("_TreeTwinkleC");
    private static readonly int TreeHeatChargeAID = Shader.PropertyToID("_TreeHeatChargeA");
    private static readonly int TreeHeatChargeBID = Shader.PropertyToID("_TreeHeatChargeB");
    private static readonly int TreeHeatChargeColorID = Shader.PropertyToID("_TreeHeatChargeColor");
    private static readonly int TreeDamageLookOnID = Shader.PropertyToID("_TreeDamageLookOn");

    // 마을 Grid가 있는 동안의 마을 연출 설정.
    private static bool townAmbienceActive;
    private static WindGustSettings townWindGust;
    private static WaterShimmerSettings townWaterShimmer;
    private static StageColorGradingSettings townColorGrading;
    private static WindPetalSettings townWindPetals;
    private static CloudShadowSettings townCloudShadows;

    /// <summary>
    /// 던전 연출 오브젝트(파티클·색보정 등)를 둘 씬 이름. 본편은 던전 씬, 로그라이크는 로그라이크 씬이다.
    /// </summary>
    public static string AmbienceSceneName => GameModeContext.IsRoguelike ? RoguelikeSceneName : DungeonSceneName;

    // // 퍼블릭 제어 메서드
    public static void Apply(StageTileDataSO _tileData)
    {
        if (null == _tileData)
        {
            Clear();
            return;
        }

        // 로그라이크 필드는 지형 컨셉(본편 스테이지)의 연출을 그대로 쓰되, 게임 판정과 이어진 연출(나무 열기 차오름·포자막)은 뺀다.
        bool bRoguelike = GameModeContext.IsRoguelike;
        string sceneName = AmbienceSceneName;
        bool bRoguelikeLava = bRoguelike && null != _tileData.WaterMagma && _tileData.WaterMagma.enabled;

        Shader.SetGlobalFloat(PixelsPerUnitID, TilePixelsPerUnit);
        ApplyWindGust(_tileData.WindGust);
        ApplyWaterShimmer(_tileData.WaterShimmer);
        ApplySporeGlow(_tileData.SporeGlow);
        ApplyWaterSpores(_tileData.WaterSpores);
        ApplyWaterBubbles(_tileData.WaterBubbles, bRoguelikeLava ? RoguelikeLavaBubbleDensity : 1f);
        ApplyWaterMagma(_tileData.WaterMagma, bRoguelikeLava ? RoguelikeLavaCrustCoverage : 1f, bRoguelikeLava ? RoguelikeLavaPatternNarrow : 0f);
        ApplyWaterDepth(_tileData.WaterDepth);
        // 물(용암) 위에 겹쳐 그리는 나무·캐릭터 반사를 끄는 스테이지(렌더링 전용)
        Shader.SetGlobalFloat(HideWaterReflectionID, _tileData.HideWaterReflections ? 1f : 0f);
        ApplyTreeStarTwinkle(_tileData.TreeStarTwinkle);
        // 나무 열기 차오름(꺼진 스테이지는 카운트다운 중이어도 아무것도 칠하지 않는다). 로그라이크는 나무 열기가 없어 늘 끈다.
        ApplyTreeHeatCharge(bRoguelike ? null : _tileData.TreeHeatCharge);
        // 나무가 깎인 모습(금·숯불). 열기 차오름이 켜진 스테이지(4스테이지, 로그라이크 용암 숲)에서만 그린다.
        ApplyTreeDamageLook(_tileData);
        // 색보정 볼륨(꺼진 스테이지는 아무것도 띄우지 않는다)
        StageAtmosphere.Apply(_tileData.ColorGrading, sceneName);
        // 포자 안개(꺼진 스테이지는 아무것도 띄우지 않는다)
        StageSporeHaze.Apply(_tileData.SporeHaze, sceneName);
        // 용암 아지랑이(꺼진 스테이지는 아무것도 띄우지 않는다)
        StageSporeHaze.Apply(_tileData.HeatHaze, sceneName, "HeatHaze");
        // 나무 타격 별가루(꺼진 스테이지는 아무것도 띄우지 않는다)
        StageHitSparkles.Apply(_tileData.HitSparkles, sceneName);
        // 숲의 숨결(꺼진 스테이지는 아무것도 만들지 않는다)
        StageForestBreath.Apply(_tileData.ForestBreath, sceneName);
        // 포자막 상태 연출(꺼진 스테이지는 아무것도 만들지 않는다). 포자막 판정이 없는 로그라이크는 늘 끈다.
        StageSporeShieldFx.Apply(bRoguelike ? null : _tileData.SporeShieldFx, sceneName);
        // 돌풍에 실려 날아가는 꽃잎·잎(꺼진 스테이지나 돌풍이 없는 스테이지는 아무것도 만들지 않는다)
        StageWindPetals.Apply(_tileData.WindPetals, _tileData.WindGust, sceneName);
        // 흐르는 구름 그림자(꺼진 스테이지는 아무것도 만들지 않는다)
        StageCloudShadows.Apply(_tileData.CloudShadows, sceneName);
    }

    /// <summary>
    /// 마을 돌풍·물 반짝임·색보정·돌풍 꽃잎·구름 그림자를 켠다(null이거나 꺼진 설정은 꺼진다). 로그라이크면 모두 끈다.
    /// 돌풍 꽃잎은 돌풍이 켜져 있을 때만 생긴다. 마을 Grid가 있는 동안 설정을 기억해 두어, 씬 로드 때의 일괄 끄기 뒤에도 다시 켜진다.
    /// </summary>
    public static void ApplyTown(WindGustSettings _windGust, WaterShimmerSettings _waterShimmer, StageColorGradingSettings _colorGrading,
        WindPetalSettings _windPetals, CloudShadowSettings _cloudShadows)
    {
        if (GameModeContext.IsRoguelike)
        {
            ClearTown();
            return;
        }

        townAmbienceActive = true;
        townWindGust = _windGust;
        townWaterShimmer = _waterShimmer;
        townColorGrading = _colorGrading;
        townWindPetals = _windPetals;
        townCloudShadows = _cloudShadows;
        ApplyTownSettings();
    }

    /// <summary>
    /// 마을 Grid가 사라질 때 마을 연출을 끈다.
    /// </summary>
    public static void ClearTown()
    {
        townAmbienceActive = false;
        townWindGust = null;
        townWaterShimmer = null;
        townColorGrading = null;
        townWindPetals = null;
        townCloudShadows = null;
        Clear();
    }

    /// <summary>
    /// 마을 바닥 타일맵에 끼울 잔디 바람 머티리얼. 돌풍이 꺼져 있거나 로그라이크면 null(원래 머티리얼 유지).
    /// </summary>
    public static Material GetTownGroundMaterialOverride(WindGustSettings _windGust)
    {
        if (null == _windGust || false == _windGust.enabled || GameModeContext.IsRoguelike)
        {
            return null;
        }

        return _windGust.groundMaterial;
    }

    private static void ApplyTownSettings()
    {
        Shader.SetGlobalFloat(PixelsPerUnitID, TilePixelsPerUnit);
        ApplyWindGust(townWindGust);
        ApplyWaterShimmer(townWaterShimmer);
        StageAtmosphere.Apply(townColorGrading, TownSceneName);
        StageWindPetals.Apply(townWindPetals, townWindGust, TownSceneName);
        StageCloudShadows.Apply(townCloudShadows, TownSceneName);
    }

    public static void Clear()
    {
        Shader.SetGlobalVector(WindGustBID, Vector4.zero);
        Shader.SetGlobalVector(WaterShimmerAID, Vector4.zero);
        Shader.SetGlobalVector(SporeGlowAID, Vector4.zero);
        Shader.SetGlobalVector(WaterSporeAID, Vector4.zero);
        Shader.SetGlobalVector(WaterBubbleAID, Vector4.zero);
        Shader.SetGlobalVector(WaterMagmaAID, Vector4.zero);
        Shader.SetGlobalVector(WaterDepthAID, Vector4.zero);
        Shader.SetGlobalFloat(HideWaterReflectionID, 0f);
        Shader.SetGlobalVector(TreeTwinkleAID, Vector4.zero);
        Shader.SetGlobalVector(TreeHeatChargeAID, Vector4.zero);
        Shader.SetGlobalFloat(TreeDamageLookOnID, 0f);
        StageAtmosphere.Remove();
        StageSporeHaze.Remove();
        StageMushroomSpores.Remove();
        StageDecoBreath.Remove();
        StageHitSparkles.Remove();
        StageLavaEmbers.Remove();
        StageLavaSpouts.Remove();
        StageDecoSteam.Remove();
        StageFireflies.Remove();
        StageForestBreath.Remove();
        StageSporeShieldFx.Remove();
        StageWindPetals.Remove();
        StageCloudShadows.Remove();
        StageLavaGlow.Clear();
        StageLavaSeaMap.Clear();
        StageShoreMap.Clear();
    }

    /// <summary>
    /// 이 스테이지에서 블룸 데코 타일맵에 끼울 반짝임 머티리얼. 꺼져 있으면 null(원래 머티리얼 유지).
    /// </summary>
    public static Material GetBloomDecoMaterialOverride(StageTileDataSO _tileData)
    {
        if (null == _tileData)
        {
            return null;
        }

        // 반짝임이 먼저, 그다음 불씨 데코 숨쉬기
        BloomDecoTwinkleSettings settings = _tileData.BloomDecoTwinkle;
        if (null != settings && true == settings.enabled && null != settings.material)
        {
            return settings.material;
        }

        BloomDecoBreathSettings breath = _tileData.BloomDecoBreath;
        if (null != breath && true == breath.enabled)
        {
            return breath.material;
        }

        return null;
    }

    private static void ApplyWindGust(WindGustSettings _settings)
    {
        if (null == _settings || false == _settings.enabled)
        {
            Shader.SetGlobalVector(WindGustBID, Vector4.zero);
            return;
        }

        Vector2 dir = _settings.direction.sqrMagnitude > 0.0001f ? _settings.direction.normalized : Vector2.right;
        Shader.SetGlobalVector(WindGustAID, new Vector4(dir.x, dir.y, _settings.speed, Mathf.Max(1f, _settings.spacing)));
        Shader.SetGlobalVector(WindGustBID, new Vector4(1f, _settings.bandHalfWidth, _settings.patchSize, _settings.patchCoverage));
        Shader.SetGlobalVector(WindGustCID, new Vector4(_settings.grassBendBrightness, _settings.treeLeanDegrees, _settings.treeFlutterSpeed, _settings.treeFlutterRatio));
        Shader.SetGlobalVector(WindGustDID, new Vector4(Mathf.Max(0.1f, _settings.grassClumpSize), _settings.grassBendThreshold, Mathf.Max(0f, _settings.grassBendSpread), Mathf.Max(0f, _settings.gustGroupLength)));
        Shader.SetGlobalVector(WindGustEID, new Vector4(Mathf.Clamp(_settings.smallPatchScale, 1f, 2f), 0f, 0f, 0f));
    }

    /// <summary>
    /// 이 스테이지에서 바닥 타일맵에 끼울 잔디 바람 머티리얼. 돌풍이 꺼져 있으면 null(원래 머티리얼 유지).
    /// </summary>
    public static Material GetGroundMaterialOverride(StageTileDataSO _tileData)
    {
        if (null == _tileData)
        {
            return null;
        }

        WindGustSettings settings = _tileData.WindGust;
        if (null != settings && settings.enabled)
        {
            return settings.groundMaterial;
        }

        // 용암 빛이 켜진 스테이지는 바닥을 용암 빛 머티리얼로 그린다(돌풍과 함께 켜는 스테이지는 없다).
        Material lavaGlow = GetLavaGlowMaterial(_tileData);
        if (null != lavaGlow)
        {
            return lavaGlow;
        }

        // 숲의 숨결이 켜진 스테이지는 바닥을 숨결 머티리얼로 그린다.
        return GetForestBreathGroundMaterial(_tileData);
    }

    /// <summary>
    /// 이 스테이지에서 물·육지 스텐실 타일맵에 끼울 머티리얼(용암 위 나무 그림자 없애기). 꺼져 있거나 두 머티리얼 중 하나라도 비었으면
    /// 둘 다 null(원래 머티리얼 유지). 한쪽만 바뀌면 육지 위 그림자까지 사라지므로 늘 함께 바꾼다.
    /// </summary>
    public static bool TryGetStencilMaterialOverrides(StageTileDataSO _tileData, out Material _waterStencil, out Material _groundStencil)
    {
        _waterStencil = null;
        _groundStencil = null;
        if (null == _tileData)
        {
            return false;
        }

        LavaShadowMaskSettings settings = _tileData.LavaShadowMask;
        if (null == settings || false == settings.enabled || null == settings.waterStencilMaterial || null == settings.groundStencilMaterial)
        {
            return false;
        }

        _waterStencil = settings.waterStencilMaterial;
        _groundStencil = settings.groundStencilMaterial;
        return true;
    }

    // 결정 장식 숨쉬기를 켰는데 앞 순서의 데코 머티리얼이 먼저 잡혀 쓰이지 않을 때 에디터에서 알린다.
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void WarnCrystalDecoBreathShadowed(StageTileDataSO _tileData, bool _bCrystalOn, string _winner)
    {
        if (_bCrystalOn)
        {
            Debug.LogWarning($"[StageAmbienceShaderGlobals] {_tileData.name}: 결정 장식 숨쉬기가 켜져 있지만 {_winner} 데코 머티리얼이 먼저 쓰여 적용되지 않습니다.", _tileData);
        }
    }

    /// <summary>
    /// 이 스테이지에서 바닥 장식 타일맵에 끼울 머티리얼. 용암 빛·숲의 숨결·결정 장식 숨쉬기가 꺼져 있으면 null(원래 머티리얼 유지).
    /// </summary>
    public static Material GetDecoMaterialOverride(StageTileDataSO _tileData)
    {
        if (null == _tileData)
        {
            return null;
        }

        // 데코 타일맵 머티리얼은 하나뿐이라 용암 빛 → 숲의 숨결 → 결정 장식 숨쉬기 순으로 하나만 쓴다.
        CrystalDecoBreathSettings crystalBreath = _tileData.CrystalDecoBreath;
        bool crystalOn = null != crystalBreath && true == crystalBreath.enabled;

        Material lavaGlow = GetLavaGlowMaterial(_tileData);
        if (null != lavaGlow)
        {
            WarnCrystalDecoBreathShadowed(_tileData, crystalOn, "용암 빛");
            return lavaGlow;
        }

        // 풀 장식도 바닥과 같이 숨결 물결에 물든다(바닥만 물들면 장식 자리가 어둡게 비어 보인다).
        Material forestBreath = GetForestBreathGroundMaterial(_tileData);
        if (null != forestBreath)
        {
            WarnCrystalDecoBreathShadowed(_tileData, crystalOn, "숲의 숨결");
            return forestBreath;
        }

        // 결정 장식 숨쉬기
        if (crystalOn)
        {
#if UNITY_EDITOR
            if (null == crystalBreath.material)
            {
                Debug.LogWarning($"[StageAmbienceShaderGlobals] {_tileData.name}: 결정 장식 숨쉬기가 켜져 있지만 머티리얼이 비어 있어 원래 데코 머티리얼로 그립니다.", _tileData);
            }
#endif
            return crystalBreath.material;
        }

        return null;
    }

    private static Material GetForestBreathGroundMaterial(StageTileDataSO _tileData)
    {
        ForestBreathSettings forestBreath = _tileData.ForestBreath;
        if (null == forestBreath || false == forestBreath.enabled)
        {
            return null;
        }

        return forestBreath.groundMaterial;
    }

    private static Material GetLavaGlowMaterial(StageTileDataSO _tileData)
    {
        LavaGlowSettings lavaGlow = _tileData.LavaGlow;
        if (null == lavaGlow || false == lavaGlow.enabled)
        {
            return null;
        }

        return lavaGlow.groundMaterial;
    }

    private static void ApplyWaterShimmer(WaterShimmerSettings _settings)
    {
        if (null == _settings || false == _settings.enabled)
        {
            Shader.SetGlobalVector(WaterShimmerAID, Vector4.zero);
            return;
        }

        Shader.SetGlobalVector(WaterShimmerAID, new Vector4(1f, _settings.sparkleCellTexels, _settings.sparkleDuty, _settings.sparkleIntensity));
        Shader.SetGlobalVector(WaterShimmerBID, new Vector4(_settings.sparkleArmStrength, 0f, _settings.colorTolerance, _settings.sparklePeriod));
        Shader.SetGlobalVector(WaterShimmerSurfaceColorID, ToShaderColor(_settings.surfaceColor));
        Shader.SetGlobalVector(WaterShimmerFoamColorID, ToShaderColor(_settings.foamColor));

        // 물가 테두리 달빛 물결(꺼져 있으면 세기 0이라 테두리는 그대로)
        if (_settings.rimGlowEnabled && _settings.rimGlowStrength > 0f)
        {
            Vector2 direction = _settings.rimWaveDirection.sqrMagnitude > 0.0001f ? _settings.rimWaveDirection.normalized : Vector2.right;
            Shader.SetGlobalVector(WaterShimmerRimAID, new Vector4(_settings.rimGlowStrength, Mathf.Max(0.5f, _settings.rimWaveSpacing), Mathf.Max(0.1f, _settings.rimWavePeriod), Mathf.Clamp(_settings.rimWaveWidth, 0.01f, 1f)));
            Shader.SetGlobalVector(WaterShimmerRimBID, new Vector4(direction.x, direction.y, 0f, 0f));
            Shader.SetGlobalVector(WaterShimmerRimColorID, ToShaderColor(_settings.rimColor));
            Shader.SetGlobalVector(WaterShimmerRimGlowColorID, ToShaderColor(_settings.rimGlowColor));
        }
        else
        {
            Shader.SetGlobalVector(WaterShimmerRimAID, Vector4.zero);
        }
    }

    private static void ApplySporeGlow(SporeGlowSettings _settings)
    {
        if (null == _settings || false == _settings.enabled)
        {
            Shader.SetGlobalVector(SporeGlowAID, Vector4.zero);
            return;
        }

        Shader.SetGlobalVector(SporeGlowAID, new Vector4(1f, Mathf.Max(0f, _settings.glowAmount), _settings.waveSpeed, Mathf.Max(0.5f, _settings.waveScale)));
        Shader.SetGlobalVector(SporeGlowBID, new Vector4(_settings.minWhite, _settings.maxSaturation, Mathf.Max(1f, _settings.sharpness), 0f));
    }

    private static void ApplyTreeStarTwinkle(TreeStarTwinkleSettings _settings)
    {
        if (null == _settings || false == _settings.enabled)
        {
            Shader.SetGlobalVector(TreeTwinkleAID, Vector4.zero);
            return;
        }

        // 별점 색은 셰이더가 감마로 되돌려 비교하므로 인스펙터 색(sRGB) 그대로 넘긴다.
        Shader.SetGlobalVector(TreeTwinkleAID, new Vector4(1f, Mathf.Max(0f, _settings.restLevel), Mathf.Max(0f, _settings.peakLevel), Mathf.Max(0.1f, _settings.period)));
        Shader.SetGlobalVector(TreeTwinkleBID, new Vector4(_settings.starColor.r, _settings.starColor.g, _settings.starColor.b, _settings.colorTolerance));
        Shader.SetGlobalVector(TreeTwinkleCID, new Vector4(Mathf.Clamp(_settings.duty, 0.01f, 1f), Mathf.Clamp01(_settings.hotMinRed), Mathf.Clamp01(_settings.hotMinRedLead), _settings.syncPerTree ? 1f : 0f));
    }

    private static void ApplyTreeHeatCharge(TreeHeatChargeSettings _settings)
    {
        if (null == _settings || false == _settings.enabled)
        {
            Shader.SetGlobalVector(TreeHeatChargeAID, Vector4.zero);
            return;
        }

        // 뜨거운 픽셀 판정은 셰이더가 감마로 되돌려 하므로 인스펙터 값 그대로, 더하는 빛 색은 선형으로 넘긴다.
        Shader.SetGlobalVector(TreeHeatChargeAID, new Vector4(1f, Mathf.Max(0f, _settings.hotBoost), Mathf.Max(0f, _settings.tintAmount), Mathf.Max(0.1f, _settings.curvePower)));
        Shader.SetGlobalVector(TreeHeatChargeBID, new Vector4(Mathf.Clamp01(_settings.hotMinRed), Mathf.Clamp01(_settings.hotMinRedLead), 0f, 0f));
        Shader.SetGlobalVector(TreeHeatChargeColorID, ToShaderColor(_settings.tintColor));
    }

    // 나무가 깎인 모습(금·숯불, TreeDamageLook.hlsl) 켜짐. 열기 차오름과 같은 스테이지에서 켜지지만, 로그라이크는
    // 열기 차오름을 끄고 이 값만 켜므로 따로 둔다.
    private static void ApplyTreeDamageLook(StageTileDataSO _tileData)
    {
        bool bOn = null != _tileData && null != _tileData.TreeHeatCharge && _tileData.TreeHeatCharge.enabled;
        Shader.SetGlobalFloat(TreeDamageLookOnID, bOn ? 1f : 0f);
    }

    private static void ApplyWaterSpores(WaterSporeSettings _settings)
    {
        if (null == _settings || false == _settings.enabled)
        {
            Shader.SetGlobalVector(WaterSporeAID, Vector4.zero);
            return;
        }

        Vector2 dir = _settings.driftDirection.sqrMagnitude > 0.0001f ? _settings.driftDirection.normalized : Vector2.right;
        Shader.SetGlobalVector(WaterSporeAID, new Vector4(1f, Mathf.Max(4f, _settings.cellTexels), _settings.duty, Mathf.Max(0f, _settings.brightness)));
        Shader.SetGlobalVector(WaterSporeBID, new Vector4(dir.x, dir.y, Mathf.Max(0f, _settings.travelTexels), Mathf.Max(0.1f, _settings.period)));
        Shader.SetGlobalVector(WaterSporeCID, new Vector4(_settings.wobbleTexels, _settings.colorTolerance, 0f, 0f));
        Shader.SetGlobalVector(WaterSporeDID, new Vector4(Mathf.Clamp01(_settings.glowArms), Mathf.Clamp(_settings.haloTexels, 0f, 3f), Mathf.Clamp01(_settings.haloAlpha), Mathf.Clamp01(_settings.twinkle)));
        Shader.SetGlobalVector(WaterSporeColorID, ToShaderColor(_settings.sporeColor));
        Shader.SetGlobalVector(WaterSporeColorBID, ToShaderColor(_settings.sporeColorB));
        Shader.SetGlobalVector(WaterSporeSurfaceAID, ToShaderColor(_settings.surfaceColorA));
        Shader.SetGlobalVector(WaterSporeSurfaceBID, ToShaderColor(_settings.surfaceColorB));
    }

    // _density: 같은 넓이에 올라오는 거품 수 비율(1이면 설정 그대로). 칸 넓이를 1 / _density배로 키워 줄인다.
    private static void ApplyWaterBubbles(WaterBubbleSettings _settings, float _density)
    {
        if (null == _settings || false == _settings.enabled)
        {
            Shader.SetGlobalVector(WaterBubbleAID, Vector4.zero);
            return;
        }

        float minRadius = Mathf.Max(0.5f, _settings.maxRadiusRange.x);
        float maxRadius = Mathf.Max(minRadius, _settings.maxRadiusRange.y);
        float cellTexels = Mathf.Max(6f, _settings.cellTexels);
        if (_density < 1f)
        {
            cellTexels /= Mathf.Sqrt(Mathf.Max(0.05f, _density));
        }
        Shader.SetGlobalVector(WaterBubbleAID, new Vector4(1f, cellTexels, Mathf.Clamp01(_settings.duty), Mathf.Max(0.1f, _settings.period)));
        Shader.SetGlobalVector(WaterBubbleBID, new Vector4(minRadius, maxRadius, Mathf.Clamp(_settings.popFraction, 0.05f, 0.9f), Mathf.Max(0f, _settings.highlightBrightness)));
        Shader.SetGlobalVector(WaterBubbleBodyColorID, ToShaderColor(_settings.bodyColor));
        Shader.SetGlobalVector(WaterBubbleEdgeColorID, ToShaderColor(_settings.edgeColor));
        Shader.SetGlobalVector(WaterBubbleHighlightColorID, ToShaderColor(_settings.highlightColor));
        Vector4 surface = ToShaderColor(_settings.surfaceColor);
        surface.w = Mathf.Max(0.0001f, _settings.colorTolerance);
        Shader.SetGlobalVector(WaterBubbleSurfaceID, surface);
        // 바다 쪽 빈도는 용암 바다 지도(StageLavaSeaMap)가 켜졌을 때만 쓰인다.
        Shader.SetGlobalVector(WaterBubbleCID, new Vector4(Mathf.Clamp01(_settings.seaFrequency), 0f, 0f, 0f));
    }

    private static void ApplyWaterDepth(WaterDepthSettings _settings)
    {
        if (null == _settings || false == _settings.enabled)
        {
            Shader.SetGlobalVector(WaterDepthAID, Vector4.zero);
            return;
        }

        // 물가 거리는 물가 지도(StageShoreMap)가 텍스처로 넘긴다(그 텍스처가 없으면 셰이더가 그리지 않는다).
        float startCells = Mathf.Max(0f, _settings.startCells);
        float fullCells = Mathf.Max(startCells + 0.1f, _settings.fullCells);
        Shader.SetGlobalVector(WaterDepthAID, new Vector4(1f, _settings.colorTolerance, Mathf.Clamp(_settings.bands, 2, 8), Mathf.Clamp01(_settings.ditherWidth)));
        Shader.SetGlobalVector(WaterDepthBID, new Vector4(startCells, fullCells, 0f, 0f));
        Shader.SetGlobalVector(WaterDepthSurfaceID, ToShaderColor(_settings.surfaceColor));
        Shader.SetGlobalVector(WaterDepthSideID, ToShaderColor(_settings.sideColor));
        Shader.SetGlobalVector(WaterDepthDeepColorID, ToShaderColor(_settings.deepColor));
    }

    // _crustCoverage: 떠다니는 굳은 판이 깔리는 비율에 곱하는 값(1이면 설정 그대로).
    private static void ApplyWaterMagma(WaterMagmaSettings _settings, float _crustCoverage, float _patternNarrow)
    {
        if (null == _settings || false == _settings.enabled)
        {
            Shader.SetGlobalVector(WaterMagmaAID, Vector4.zero);
            return;
        }

        Shader.SetGlobalVector(WaterMagmaAID, new Vector4(1f, Mathf.Max(0.2f, _settings.scale), Mathf.Max(0f, _settings.evolveSpeed), 0f));
        float cool = Mathf.Clamp01(_settings.coolThreshold);
        float hot = Mathf.Max(cool, Mathf.Clamp01(_settings.hotThreshold));
        // 본편은 _patternNarrow가 0이라 그대로다.
        if (_patternNarrow > 0f)
        {
            cool = Mathf.Clamp01(cool - _patternNarrow);
            hot = Mathf.Clamp01(hot + _patternNarrow);
        }
        Shader.SetGlobalVector(WaterMagmaBID, new Vector4(_settings.drift.x, _settings.drift.y, cool, hot));
        Shader.SetGlobalVector(WaterMagmaCoolColorID, ToShaderColor(_settings.coolColor));
        Shader.SetGlobalVector(WaterMagmaHotColorID, ToShaderColor(_settings.hotColor));
        Vector4 surface = ToShaderColor(_settings.surfaceColor);
        surface.w = Mathf.Max(0.0001f, _settings.colorTolerance);
        Shader.SetGlobalVector(WaterMagmaSurfaceID, surface);

        // 바다만 따로 칠하는 값. 용암 바다 지도(StageLavaSeaMap)가 켜졌을 때만 쓰인다.
        Shader.SetGlobalVector(WaterMagmaSeaAID, new Vector4(_settings.seaEnabled ? 1f : 0f, Mathf.Max(1f, _settings.seaFarCells), Mathf.Max(0.2f, _settings.seaScale), Mathf.Clamp01(_settings.seaFarDarken)));
        Shader.SetGlobalVector(WaterMagmaSeaBID, new Vector4(Mathf.Clamp01(_settings.seaFarCoolRaise), Mathf.Clamp01(_settings.seaFarHotRaise), 0f, 0f));
        Shader.SetGlobalVector(WaterMagmaSeaFarColorID, ToShaderColor(_settings.seaFarColor));
        bool bCrust = _settings.seaEnabled && _settings.crustEnabled;
        float crustNear = Mathf.Clamp01(_settings.crustCoverageNear);
        float crustFar = Mathf.Clamp01(_settings.crustCoverageFar);
        if (_crustCoverage < 1f)
        {
            crustNear *= Mathf.Max(0f, _crustCoverage);
            crustFar *= Mathf.Max(0f, _crustCoverage);
        }
        Shader.SetGlobalVector(WaterMagmaCrustAID, new Vector4(bCrust ? 1f : 0f, Mathf.Max(0.2f, _settings.crustSize), crustNear, crustFar));
        Shader.SetGlobalVector(WaterMagmaCrustBID, new Vector4(_settings.crustDrift.x, _settings.crustDrift.y, Mathf.Clamp01(_settings.crustGap), Mathf.Clamp01(_settings.crustRim)));
        Shader.SetGlobalVector(WaterMagmaCrustColorAID, ToShaderColor(_settings.crustColorA));
        Shader.SetGlobalVector(WaterMagmaCrustColorBID, ToShaderColor(_settings.crustColorB));
        Shader.SetGlobalVector(WaterMagmaCrustRimColorID, ToShaderColor(_settings.crustRimColor));
    }

    // 셰이더가 샘플한 텍스처 색과 같은 공간으로 맞춘다. 선형 색공간이면 sRGB 텍스처가 선형으로 읽히므로
    // 인스펙터에 적은(감마) 색도 선형으로 바꿔 넘긴다. 전역 색은 자동 변환되지 않는다.
    private static Vector4 ToShaderColor(Color _color)
    {
        return QualitySettings.activeColorSpace == ColorSpace.Linear ? (Vector4)_color.linear : (Vector4)_color;
    }

    // 던전 씬이 아닌 씬이 열리면 무조건 끈다(마을·메인메뉴·로그라이크·엔딩에 이전 던전 값이 남지 않게).
    // 로그라이크 지형 전환 도중에 씬이 바뀌어도 연출이 흐린 채로 남지 않게, 어느 씬이 열리든 서서히 끄고 켜기를 되돌린다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void RegisterSceneHook()
    {
        townAmbienceActive = false;
        townWindGust = null;
        townWaterShimmer = null;
        townColorGrading = null;
        townWindPetals = null;
        townCloudShadows = null;
        StageAmbienceFade.Discard();
        Clear();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(UnityEngine.SceneManagement.Scene _scene, UnityEngine.SceneManagement.LoadSceneMode _mode)
    {
        StageAmbienceFade.Discard();

        if (_scene.name != DungeonSceneName)
        {
            Clear();

            // 마을 Grid가 씬 로드 알림보다 먼저 만들어졌어도 마을 연출은 남긴다.
            if (townAmbienceActive)
            {
                ApplyTownSettings();
            }
        }
    }
}
