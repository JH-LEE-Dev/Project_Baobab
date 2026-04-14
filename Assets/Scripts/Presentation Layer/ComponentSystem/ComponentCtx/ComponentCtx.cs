using UnityEngine;

public class ComponentCtx
{
    public InputManager inputManager;
    public Vector2 moveInput;
    public StatComponent characterStat;
    public bool bWhileChangingWeapon = false;

    public void Initialize(InputManager _inputManager, StatComponent _characterStat)
    {
        inputManager = _inputManager;
        characterStat = _characterStat;
    }
}
