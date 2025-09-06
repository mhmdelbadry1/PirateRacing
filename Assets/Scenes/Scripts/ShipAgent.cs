using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

[RequireComponent(typeof(Rigidbody))]
public class ShipAgent : Agent
{
    [Header("References")]
    public EnvController env;
    public Transform goal;
    private Crest.Examples.BoatAlignNormal boat; // مرجع للـ Crest boat

    [Header("Movement")]
    public float maxSpeed = 18f;

    [Header("Sensing")]
    public int rays = 9;
    public float rayAngleSpan = 120f;
    public float rayLength = 60f;
    public LayerMask obstacleMask;

    [Header("Rewards")]
    public float stepPenalty = -0.0005f;
    public float progressRewardScale = 0.01f;
    public float collisionPenalty = -0.5f;
    public float reachGoalReward = 2f;
    public int maxStepsPerEpisode = 4000;

    Rigidbody rb;
    float prevDistToGoal;

    public override void Initialize()
    {
        Debug.Log($"[ShipAgent] Expected obs size = {5 + rays}");
        rb = GetComponent<Rigidbody>();
        boat = GetComponent<Crest.Examples.BoatAlignNormal>();
        rb.maxAngularVelocity = 10f;
    }

    public override void OnEpisodeBegin()
    {
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        if (env != null) env.ResetEnvironment(this);
        if (goal == null && env != null) goal = env.goal;
        prevDistToGoal = DistanceToGoal();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 toGoal = (goal.position - transform.position).normalized;
        Vector3 toGoalLocal = transform.InverseTransformDirection(toGoal);
        sensor.AddObservation(toGoalLocal.x);
        sensor.AddObservation(toGoalLocal.z);

        float dist = Mathf.Clamp01(Vector3.Distance(transform.position, goal.position) / 1000f);
        sensor.AddObservation(dist);

        Vector3 velLocal = transform.InverseTransformDirection(rb.velocity);
        sensor.AddObservation(velLocal.x);
        sensor.AddObservation(velLocal.z);

        float half = rayAngleSpan * 0.5f;
        for (int i = 0; i < rays; i++)
        {
            float t = (rays == 1) ? 0.5f : (float)i / (rays - 1);
            float ang = Mathf.Lerp(-half, half, t);
            Vector3 dir = Quaternion.Euler(0f, ang, 0f) * transform.forward;
            float hitNorm = 1f;
            if (Physics.Raycast(transform.position + Vector3.up * 1f, dir, out RaycastHit hit, rayLength, obstacleMask))
                hitNorm = hit.distance / rayLength;
            sensor.AddObservation(hitNorm);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float throttle = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float steer = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

        if (boat != null)
        {
            boat.AgentThrottle = throttle;
            boat.AgentSteer = steer;
        }

        if (StepCount % 50 == 0)
        {
            Debug.Log($"[ShipAgent] Step {StepCount}: throttle={throttle:F2}, steer={steer:F2}, pos={transform.position}");
        }   

        if (rb.velocity.magnitude > maxSpeed)
            rb.velocity = rb.velocity.normalized * maxSpeed;

        AddReward(stepPenalty);
        float d = DistanceToGoal();
        float progress = (prevDistToGoal - d);
        AddReward(progress * progressRewardScale);
        prevDistToGoal = d;

        if (env != null && env.killZone != null)
        {
            float maxRange = env.killRadius;
            if ((transform.position - env.killZone.position).sqrMagnitude > maxRange * maxRange)
            {
                AddReward(-1f);
                EndEpisode();
            }
        }

        if (StepCount >= maxStepsPerEpisode)
            EndEpisode();
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var cont = actionsOut.ContinuousActions;
        float throttle = 0f; float steer = 0f;
        if (Input.GetKey(KeyCode.W)) throttle += 1f;
        if (Input.GetKey(KeyCode.S)) throttle -= 1f;
        if (Input.GetKey(KeyCode.A)) steer -= 1f;
        if (Input.GetKey(KeyCode.D)) steer += 1f;
        cont[0] = throttle;
        cont[1] = steer;
    }

    float DistanceToGoal()
    {
        if (goal == null) return 9999f;
        Vector3 a = transform.position; a.y = 0f;
        Vector3 b = goal.position; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    public void RewardReachGoal()
    {
        AddReward(reachGoalReward);
        EndEpisode();
    }

    public void PenalizeCollision()
    {
        AddReward(collisionPenalty);
    }
}
