using UnityEngine;

public enum MapType
{
    None,
    Town,
    Forest1_1,
    Forest1_2,
    Forest1_3,
}

public class SceneManager : MonoBehaviour
{
    public AsyncOperation ChangeSceneAsync(SceneType sceneType)
    {
        string sceneName = "";
        switch (sceneType)
        {
            case SceneType.MainMenu:
                sceneName = "MainMenuScene";
                break;
            case SceneType.Town:
                sceneName = "TownScene";
                break;
            case SceneType.DungeonScene:
                sceneName = "DungeonScene";
                break;
        }

        if (string.IsNullOrEmpty(sceneName)) return null;

        return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
    }

    // 기존 동기 메서드도 유지 (필요한 경우)
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
            case SceneType.DungeonScene:
                UnityEngine.SceneManagement.SceneManager.LoadScene("DungeonScene");
                break;
        }
    }
}
