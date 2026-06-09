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
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<GoToDungeonSignal>(GoToDungeon);
        signalHub.UnSubscribe<GoToHomeSignal>(GoToHome);
    }

    private void GoToDungeon(GoToDungeonSignal goToDungeonSignal)
    {
        inputManager.PauseMove(true);
        switch (goToDungeonSignal.type)
        {
            case MapType.VegetatedForest:
                switch (goToDungeonSignal.forestType)
                {
                    case ForestType.VegetatedForest_1:
                        bootStrapProvider.GoToOtherScene(MapType.VegetatedForest, ForestType.VegetatedForest_1);
                        break;
                    case ForestType.VegetatedForest_2:
                        bootStrapProvider.GoToOtherScene(MapType.VegetatedForest, ForestType.VegetatedForest_2);
                        break;
                    case ForestType.VegetatedForest_3:
                        bootStrapProvider.GoToOtherScene(MapType.VegetatedForest, ForestType.VegetatedForest_3);
                        break;
                }
                break;
        }
    }

    private void GoToHome(GoToHomeSignal goToHomeSignal)
    {
        inputManager.PauseMove(true);
        bootStrapProvider.GoToTownScene(true);
    }
}
