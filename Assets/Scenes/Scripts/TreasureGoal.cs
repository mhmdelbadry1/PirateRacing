using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class TreasureGoal : MonoBehaviour
{
    [Header("Tags")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string agentTag = "Agent";

    [Header("UI Elements")]
    [SerializeField] private GameObject playerWinUI;
    [SerializeField] private GameObject agentWinUI;

    [Header("Sounds")]
    [SerializeField] private AudioSource winSfx;
    [SerializeField] private AudioSource loseSfx;

    [Header("Settings")]
    [SerializeField] private float delayBeforeEnd = 3f;
    [SerializeField] private string nextSceneName = "";

    private bool finished = false;

    private void OnTriggerEnter(Collider other)

    
    {

            Debug.Log($"Something entered: {other.name}, Tag = {other.tag}");

        if (finished) return;

        float currentTime = Time.timeSinceLevelLoad;

        if (other.CompareTag(playerTag))
        {
            finished = true;
            Debug.Log($"🏆 Player reached the goal at {currentTime:F2} seconds!");
            StartCoroutine(FinishGame(true));
        }
        else if (other.CompareTag(agentTag))
        {
            finished = true;
            Debug.Log($"🤖 Agent reached the goal first at {currentTime:F2} seconds!");
            StartCoroutine(FinishGame(false));
        }
    }

    private IEnumerator FinishGame(bool playerWon)
    {
        if (playerWon)
        {
            if (winSfx) winSfx.Play();
            if (playerWinUI) playerWinUI.SetActive(true);
        }
        else
        {
            if (loseSfx) loseSfx.Play();
            if (agentWinUI) agentWinUI.SetActive(true);
        }

        if (delayBeforeEnd > 0f)
            yield return new WaitForSecondsRealtime(delayBeforeEnd);

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Application.Quit();
        }
    }
}
