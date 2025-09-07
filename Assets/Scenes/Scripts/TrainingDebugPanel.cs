using UnityEngine;
using TMPro;

public class TrainingDebugPanel : MonoBehaviour
{
    [Header("References")]
    public ShipAgent agent;
    public TextMeshProUGUI text;

    [Header("Options")]
    public bool showWhenNotPlaying = true;
    public int fpsSmoothing = 20;

    float _fps;

    void Awake()
    {
        if (agent == null) agent = FindFirstObjectByType<ShipAgent>();
        if (text == null) text = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        if (text == null) return;
        if (agent == null)
        {
            text.text = "Training Debug: No ShipAgent found";
            return;
        }

        if (!Application.isPlaying && !showWhenNotPlaying)
        {
            text.text = "";
            return;
        }

        // FPS smoothing
        float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
        _fps = Mathf.Lerp(_fps, 1f / dt, 1f / Mathf.Max(1, fpsSmoothing));

        var boat = agent.boat;
        string boatState = boat != null ? (boat.InWater ? "InWater" : "Air") : "NoBoat";

        text.text =
            $"Ship Training Debug\n" +
            $"FPS: {_fps:0} | Time: {Time.timeSinceLevelLoad:0.0}s\n" +
            $"Speed: {agent.CurrentSpeed:0.0} m/s | Dist→Goal: {agent.DistanceToGoal:0.0} m\n" +
            $"CumReward: {agent.GetCumulativeReward():0.000}\n" +
            $"Boat: {boatState}";
    }
}