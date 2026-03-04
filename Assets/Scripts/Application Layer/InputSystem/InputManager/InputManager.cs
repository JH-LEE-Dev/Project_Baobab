using UnityEngine;

public class InputManager : MonoBehaviour
{
    public InputReader inputReader { get; private set; }

    public void Initialize()
    {
        inputReader = new InputReader();

        if (inputReader == null)
        {
            Debug.Log("inputReader is null -> InputManager::Initialize");
            return;
        }

        inputReader.Initialize();
    }

    public void Release()
    {
        inputReader.Release();
    }

    public void OnDestroy()
    {
        inputReader?.Release();
    }
}
