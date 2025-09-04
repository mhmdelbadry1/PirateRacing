using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void PlayGame()
    {
        SceneManager.LoadScene("PirateRacing"); // Change "GameScene" to your scene name
    }

    public void QuitGame()
    {
        Debug.Log("Game Quit!");
        Application.Quit();
    }
}
