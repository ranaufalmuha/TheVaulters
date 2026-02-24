using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string playSceneName = "SampleScene";

    public void PlayGame()
    {
        if (!Application.CanStreamedLevelBeLoaded(playSceneName))
        {
            Debug.LogError("Play scene not found in Build Settings: " + playSceneName);
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(playSceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
