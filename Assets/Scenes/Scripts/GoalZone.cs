using UnityEngine;

public class GoalZone : MonoBehaviour
{
    [Header("Debug")]
    public bool debugMode = false;
    
    private void OnTriggerEnter(Collider other)
    {
        var agent = other.GetComponent<ShipAgent>();
        if (agent != null)
        {
            if (debugMode)
                Debug.Log($"[GoalZone] Ship reached goal! Speed: {agent.CurrentSpeed:F2} m/s, Distance: {agent.DistanceToGoal:F2} m");
            
            agent.RewardReachGoal();
        }
    }
    
    private void OnTriggerStay(Collider other)
    {
        // Small bonus for staying in goal area
        var agent = other.GetComponent<ShipAgent>();
        if (agent != null)
        {
            agent.AddReward(0.001f);
        }
    }
}
