using UnityEngine;

public enum SceneType
{
    MainMenu,
    Gameplay
}

public class SceneManager : MonoBehaviour
{
    public void ChangeScene(SceneType sceneType)
    {
        switch (sceneType)
        {
            case SceneType.MainMenu:
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
                break;
            case SceneType.Gameplay:
                UnityEngine.SceneManagement.SceneManager.LoadScene("GameplayScene");
                break;
        }
    }
}
