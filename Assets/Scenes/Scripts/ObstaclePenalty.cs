using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ObstaclePenalty : MonoBehaviour
{
    public float slowFactor = 0.6f;

    private void OnCollisionEnter(Collision collision)
    {
        var agent = collision.collider.GetComponent<ShipAgent>();
        if (agent != null)
        {
            agent.PenalizeCollision();
            var rb = agent.GetComponent<Rigidbody>();
            if (rb != null && rb.velocity.sqrMagnitude > 0.01f) rb.velocity *= slowFactor;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        var agent = other.GetComponent<ShipAgent>();
        if (agent != null)
        {
            agent.PenalizeCollision();
            var rb = agent.GetComponent<Rigidbody>();
            if (rb != null && rb.velocity.sqrMagnitude > 0.01f) rb.velocity *= slowFactor;
        }
    }
}
