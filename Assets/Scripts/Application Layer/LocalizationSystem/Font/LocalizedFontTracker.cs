using TMPro;
using UnityEngine;

/// <summary>
/// 폰트가 교체되기 전의 "원본" 폰트와 머티리얼을 텍스트 오브젝트 자신이 들고 있게 합니다.
///
/// 프리팹·씬의 TMP 텍스트에는 <c>Tools ▸ Localization ▸ Font Tracker ▸ 전체 부착</c>으로
/// 미리 붙여둡니다. 미리 붙어 있어야 Instantiate 직후 OnEnable에서 교체가 끝나, 캔버스가
/// 처음 그리기 전에 올바른 폰트가 됩니다. 붙어 있지 않은 텍스트는 <see cref="FontLocalizer"/>가
/// 뒤늦게 발견해 런타임으로 붙이지만, 그 경우 첫 한 프레임은 원본 폰트로 그려집니다.
///
/// 원본을 정적 Dictionary가 아니라 컴포넌트에 보관하는 이유:
/// 파괴된 오브젝트를 키로 들고 있으면 관리 객체가 계속 살아남아 누수가 되고,
/// 오브젝트 풀에서 재사용될 때 자기 원본을 그대로 따라다니게 하는 편이 안전하기 때문입니다.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("")] // 손으로 붙이는 컴포넌트가 아니다. 위 메뉴의 배치 툴로만 부착한다.
public class LocalizedFontTracker : MonoBehaviour
{
    // //내부 의존성
    // [SerializeField]인 이유: Instantiate는 직렬화되는 필드만 복제한다. 프리팹이 아니라
    // "이미 화면에 떠 있어 폰트가 교체된 텍스트 오브젝트"를 복제하면 복제본의 기록이 비게 되고,
    // Awake가 교체된 폰트를 원본으로 다시 기록해버려 한국어로 되돌려도 그 오브젝트만
    // 일본어/중국어 폰트로 남는다. (현재 그런 복제 경로는 없지만 조용히 깨지는 종류라 미리 막는다)
    //
    // [HideInInspector]인 이유: 순수한 런타임 기록이다. 디자이너가 손으로 채우면 "원본"이
    // 실제와 어긋나 잘못된 폰트로 되돌아간다. 값 확인이 필요하면 Debug 인스펙터를 쓴다.
    [HideInInspector, SerializeField] private TMP_Text targetText;
    [HideInInspector, SerializeField] private TMP_FontAsset originalFont;
    [HideInInspector, SerializeField] private Material originalMaterial;
    [HideInInspector, SerializeField] private bool hasCaptured = false;

    public TMP_Text TargetText => targetText;
    public TMP_FontAsset OriginalFont => originalFont;
    public Material OriginalMaterial => originalMaterial;
    public bool HasCaptured => hasCaptured;

    // //퍼블릭 초기화 및 제어 메서드
    /// <summary>
    /// 아직 원본을 기록하지 않았다면 지금 기록합니다.
    ///
    /// 비활성 오브젝트에 AddComponent하면 Awake가 즉시 호출되지 않으므로,
    /// FontLocalizer가 교체를 시작하기 전에 이 메서드를 직접 한 번 더 불러
    /// "교체 전 상태"가 반드시 원본으로 남도록 보장합니다.
    /// </summary>
    public void EnsureCaptured()
    {
        if (true == hasCaptured) return;

        if (null == targetText)
        {
            targetText = GetComponent<TMP_Text>();
        }
        if (null == targetText) return;

        originalFont = targetText.font;
        originalMaterial = targetText.fontSharedMaterial;
        hasCaptured = true;
    }

    // //유니티 이벤트 함수
    private void Awake()
    {
        EnsureCaptured();
    }

    private void OnEnable()
    {
        // 런타임에 Instantiate된 UI가 올바른 폰트로 첫 프레임을 그리게 하는 지점이 바로 여기다.
        // OnEnable은 캔버스 리빌드보다 먼저 돌기 때문에, 잘못된 폰트가 한 번도 그려지지 않는다.
        // (풀에서 꺼내 재사용되는 경우도 같은 경로로 보정된다)
        // 이미 현재 언어에 맞는 폰트라면 FontLocalizer가 곧바로 빠져나가므로 비용은 참조 비교 한 번이다.
        FontLocalizer.Apply(targetText);
    }
}
