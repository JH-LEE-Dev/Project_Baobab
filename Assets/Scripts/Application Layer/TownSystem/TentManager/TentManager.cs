using UnityEngine;
using System;

public class TentManager : MonoBehaviour
{
    public event Action<bool> TentInteractEvent;

    private InputManager inputManager;

    [SerializeField] private Tent tentObj;
    [SerializeField] private Transform tentSpawnPoint;
    private Tent tent;

    public void Initialize(InputManager _inputManager)
    {
        inputManager = _inputManager;

        tent = Instantiate(tentObj, tentSpawnPoint.position, tentSpawnPoint.rotation, transform);
        tent.Initialize(inputManager);

        BindEvents();
    }

    public void Release()
    {
        ReleaseEvents();
        tent.Release();
    }

    private void BindEvents()
    {
        tent.TentInteractEvent -= TentInteract;
        tent.TentInteractEvent += TentInteract;
    }

    private void ReleaseEvents()
    {
        tent.TentInteractEvent -= TentInteract;
    }

    private void TentInteract(bool _bInteract)
    {
        TentInteractEvent?.Invoke(_bInteract);
    }
}
