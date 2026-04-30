using UnityEngine;

public class TeleportManager : MonoBehaviour
{
    //외부 의존성
    private SignalHub signalHub;
    private IBootStrapProvider bootStrapProvider;

    public void Initialize(SignalHub _signalHub, IBootStrapProvider _bootstrapProvider)
    {
        signalHub = _signalHub;
        bootStrapProvider = _bootstrapProvider;

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
        switch (goToDungeonSignal.type)
        {
            case MapType.Forest1_1:
                bootStrapProvider.GoToOtherScene("DungeonScene", MapType.Forest1_1);
                break;
            case MapType.Forest1_2:
                bootStrapProvider.GoToOtherScene("DungeonScene", MapType.Forest1_2);
                break;
            case MapType.Forest1_3:
                bootStrapProvider.GoToOtherScene("DungeonScene", MapType.Forest1_3);
                break;
        }
    }

    private void GoToHome(GoToHomeSignal goToHomeSignal)
    {
        bootStrapProvider.GoToTownScene(true);
    }
}
