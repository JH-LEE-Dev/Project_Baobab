using UnityEngine;
using System;

public class TentManager : MonoBehaviour
{
    public event Action<bool> TentInteractEvent;
    public event Action<bool> TentInteractStateChangedEvent;

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

        tent.TentInteractStateChangedEvent -= TentInteractStateChanged;
        tent.TentInteractStateChangedEvent += TentInteractStateChanged;
    }

    private void ReleaseEvents()
    {
        tent.TentInteractEvent -= TentInteract;
    }

    private void TentInteract(bool _bInteract)
    {
        TentInteractEvent?.Invoke(_bInteract);
    }

    private void TentInteractStateChanged(bool _boolean)
    {
        TentInteractStateChangedEvent?.Invoke(_boolean);
    }

    public void DisableTent()
    {
        tent.gameObject.SetActive(false);
    }

    public void EnableTent()
    {
        tent.gameObject.SetActive(true);
    }
}
