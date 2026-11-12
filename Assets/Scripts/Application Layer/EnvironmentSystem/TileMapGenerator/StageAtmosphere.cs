using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 스테이지(마을 포함) 전용 색보정 볼륨을 그 씬에 하나 띄운다. 렌더링만 바꾸고 게임 동작에는 영향이 없다.
/// 해당 씬에 붙여 두므로 씬이 내려가면 같이 사라지고, 다시 켤 때는 이전 것을 지우고 새로 만든다.
/// 꺼진 곳에서는 아무것도 만들지 않는다. 로그라이크 지형 전환 때는 볼륨 세기가 화면 전체 연출 세기(StageAmbienceFade.ScreenVisibility)를 따른다.
/// </summary>
public class StageAtmosphere : MonoBehaviour
{
    private static StageAtmosphere current;

    // //내부 의존성
    private Volume volume;
    private float baseWeight;
    // 볼륨 세기에 마지막으로 곱한 연출 세기(본편은 늘 1이라 세기를 다시 쓰지 않는다)
    private float appliedVisibility = 1f;

    // // 퍼블릭 제어 메서드
    public static void Apply(StageColorGradingSettings _colorGrading, string _sceneName)
    {
        Remove();

        if (null == _colorGrading || false == _colorGrading.enabled || null == _colorGrading.profile)
        {
            return;
        }

        GameObject root = new GameObject("[StageAtmosphere]");
        UnityEngine.SceneManagement.Scene targetScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(_sceneName);
        if (targetScene.IsValid() && targetScene.isLoaded && root.scene != targetScene)
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, targetScene);
        }

        current = root.AddComponent<StageAtmosphere>();
        current.SetupGrading(_colorGrading);
    }

    public static void Remove()
    {
        if (null != current)
        {
            Destroy(current.gameObject);
            current = null;
        }
    }

    private void SetupGrading(StageColorGradingSettings _settings)
    {
        volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        // 기존 전역 볼륨(우선순위 0)보다 위에서 이 프로필에 켜 둔 값만 덮는다. 원본 프로필은 고치지 않는다.
        volume.priority = 10f;
        baseWeight = _settings.weight;
        appliedVisibility = StageAmbienceFade.ScreenVisibility;
        volume.weight = appliedVisibility < 1f ? baseWeight * appliedVisibility : baseWeight;
        volume.sharedProfile = _settings.profile;
    }

    private void Update()
    {
        float visibility = StageAmbienceFade.ScreenVisibility;
        if (null == volume || visibility == appliedVisibility)
        {
            return;
        }

        appliedVisibility = visibility;
        volume.weight = baseWeight * visibility;
    }

    private void OnDestroy()
    {
        if (current == this)
        {
            current = null;
        }
    }
}
