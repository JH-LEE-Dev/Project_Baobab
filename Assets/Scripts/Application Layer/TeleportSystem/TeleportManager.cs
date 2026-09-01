using UnityEngine;

public class TeleportManager : MonoBehaviour
{
    //외부 의존성
    private SignalHub signalHub;
    private IBootStrapProvider bootStrapProvider;
    private InputManager inputManager;

    public void Initialize(SignalHub _signalHub, IBootStrapProvider _bootstrapProvider, InputManager _inputManager)
    {
        signalHub = _signalHub;
        bootStrapProvider = _bootstrapProvider;
        inputManager = _inputManager;

        SubscribeSignals();
    }

    public void Release()
    {
        UnSubscribeSignals();
    }

    private void SubscribeSignals()
    {
        signalHub.Subscribe<GoToDungeonSignal>(GoToDungeon);
        signalHub.Subscribe<GoToHomeSignal>(GoToHome);
        signalHub.Subscribe<GoToMainMenuSignal>(GoToMainMenu);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<GoToDungeonSignal>(GoToDungeon);
        signalHub.UnSubscribe<GoToHomeSignal>(GoToHome);
        signalHub.UnSubscribe<GoToMainMenuSignal>(GoToMainMenu);
    }

    private void GoToDungeon(GoToDungeonSignal goToDungeonSignal)
    {
        inputManager.PauseMove(true);

        // 예전에는 (MapType, ForestType)을 이중 switch로 훑어 짝이 맞는 조합에서만 전환을 요청했는데,
        // 어느 쪽에도 default가 없어서 조합이 어긋나면 아무 일도 일어나지 않았다. 그런데 위에서
        // PauseMove(true)는 이미 걸린 뒤라, 씬 전환도 없고 조작도 안 되는 상태로 굳어버린다(로그도 없음).
        //
        // 12개 분기가 전부 "신호로 받은 값을 그대로 GoToOtherScene에 넘긴다"였을 뿐 실질은 조합 검증이었으므로,
        // 검증(IsForestTypeOf)과 요청을 분리하고 검증 실패를 명시적으로 처리한다.
        if (false == IsForestTypeOf(goToDungeonSignal.type, goToDungeonSignal.forestType))
        {
            // 걸어둔 이동 잠금을 반드시 되돌린다. 씬 전환이 일어나지 않으므로 이 잠금을 풀어줄
            // 다음 단계가 아예 없어서, 그대로 두면 플레이어가 영영 움직이지 못한다.
            inputManager.PauseMove(false);

            // Debug.LogError는 Sentry가 스택과 함께 자동 수집한다.
            Debug.LogError($"[TeleportManager] 던전 전환 요청의 조합이 올바르지 않습니다. " +
                $"type={goToDungeonSignal.type}, forestType={goToDungeonSignal.forestType}. 전환을 취소합니다.");
            return;
        }

        bootStrapProvider.GoToOtherScene(goToDungeonSignal.type, goToDungeonSignal.forestType);
    }

    /// <summary>
    /// 해당 ForestType이 그 MapType에 속하는 하위 지역인지 확인합니다.
    /// (기존 이중 switch가 하던 조합 검증과 동일한 판정입니다)
    /// </summary>
    private static bool IsForestTypeOf(MapType _mapType, ForestType _forestType)
    {
        switch (_mapType)
        {
            case MapType.WideGreenForest:
                return ForestType.WideGreenForest_1 == _forestType
                    || ForestType.WideGreenForest_2 == _forestType
                    || ForestType.WideGreenForest_3 == _forestType;

            case MapType.FluffySporeForest:
                return ForestType.FluffySporeForest_1 == _forestType
                    || ForestType.FluffySporeForest_2 == _forestType
                    || ForestType.FluffySporeForest_3 == _forestType;

            case MapType.StarrootForest:
                return ForestType.StarrootForest_1 == _forestType
                    || ForestType.StarrootForest_2 == _forestType
                    || ForestType.StarrootForest_3 == _forestType;

            case MapType.MagmaForest:
                return ForestType.MagmaForest_1 == _forestType
                    || ForestType.MagmaForest_2 == _forestType
                    || ForestType.MagmaForest_3 == _forestType;

            default:
                return false;
        }
    }

    private void GoToHome(GoToHomeSignal goToHomeSignal)
    {
        inputManager.PauseMove(true);

        // 던전 → 마을 귀환은 새 게임도 이어하기도 아니다. 예전엔 GoToTownScene(true)로 요청해서
        // BootStrap의 bNewGame이 "새 게임"으로 남았다(지금은 읽히지 않아 무해했지만 오해의 소지가 크다).
        // 전용 경로로 요청해 bNewGame을 건드리지 않는다. (BootStrap.ReturnToTownScene 주석 참고)
        bootStrapProvider.ReturnToTownScene();
    }

    private void GoToMainMenu(GoToMainMenuSignal goToMainMenuSignal)
    {
        bootStrapProvider.GoToMainMenuScene();
    }
}
