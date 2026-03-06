using System;
using UnityEngine;

public class ObjectManager : MonoBehaviour
{
    //이벤트
    public event Action<PortalType> PortalActivatedEvent;

    //외부 의존성
    private IEnvironmentProvider environmentProvider;

    //내부 의존성
    [SerializeField] private TownObjectManager townObjManagerPrefab;
    [SerializeField] private ForestObjectManager forestObjManagerPrefab;

    private TownObjectManager townObjManager;
    private ForestObjectManager forestObjManager;

    public delegate void ReadyObjHandler(SceneType _sceneType);
    private ReadyObjHandler[] readyObjCreatorMap;

    public void Initialize(IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;

        readyObjCreatorMap = new ReadyObjHandler[(int)SceneType.MAX];

        BindLogic(SceneType.Town, ReadyTownObjManager);
        BindLogic(SceneType.Forest, ReadyForestObjManager);

        void BindLogic(SceneType _type, ReadyObjHandler _action)
            => readyObjCreatorMap[(int)_type] = _action;
        
        ReadyTownObjManager(SceneType.Town);
    }
    
    public void SetupObj(SceneType _type)
    {
        readyObjCreatorMap[(int)_type]?.Invoke(_type);
    }

    private void ReadyTownObjManager(SceneType _sceneType)
    {
        townObjManager = Instantiate(townObjManagerPrefab);

        townObjManager.ReadyObj();

        townObjManager.PortalActivatedEvent -=  PortalActivated;
        townObjManager.PortalActivatedEvent +=  PortalActivated;
    }

    private void ReadyForestObjManager(SceneType _sceneType)
    {
        forestObjManager = Instantiate(forestObjManagerPrefab);
        forestObjManager.Initialize(environmentProvider);

        forestObjManager.ReadyObj();
        
        //forestObjManager.PortalActivatedEvent -=  PortalActivated;
        //forestObjManager.PortalActivatedEvent +=  PortalActivated;
    }

    private void PortalActivated(PortalType _type)
    {
        PortalActivatedEvent?.Invoke(_type);
    }
}
