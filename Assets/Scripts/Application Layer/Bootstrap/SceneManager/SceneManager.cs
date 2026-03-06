using UnityEngine;

public enum SceneType
{
    None,
    MainMenu,
    Town,
    Forest,
    MAX,
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
            case SceneType.Town:
                UnityEngine.SceneManagement.SceneManager.LoadScene("TownScene");
                break;
            case SceneType.Forest:
                UnityEngine.SceneManagement.SceneManager.LoadScene("ForestScene");
                break;
        }
    }
}
