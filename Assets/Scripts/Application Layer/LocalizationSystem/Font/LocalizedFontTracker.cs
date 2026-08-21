using TMPro;
using UnityEngine;

/// <summary>
/// 폰트가 교체되기 전의 "원본" 폰트와 머티리얼을 텍스트 오브젝트 자신이 들고 있게 합니다.
/// <see cref="FontLocalizer"/>가 처음 해당 텍스트를 만났을 때 런타임으로 붙입니다.
///
/// 원본을 정적 Dictionary가 아니라 컴포넌트에 보관하는 이유:
/// 파괴된 오브젝트를 키로 들고 있으면 관리 객체가 계속 살아남아 누수가 되고,
/// 오브젝트 풀에서 재사용될 때 자기 원본을 그대로 따라다니게 하는 편이 안전하기 때문입니다.
/// </summary>
[DisallowMultipleComponent]
public class LocalizedFontTracker : MonoBehaviour
{
    // //내부 의존성
    private TMP_Text targetText;
    private TMP_FontAsset originalFont;
    private Material originalMaterial;
    private bool hasCaptured = false;

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
        // 풀에서 꺼내 재사용되는 경우처럼 비활성 구간을 지나 다시 켜질 때를 위한 보정이다.
        // 이미 현재 언어에 맞는 폰트라면 FontLocalizer가 곧바로 빠져나가므로 비용은 참조 비교 한 번이다.
        FontLocalizer.Apply(targetText);
    }
}
