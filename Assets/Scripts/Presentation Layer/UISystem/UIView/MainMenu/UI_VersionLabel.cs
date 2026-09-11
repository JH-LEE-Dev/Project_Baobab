using TMPro;
using UnityEngine;

/// <summary>
/// 메인 메뉴 구석의 버전 표기를 채웁니다.
///
/// [왜 스크립트로 채우는가]
/// 원래는 TextMeshPro 컴포넌트에 "Demo v1.0.0" 이 <b>문자열로 박혀 있었습니다.</b>
/// PlayerSettings 의 bundleVersion 과 아무 연결이 없어서, 버전을 올려도 화면은 그대로였습니다.
/// 실제로 1.0.1 패치를 굽고 나서야 화면이 1.0.0 인 것을 발견했습니다.
///
/// 이 값은 유저가 버그를 제보할 때 "어느 빌드인지"를 알려주는 유일한 단서입니다. 틀린 값이
/// 박혀 있으면 없느니만 못합니다 - 제보자와 개발자가 서로 다른 빌드를 이야기하게 됩니다.
/// 그래서 화면에 찍히는 값을 빌드가 실제로 들고 있는 값에서 뽑습니다.
///
/// Application.version 은 PlayerSettings.bundleVersion 그대로이며, 에디터에서도 같은 값이
/// 나옵니다. 따로 동기화할 것이 없습니다.
///
/// [데모 표기]
/// 데모/정식은 BuildInfo 가 디파인으로 가릅니다. 여기서 문자열을 따로 두면 디파인과 어긋날 수
/// 있으므로 BuildInfo 를 그대로 씁니다.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class UI_VersionLabel : MonoBehaviour
{
    /// <summary>데모 빌드에서 버전 앞에 붙는 말입니다.</summary>
    private const string DEMO_PREFIX = "Demo ";

    private TMP_Text label;

    private void Awake()
    {
        Apply();
    }

    /// <summary>
    /// 에디터에서 값을 만지거나 프리팹을 열었을 때도 실제로 나갈 문자열이 보이도록 합니다.
    /// 이게 없으면 프리팹에 남은 옛 문자열을 보고 "아직 안 고쳐졌다"고 오해하게 됩니다.
    /// </summary>
    private void OnValidate()
    {
        Apply();
    }

    private void Apply()
    {
        if (null == label) label = GetComponent<TMP_Text>();

        if (null == label) return;

        string _prefix = (true == BuildInfo.IsDemo) ? DEMO_PREFIX : string.Empty;

        label.text = _prefix + "v" + Application.version;
    }
}
