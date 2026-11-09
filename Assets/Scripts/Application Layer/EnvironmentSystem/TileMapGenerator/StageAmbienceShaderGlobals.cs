using UnityEngine;

/// <summary>
/// 스테이지 분위기 연출(돌풍·물 반짝임·색보정)을 켜고 끈다.
/// 전역값은 씬이 바뀌어도 남으므로, 본편 던전 맵을 깔 때(TileMapGenerator.GenerateMap)만 스테이지 데이터대로 켜고
/// 마을·로그라이크·메인메뉴·엔딩 등 던전 씬이 아닌 곳에서는 항상 0(꺼짐)으로 되돌린다.
/// 예외로 마을 분위기 연출(돌풍·물 반짝임·색보정)은 마을 Grid가 생길 때(TownTileManager.CreateGrid) 켜고 Grid가 사라질 때 끈다.
/// 0이면 해당 셰이더들은 기존과 같은 결과를 그린다.
/// </summary>
public static class StageAmbienceShaderGlobals
{
    // //내부 의존성
    private const float TilePixelsPerUnit = 32f;
    private const string DungeonSceneName = "DungeonScene";
    private const string TownSceneName = "TownScene";

    private static readonly int PixelsPerUnitID = Shader.PropertyToID("_StageAmbiencePixelsPerUnit");
    private static readonly int WindGustAID = Shader.PropertyToID("_WindGustA");
    private static readonly int WindGustBID = Shader.PropertyToID("_WindGustB");
    private static readonly int WindGustCID = Shader.PropertyToID("_WindGustC");
    private static readonly int WindGustDID = Shader.PropertyToID("_WindGustD");
    private static readonly int WaterShimmerAID = Shader.PropertyToID("_WaterShimmerA");
    private static readonly int WaterShimmerBID = Shader.PropertyToID("_WaterShimmerB");
    private static readonly int WaterShimmerSurfaceColorID = Shader.PropertyToID("_WaterShimmerSurfaceColor");
    private static readonly int WaterShimmerFoamColorID = Shader.PropertyToID("_WaterShimmerFoamColor");

    // 마을 Grid가 있는 동안의 마을 연출 설정.
    private static bool townAmbienceActive;
    private static WindGustSettings townWindGust;
    private static WaterShimmerSettings townWaterShimmer;
    private static StageColorGradingSettings townColorGrading;

    // // 퍼블릭 제어 메서드
    public static void Apply(StageTileDataSO _tileData)
    {
        // 로그라이크 필드도 같은 생성기로 깔리지만 본편 스테이지 연출만 대상이다.
        if (null == _tileData || GameModeContext.IsRoguelike)
        {
            Clear();
            return;
        }

        Shader.SetGlobalFloat(PixelsPerUnitID, TilePixelsPerUnit);
        ApplyWindGust(_tileData.WindGust);
        ApplyWaterShimmer(_tileData.WaterShimmer);
        // 색보정 볼륨(꺼진 스테이지는 아무것도 띄우지 않는다)
        StageAtmosphere.Apply(_tileData.ColorGrading, DungeonSceneName);
    }

    /// <summary>
    /// 마을 돌풍·물 반짝임·색보정을 켠다(null이거나 꺼진 설정은 꺼진다). 로그라이크면 모두 끈다.
    /// 마을 Grid가 있는 동안 설정을 기억해 두어, 씬 로드 때의 일괄 끄기 뒤에도 다시 켜진다.
    /// </summary>
    public static void ApplyTown(WindGustSettings _windGust, WaterShimmerSettings _waterShimmer, StageColorGradingSettings _colorGrading)
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
    }

    public static void Clear()
    {
        Shader.SetGlobalVector(WindGustBID, Vector4.zero);
        Shader.SetGlobalVector(WaterShimmerAID, Vector4.zero);
        StageAtmosphere.Remove();
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
    }

    /// <summary>
    /// 이 스테이지에서 바닥 타일맵에 끼울 잔디 바람 머티리얼. 돌풍이 꺼져 있거나 로그라이크면 null(원래 머티리얼 유지).
    /// </summary>
    public static Material GetGroundMaterialOverride(StageTileDataSO _tileData)
    {
        if (null == _tileData || GameModeContext.IsRoguelike)
        {
            return null;
        }

        WindGustSettings settings = _tileData.WindGust;
        if (null == settings || false == settings.enabled)
        {
            return null;
        }

        return settings.groundMaterial;
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
    }

    // 셰이더가 샘플한 텍스처 색과 같은 공간으로 맞춘다. 선형 색공간이면 sRGB 텍스처가 선형으로 읽히므로
    // 인스펙터에 적은(감마) 색도 선형으로 바꿔 넘긴다. 전역 색은 자동 변환되지 않는다.
    private static Vector4 ToShaderColor(Color _color)
    {
        return QualitySettings.activeColorSpace == ColorSpace.Linear ? (Vector4)_color.linear : (Vector4)_color;
    }

    // 던전 씬이 아닌 씬이 열리면 무조건 끈다(마을·메인메뉴·로그라이크·엔딩에 이전 던전 값이 남지 않게).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void RegisterSceneHook()
    {
        townAmbienceActive = false;
        townWindGust = null;
        townWaterShimmer = null;
        townColorGrading = null;
        Clear();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(UnityEngine.SceneManagement.Scene _scene, UnityEngine.SceneManagement.LoadSceneMode _mode)
    {
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
