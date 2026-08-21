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

    /// <summary>
    /// 텐트(집)의 스폰 위치. TownUnitSpawner가 운반 NPC를 집 주변에 배치할 때 기준점으로 사용한다.
    /// </summary>
    public Transform TentSpawnPoint => tentSpawnPoint;

    /// <summary>
    /// 실제로 생성된 텐트(집) 오브젝트의 트랜스폼. 튜토리얼 퀘스트 인디케이터가 이 위에 화살표를 띄운다.
    /// Initialize() 전에는 null이다.
    /// </summary>
    public Transform TentTransform => null != tent ? tent.transform : null;

    public Tent Tent => tent;

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
