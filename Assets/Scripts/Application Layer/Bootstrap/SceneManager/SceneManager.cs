using UnityEngine;
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
            case SceneType.Dungeon:
                UnityEngine.SceneManagement.SceneManager.LoadScene("DungeonScene");
                break;
        }
    }
}
