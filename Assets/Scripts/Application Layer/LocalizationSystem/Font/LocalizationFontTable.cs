using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 언어별로 어떤 TMP 폰트를 쓸지 담아두는 설정 에셋입니다.
///
/// 갈무리11은 한글·영문·기호만 담고 있어서 일본어 가나/한자, 중국어 간체·번체는
/// 글리프가 없거나(두부 현상) 한국식 한자 모양으로 나옵니다. 그래서 폴백(fallback)으로
/// 덧붙이는 대신, 해당 언어에서는 폰트 자체를 통째로 교체합니다.
/// (한중일이 공유하는 한자 코드포인트가 언어마다 자형이 달라, 폴백으로는 해결되지 않습니다)
///
/// 항목을 비워두면 "원본 폰트 유지"를 뜻합니다. 이 프로젝트는 모든 UI가 이미
/// Galmuri11_Optimum으로 제작되어 있으므로, 한국어·영어는 비워두는 것이 곧
/// "갈무리11 사용"이면서 숫자 전용 폰트 같은 예외를 건드리지 않는 가장 안전한 설정입니다.
/// 반대로 어떤 언어에서 무조건 한 폰트로 통일하고 싶다면 그 폰트를 직접 지정하면 됩니다.
///
/// 독일어·프랑스어·포르투갈어·스페인어·러시아어는 Lorem_Optimum 하나를 공유합니다.
/// 갈무리11도 라틴 확장과 키릴을 갖고 있어 비워두는 선택지가 있었지만, 다섯 언어의
/// 자형을 한곳에서 관리할 수 있도록 전용 폰트를 둡니다.
///
/// Lorem_Optimum은 정적(Static) 아틀라스이고 원본 TTF 참조도 비어 있어, 런타임에
/// 글리프를 채우지 못합니다. 따라서 이 다섯 언어의 번역문에 새 글자가 생기면 한 자라도
/// Tools/Localization/Generate Character Sets and Bake Atlases를 돌려야 합니다.
/// (CJK 폰트도 같은 이유로 같은 제약을 받습니다.
///  Generate Keys는 문자셋 목록만 다시 쓰고 아틀라스는 굽지 않습니다)
///
/// Lorem에는 한글·CJK 글리프가 없습니다. 첫 실행 팝업은 모든 언어 이름을 한 화면에
/// 띄우는데, 이때 화면 전체가 Lorem으로 교체되면서 "한국어/日本語/简体中文/繁體中文"
/// 네 라벨만 글리프를 못 찾습니다. 그래서 Lorem_Optimum의 폴백에 FusionPixel_zh_hans를
/// 걸어두었습니다. (이 네 라벨에 필요한 12자를 모두 갖고 있는 유일한 폰트입니다)
/// </summary>
[CreateAssetMenu(fileName = "LocalizationFontTable", menuName = "Localization/Font Table")]
public class LocalizationFontTable : ScriptableObject
{
    [Serializable]
    public struct FontEntry
    {
        public Language language;

        [Tooltip("이 언어에서 사용할 폰트. 비워두면 프리팹/씬에 지정된 원본 폰트를 그대로 씁니다.")]
        public TMP_FontAsset fontAsset;
    }

    // //내부 의존성
    [Tooltip("언어별 교체 폰트 목록입니다. 목록에 없는 언어는 원본 폰트를 그대로 씁니다.")]
    [SerializeField] private FontEntry[] entries = new FontEntry[0];

    [Tooltip("언어와 무관하게 교체하지 않을 폰트입니다. 숫자 전용 폰트처럼 글자 모양 자체가 " +
        "기획 의도인 경우에 등록하세요.")]
    [SerializeField] private TMP_FontAsset[] excludedFonts = new TMP_FontAsset[0];

    [Tooltip("런타임에 새로 생성된 텍스트도 자동으로 찾아서 교체합니다. 끄면 씬 로드 시점과 " +
        "언어 변경 시점에 존재하는 텍스트만 교체됩니다.")]
    [SerializeField] private bool trackRuntimeCreatedText = true;

    public bool TrackRuntimeCreatedText => trackRuntimeCreatedText;

    /// <summary>
    /// 해당 언어에서 사용할 폰트입니다. null이면 원본 폰트를 유지하라는 뜻입니다.
    /// </summary>
    public TMP_FontAsset GetFont(Language _language)
    {
        if (null == entries) return null;

        for (int i = 0; i < entries.Length; i++)
        {
            // Language는 enum이므로 직접 비교한다. (박싱 없음)
            if (entries[i].language == _language) return entries[i].fontAsset;
        }
        return null;
    }

    /// <summary>교체 대상에서 제외된 폰트인지 여부입니다.</summary>
    public bool IsExcluded(TMP_FontAsset _fontAsset)
    {
        if (null == _fontAsset || null == excludedFonts) return false;

        for (int i = 0; i < excludedFonts.Length; i++)
        {
            if (excludedFonts[i] == _fontAsset) return true;
        }
        return false;
    }
}
