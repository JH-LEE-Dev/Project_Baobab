using UnityEngine;

public class ComponentCtx
{
    public InputManager inputManager;
    public Vector2 moveInput;
    public StatComponent characterStat;
    public bool bWhileChangingWeapon = false;
    public IPathfindGridProvider pathfindGridProvider;
    public ITilemapDataProvider tilemapDataProvider;

    public void Initialize(InputManager _inputManager, StatComponent _characterStat, IPathfindGridProvider _pathfindGridProvider,
     ITilemapDataProvider _tilemapDataProvider)
    {
        inputManager = _inputManager;
        characterStat = _characterStat;
        pathfindGridProvider = _pathfindGridProvider;
        tilemapDataProvider = _tilemapDataProvider;
    }
}
