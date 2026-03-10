using UnityEngine;
using System;

public class TriggerProxy : MonoBehaviour
{
    public event Action<Collider2D> OnTriggerEnterEvent;
    public event Action<Collider2D> OnTriggerExitEvent;

    private void OnTriggerEnter2D(Collider2D _other)
    {
        OnTriggerEnterEvent?.Invoke(_other);
    }

    private void OnTriggerExit2D(Collider2D _other)
    {
        OnTriggerExitEvent?.Invoke(_other);
    }
}
