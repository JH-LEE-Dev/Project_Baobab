using UnityEngine;

public class InputManager : MonoBehaviour
{
    public InputReader inputReader { get; private set; }

    private bool bCursorHoveredOnUI = false;

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

    public void Pause(bool _bPause)
    {
        inputReader.Pause(_bPause);
    }

    public void SetCursorHoveredOnUI(bool _bCursorHoveredOnUI)
    {
        bCursorHoveredOnUI = _bCursorHoveredOnUI;
    }

    public bool IsCursorHoveredOnUI()
    {
        return bCursorHoveredOnUI;
    }
}
