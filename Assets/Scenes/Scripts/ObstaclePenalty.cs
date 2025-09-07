using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ObstaclePenalty : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        var agent = collision.collider.GetComponent<ShipAgent>();
        if (agent != null)
        {
            agent.PenalizeCollision();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        var agent = other.GetComponent<ShipAgent>();
        if (agent != null)
        {
            agent.PenalizeCollision();
        }
    }
}