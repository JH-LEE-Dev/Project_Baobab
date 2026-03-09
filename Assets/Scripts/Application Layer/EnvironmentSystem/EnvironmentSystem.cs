using System.Collections.Generic;
using UnityEngine;

public class EnvironmentSystem : MonoBehaviour, IEnvironmentProvider
{
    //제공 인터페이스
    public IShadowDataProvider shadowDataProvider => isometricShadowController;
    public IGroundDataProvider groundDataProvider => groundDataManager;

    public ITilemapDataProvider tilemapDataProvider => tileMapGenerator;

    //외부 의존성
    private SignalHub signalHub;

    //내부 의존성
    private TileMapGenerator tileMapGenerator;
    private IsometricShadowController isometricShadowController;
    private TimeController timeController;
    private GroundDataManager groundDataManager;

    public void Initialize(SignalHub _signalHub)
    {
        signalHub = _signalHub;

        tileMapGenerator = GetComponentInChildren<TileMapGenerator>();

        isometricShadowController = GetComponentInChildren<IsometricShadowController>();
        timeController = GetComponentInChildren<TimeController>();
        groundDataManager = GetComponentInChildren<GroundDataManager>();

        if (timeController != null)
            timeController.Initialize();

        if (isometricShadowController != null)
            isometricShadowController.Initialize(timeController);

        if (groundDataManager != null)
            groundDataManager.Initialize();

        BindEvents();
        SubscribeSignals();
    }

    public void Release()
    {
        ReleaseEvents();
        UnSubscribeSignals();
    }

    private void SubscribeSignals()
    {
        signalHub.Subscribe<DungeonReadySignal>(DungeonStarted);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<DungeonReadySignal>(DungeonStarted);
    }

    private void BindEvents()
    {
        tileMapGenerator.TilemapGeneratedEvent -= TilemapGenerated;
        tileMapGenerator.TilemapGeneratedEvent += TilemapGenerated;
    }

    private void ReleaseEvents()
    {
        tileMapGenerator.TilemapGeneratedEvent -= TilemapGenerated;
    }

    private void DungeonStarted(DungeonReadySignal dungeonStartSignal)
    {
        tileMapGenerator.InitializeMapData();
        tileMapGenerator.GenerateMap();
    }

    private void TilemapGenerated(List<Vector3> tilePositions)
    {
        signalHub.Publish(new MapGeneratedSignal(tilePositions));
    }
}
