#if UNITY_EDITOR || DEVELOPMENT_BUILD
using PresentationLayer.Environment;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// [임시] 전리품 성장 UI 인 게임 확인용 연결기. 실제 연결(R1~R3, Docs/LootGrowthUI_Guide.md)이 들어오면 이 Temp 폴더째 삭제한다.
/// - 전리품 화면(UIView_ScreenModal)에 성장 정보 원천을 대신 연결한다(현재 단계 = 디버그 단계).
/// - F6: 디버그 단계를 1 -> 2 -> 3 -> 1로 순환하고, 마을 기둥에 SetGrowthTier를 호출해 아우라 색을 바꾼다.
/// 에디터와 개발 빌드에서만 컴파일된다. 세이브/프레스티지 값은 건드리지 않는다.
/// </summary>
public class LootGrowthDebugConnector : MonoBehaviour
{
    // 끄려면 false (실제 연결이 들어온 뒤 겹쳐 덮어쓰지 않게)
    private static readonly bool Enabled = true;

    private const float FindInterval = 1f;

    // 다음 단계가 되는 프레스티지 레벨(InDungeonObjectManager의 현재 인스펙터 값과 같은 표). 확인용 복사본이다.
    private static readonly int[] LostAndFoundBoxLevels = { 4, 7 };
    private static readonly int[] SporePotionLevels = { 8, 10 };
    private static readonly int[] StarCompassLevels = { 9, 11 };

    private int debugTier = 1;
    private float nextFindTime = 0f;
    private UIView_ScreenModal connectedModal;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (false == Enabled)
        {
            return;
        }

        GameObject _go = new GameObject("LootGrowthDebugConnector");
        DontDestroyOnLoad(_go);
        _go.AddComponent<LootGrowthDebugConnector>();
    }

    private static bool IsGrowthLoot(LootType _type)
    {
        return LootType.LostAndFoundBox == _type || LootType.SporePotion == _type || LootType.StarCompass == _type;
    }

    private int GetTier(LootType _type)
    {
        return true == IsGrowthLoot(_type) ? debugTier : 0;
    }

    private int GetNextLevel(LootType _type)
    {
        if (false == IsGrowthLoot(_type) || 3 <= debugTier)
        {
            return 0;
        }

        int[] _levels = LootType.LostAndFoundBox == _type ? LostAndFoundBoxLevels
            : (LootType.SporePotion == _type ? SporePotionLevels : StarCompassLevels);

        // 1단계면 2단계가 되는 레벨(_levels[0]), 2단계면 3단계가 되는 레벨(_levels[1])
        return _levels[debugTier - 1];
    }

    private void ApplyTierToPillars()
    {
        LootDisplayObject[] _pillars = FindObjectsByType<LootDisplayObject>();

        for (int i = 0; i < _pillars.Length; i++)
        {
            if (true == IsGrowthLoot(_pillars[i].CurrentLootType))
            {
                _pillars[i].SetGrowthTier(debugTier);
            }
        }
    }

    private void Update()
    {
        float _now = Time.unscaledTime;

        // 전리품 화면 연결(씬이 바뀌면 새 인스턴스에 다시 연결)
        if (_now >= nextFindTime)
        {
            nextFindTime = _now + FindInterval;
            UIView_ScreenModal _modal = FindAnyObjectByType<UIView_ScreenModal>(FindObjectsInactive.Include);

            if (null != _modal && _modal != connectedModal)
            {
                _modal.SetGrowthInfoSource(GetTier, GetNextLevel);
                connectedModal = _modal;
                Debug.Log("[LootGrowthDebug] 전리품 화면에 성장 정보 원천을 연결했습니다(F6: 단계 순환)");
            }

            // 기둥은 마을 씬을 새로 불러올 때마다 새로 생기므로 주기적으로 현재 단계를 다시 적용한다
            ApplyTierToPillars();
        }

        Keyboard _keyboard = Keyboard.current;

        if (null != _keyboard && true == _keyboard.f6Key.wasPressedThisFrame)
        {
            debugTier = debugTier % 3 + 1;
            ApplyTierToPillars();
            Debug.Log("[LootGrowthDebug] 디버그 단계 = " + debugTier);
        }
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(8f, 8f, 360f, 22f), "[LootGrowthDebug] tier=" + debugTier + "  (F6: 단계 변경)");
    }
}
#endif
