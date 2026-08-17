/// <summary>
/// 세이브 파일이 어느 배포 빌드에서 만들어졌는지 나타냅니다.
/// 이 값은 세이브 파일에 정수로 그대로 직렬화되므로, 기존 값의 숫자를 바꾸거나 재사용하면 안 됩니다.
/// </summary>
public enum SaveBuildVariant
{
    /// <summary>변형 표기가 없던 시절(= 이 기능이 들어가기 전 데모)의 세이브. 항상 데모로 간주합니다.</summary>
    Unknown = 0,
    Demo = 1,
    Release = 2,
}

/// <summary>
/// 지금 실행 중인 빌드가 데모인지 정식인지 판정합니다.
///
/// [빌드 방법]
/// 정식 출시 빌드를 만들 때만 Project Settings > Player > Scripting Define Symbols 에
/// BAOBAB_FULL_RELEASE 를 추가하십시오. 데모 빌드는 아무 것도 설정할 필요가 없습니다.
///
/// 기본값(정의 없음)을 Demo로 둔 이유:
/// - 이미 배포된 데모는 변형 표기가 없어(Unknown) 어차피 데모로 판정되고, 이후 데모 패치도
///   설정을 잊어버릴 여지가 없다.
/// - 정의를 깜빡했을 때의 결과가 "정식이 데모 세이브를 이어받는 사고"가 아니라
///   "정식 빌드가 자기 세이브만 못 알아보는" 쪽이 되도록, 실수의 방향을 안전한 쪽으로 몰아둔다.
/// </summary>
public static class BuildInfo
{
#if BAOBAB_FULL_RELEASE
    public static SaveBuildVariant Variant => SaveBuildVariant.Release;
#else
    public static SaveBuildVariant Variant => SaveBuildVariant.Demo;
#endif

    public static bool IsFullRelease => SaveBuildVariant.Release == Variant;

    /// <summary>
    /// 세이브에 기록된 변형을 현재 빌드에서 이어서 플레이해도 되는지 판정합니다.
    /// 데모↔정식은 서로 호환되지 않으며, 호환되지 않는 세이브는 "없는 것"으로 취급되어
    /// 다음 저장 때 그대로 덮어써집니다.
    /// </summary>
    public static bool IsSaveVariantCompatible(SaveBuildVariant _savedVariant)
    {
        // 표기가 없는 세이브(Unknown)는 이 기능이 들어가기 전, 즉 데모에서만 만들어질 수 있었다.
        SaveBuildVariant _normalized = (SaveBuildVariant.Unknown == _savedVariant) ? SaveBuildVariant.Demo : _savedVariant;

        // 아직 모르는 미래 값(구버전 클라이언트가 신버전 세이브를 만난 경우)도 자연히 불일치 처리된다.
        return _normalized == Variant;
    }
}
