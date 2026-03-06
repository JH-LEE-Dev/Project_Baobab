using System;
using UnityEngine;

public class TownObjectManager : MonoBehaviour
{
    //이벤트
    public event Action<PortalType> PortalActivatedEvent;

    //외부 의존성
    private IEnvironmentProvider environmentProvider;

    //내부 의존성
    [Header("Portal")]
    [SerializeField] private PortalObj portalPrefab;
    [SerializeField] private Transform portalSpawnPoint;

    //내부 의존성
    private PortalObj portal;

    public void Initialize(IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;
    }

    public void ReadyObj()
    {
        portal = Instantiate(portalPrefab);
        portal.transform.position = portalSpawnPoint.position;

        portal.Initialize(PortalType.ToDungeonPortal);

        BindEvents();
    }

    private void BindEvents()
    {
        if (portal == null)
            return;
        
        portal.PortalActivated -= PortalActivated;
        portal.PortalActivated += PortalActivated;
    }

    private void ReleaseEvents()
    {
        portal.PortalActivated -= PortalActivated;
    }

    private void OnDestroy()
    {
        ReleaseEvents();
    }

    private void PortalActivated(PortalType _type)
    {
        PortalActivatedEvent?.Invoke(_type);
    }
}
