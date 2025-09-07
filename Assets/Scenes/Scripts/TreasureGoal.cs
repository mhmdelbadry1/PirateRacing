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
            // Check if this is ML-Agents training mode
            // First try the collider itself, then parent, then children
            var agent = other.GetComponent<ShipAgent>();
            if (agent == null)
                agent = other.GetComponentInParent<ShipAgent>();
            if (agent == null)
                agent = other.GetComponentInChildren<ShipAgent>();
                
            if (agent != null)
            {
                finished = true; // Prevent multiple triggers
                Debug.Log($"🤖 Agent reached the goal at {currentTime:F2} seconds! Rewarding and restarting episode.");
                agent.RewardReachGoal(); // This will reward the agent and restart the episode automatically
                return; // Don't run the finish game sequence for ML-Agents training
            }
            
            // Fallback for regular gameplay (no ML-Agents training)
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
