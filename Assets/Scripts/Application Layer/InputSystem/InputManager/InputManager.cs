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

    public void PauseMove(bool _bPause)
    {
        inputReader.PauseMove(_bPause);
    }

    public void SetCursorHoveredOnUI(bool _bCursorHoveredOnUI)
    {
        bCursorHoveredOnUI = _bCursorHoveredOnUI;
    }

    public bool IsCursorHoveredOnUI()
    {
        return bCursorHoveredOnUI;
    }

    public void PauseMouse(bool _boolean)
    {
        inputReader.PauseMouse(_boolean);
    }
}
