using UnityEngine;

public class GameSystem
{
    private InDungeonSystem inDungeonSystem;
    private TownSystem townSystem;

    public void Initialize(InDungeonSystem _inDungeonSystem, TownSystem _townSystem)
    {
        inDungeonSystem = _inDungeonSystem;
        townSystem = _townSystem;

        BindEvents();
    }

    public void Release()
    {
        ReleaseEvents();
    }

    public void BindEvents()
    {
        townSystem.ActivatePortalEvent -= inDungeonSystem.ActivatePortal;
        townSystem.ActivatePortalEvent += inDungeonSystem.ActivatePortal;

        inDungeonSystem.ActivatePortalEvent -= townSystem.ActivatePortal;
        inDungeonSystem.ActivatePortalEvent += townSystem.ActivatePortal;
    }

    public void ReleaseEvents()
    {
        townSystem.ActivatePortalEvent -= inDungeonSystem.ActivatePortal;
        inDungeonSystem.ActivatePortalEvent -= townSystem.ActivatePortal;
    }
}
