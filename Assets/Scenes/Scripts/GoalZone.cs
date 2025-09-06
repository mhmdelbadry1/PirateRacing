using UnityEngine;

public class GoalZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        var agent = other.GetComponent<ShipAgent>();
        if (agent != null)
        {
            agent.RewardReachGoal();
        }
    }
}
