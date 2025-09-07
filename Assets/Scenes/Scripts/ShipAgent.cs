using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;
using System.IO;
using System.Linq;

public class ShipAgent : Agent
{
    [Header("References")]
    public Crest.Examples.BoatAlignNormal boat; // Movement provider
    public EnvController env;
    public Transform goal;

    [Header("Training")]
    public float stepPenalty = -0.0001f;         // Reduced for complex environment
    public float lowSpeedPenalty = -0.015f;      // Increased to encourage higher speed
    public float lowSpeedThreshold = 7.0f;       // Increased to target ~10 m/s
    public float collisionPenalty = -0.3f;       // Reduced to allow recovery
    public bool endEpisodeOnCollision = false;   // Allow recovery after collision
    public float reachedGoalReward = 2.0f;       // Strong goal incentive
    public bool endEpisodeOnGoal = true;         // End episode when reaching goal
    public float progressRewardScale = 0.02f;    // Increased for longer paths
    public int maxStepsPerEpisode = 5000;        // End episode after 5000 steps
    public float goalNotReachedPenalty = -2.0f;  // Large penalty for not reaching goal

    [Header("Avoidance Rewards")]
    public float proximityPenaltyThreshold = 0.2f;  // Normalized ray distance below which to penalize
    public float proximityPenalty = -0.002f;        // Reduced to balance with speed rewards
    public float safeSpeedRewardThreshold = 0.5f;   // Normalized ray distance above which to reward speed
    public float safeSpeedRewardScale = 0.002f;     // Increased to encourage ~10 m/s when safe

    [Header("Recovery")]
    public bool enableRecovery = true;           // Enable recovery after collisions
    public float recoveryDuration = 1.5f;        // Increased for complex obstacle navigation
    public float recoveryBackThrottle = -0.8f;   // Stronger reverse for dense obstacles
    public float recoverySteerStrength = 1.0f;   // Stronger steering to avoid obstacles

    [Header("Sensing")]
    public LayerMask raycastMask = ~0;           // Everything by default
    public float rayLength = 30f;                // Increased for longer-range detection
    public int raysPerSide = 4;                  // Increased for dense obstacles
    public float rayFanAngle = 60f;              // Wider angle for better coverage

    [Header("Motion Fallback")]
    [Tooltip("Apply forward throttle bias at start of training.")]
    public bool forceForwardBias = true;
    [Range(0f, 1f)] public float forwardBias = 0.15f; // Reduced for more agent control

    [Header("Transform Sync")]
    public bool syncAgentToBoatTransform = true;

    [Header("Demonstration Recording")]
    public bool recordDemonstrations = false;    // Disabled due to ML-Agents 2.0.1 limitations
    public string demoDirectory = "D:/Unity/Projects/Pirates/Demonstrations"; // Path outside Assets
    public string demoName = "ShipBCDemo";       // Base name for .demo files

    Rigidbody _rb;
    float _prevGoalDist;
    float _recoveryUntil = -1f;
    float _recoverySteerSign = 0f;
    float _lastCollisionTime = -1f;
    int _collisionCount = 0;                     // Track collisions per episode
    int _stepCount = 0;                          // Track steps per episode
    float _minRayDistance = 1f;                  // Minimum ray distance from last observation
    bool _goalReached = false;                   // Track if goal was reached in episode
    Vector3 _lastPosition;                       // Store last position for next spawn

    public float CurrentSpeed => _rb != null ? _rb.velocity.magnitude : 0f;
    public float DistanceToGoal => (goal != null ? Vector3.Distance(transform.position, goal.position) : 0f);
    public float LastThrottle { get; private set; }
    public float LastSteer { get; private set; }
    public Vector3 LastPosition => _lastPosition; // Expose last position for EnvController
    public bool GoalReached => _goalReached;     // Expose goal status for EnvController

    void Awake()
    {
        if (boat == null) boat = GetComponent<Crest.Examples.BoatAlignNormal>();
        if (env == null) env = FindFirstObjectByType<EnvController>();
        if (_rb == null) _rb = GetComponent<Rigidbody>();
        if (boat != null)
        {
            var brb = boat.GetComponent<Rigidbody>();
            if (brb != null) _rb = brb;
        }
        if (goal == null && env != null) goal = env.goal;
    }

    void Start()
    {
        if (boat != null) boat.useAgentControls = true;
        if (boat != null && forceForwardBias) boat._throttleBias = forwardBias;

        if (recordDemonstrations)
        {
            Debug.LogWarning("[ShipAgent] DemonstrationRecorder is not available in ML-Agents 2.0.1. Please upgrade to 2.3.0 or higher to enable recording.");
            recordDemonstrations = false;
        }
    }

    public override void OnEpisodeBegin()
    {
        if (_rb == null) _rb = GetComponent<Rigidbody>();
        if (_rb != null)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        if (env != null)
        {
            env.ResetEnvironment(this);
        }

        if (boat != null)
        {
            boat.transform.position = transform.position;
            boat.transform.rotation = transform.rotation;
            var brb = boat.GetComponent<Rigidbody>();
            if (brb != null)
            {
                brb.velocity = Vector3.zero;
                brb.angularVelocity = Vector3.zero;
            }
        }

        _prevGoalDist = goal != null ? Vector3.Distance(transform.position, goal.position) : 0f;
        _recoveryUntil = -1f;
        _lastCollisionTime = -1f;
        _collisionCount = 0;
        _stepCount = 0;
        _minRayDistance = 1f;
        _goalReached = false;

        Debug.Log($"[ShipAgent] Episode started. Initial distance to goal: {_prevGoalDist:F2}m");
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 fwd = transform.forward;
        Vector3 vel = _rb != null ? _rb.velocity : Vector3.zero;
        Vector3 toGoal = goal != null ? (goal.position - transform.position) : Vector3.zero;
        toGoal.y = 0f;
        float dist = toGoal.magnitude;
        Vector3 dirToGoal = dist > 1e-3f ? toGoal / dist : Vector3.zero;

        float cos = Vector3.Dot(fwd, dirToGoal);
        float sin = Vector3.Cross(fwd, dirToGoal).y;

        sensor.AddObservation(cos);                 // 1
        sensor.AddObservation(sin);                 // 2
        sensor.AddObservation(Mathf.Clamp(dist / 500f, 0f, 1f)); // 3
        sensor.AddObservation(transform.InverseTransformDirection(vel) / 20f); // 6
        sensor.AddObservation(CurrentSpeed / 20f); // 7: Normalized speed
        sensor.AddObservation(_collisionCount / 10f); // 8: Normalized collision count

        List<float> rayHits = new List<float>();
        rayHits.Add(CastRay(transform.forward));
        for (int i = 1; i <= raysPerSide; i++)
        {
            float a = (rayFanAngle / raysPerSide) * i;
            rayHits.Add(CastRay(Quaternion.Euler(0f, -a, 0f) * transform.forward));
            rayHits.Add(CastRay(Quaternion.Euler(0f, +a, 0f) * transform.forward));
        }
        foreach (var v in rayHits) sensor.AddObservation(v); // 9 raycasts (1 + 4*2)

        _minRayDistance = rayHits.Min(); // Store min ray distance for rewards

        Debug.Log($"[ShipAgent] Observations: cos={cos:F2}, sin={sin:F2}, dist={dist:F2}m, vel={vel.magnitude:F2}m/s, collisions={_collisionCount}, min_ray={_minRayDistance:F2}, rays={string.Join(",", rayHits)}");
    }

    float CastRay(Vector3 dir)
    {
        if (Physics.Raycast(transform.position + Vector3.up * 1f, dir, out RaycastHit hit, rayLength, raycastMask))
        {
            return hit.distance / rayLength; // 0..1
        }
        return 1f;
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        _stepCount++;
        _lastPosition = transform.position; // Store position for next spawn

        float throttle = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float steer = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        LastThrottle = throttle;
        LastSteer = steer;

        bool inRecovery = enableRecovery && (Time.time < _recoveryUntil);
        if (inRecovery)
        {
            throttle = recoveryBackThrottle;
            steer = _recoverySteerSign * recoverySteerStrength;
            if (boat != null) boat._throttleBias = 0f;
        }
        else if (boat != null && forceForwardBias)
        {
            boat._throttleBias = forwardBias;
        }

        if (boat != null)
        {
            boat.AgentThrottle = throttle;
            boat.AgentSteer = steer;
        }

        AddReward(stepPenalty);

        float currentSpeed = CurrentSpeed;
        if (currentSpeed < lowSpeedThreshold)
        {
            AddReward(lowSpeedPenalty);
            Debug.Log($"[ShipAgent] Low speed penalty applied: {lowSpeedPenalty:F4}, Speed: {currentSpeed:F2}m/s");
        }

        // Proximity penalty for being too close to obstacles
        if (_minRayDistance < proximityPenaltyThreshold)
        {
            AddReward(proximityPenalty);
            Debug.Log($"[ShipAgent] Proximity penalty applied: {proximityPenalty:F4}, Min Ray Distance: {_minRayDistance:F2}");
        }

        // Reward for maintaining speed when safe
        if (_minRayDistance > safeSpeedRewardThreshold)
        {
            AddReward(currentSpeed * safeSpeedRewardScale);
            Debug.Log($"[ShipAgent] Safe speed reward: {currentSpeed * safeSpeedRewardScale:F4}, Speed: {currentSpeed:F2}m/s, Min Ray Distance: {_minRayDistance:F2}");
        }

        if (goal != null)
        {
            float dist = Vector3.Distance(transform.position, goal.position);
            float progress = _prevGoalDist - dist;
            if (progress > 0)
            {
                AddReward(progress * progressRewardScale);
                Debug.Log($"[ShipAgent] Progress reward: {progress * progressRewardScale:F4}, Distance: {dist:F2}m");
            }
            _prevGoalDist = dist;
        }

        if (env != null && env.killZone != null && env.killRadius > 0f)
        {
            float distToKill = Vector3.Distance(transform.position, env.killZone.position);
            if (distToKill > env.killRadius)
            {
                AddReward(-1f);
                Debug.Log($"[ShipAgent] Exceeded kill radius: {distToKill:F2}m, Ending episode");
                HandleEpisodeEnd();
            }
        }

        if (_collisionCount >= 5)
        {
            AddReward(-1f);
            Debug.Log($"[ShipAgent] Too many collisions ({_collisionCount}), Ending episode");
            HandleEpisodeEnd();
        }

        if (_stepCount >= maxStepsPerEpisode)
        {
            Debug.Log($"[ShipAgent] Reached {maxStepsPerEpisode} steps, Ending episode");
            HandleEpisodeEnd();
        }

        Debug.Log($"[ShipAgent] Step: {_stepCount}, Throttle={throttle:F2}, Steer={steer:F2}, Speed={currentSpeed:F2}m/s, CumReward={GetCumulativeReward():F4}");
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var c = actionsOut.ContinuousActions;
        float throttle = 0f;
        float steer = 0f;

        if (Input.GetKey(KeyCode.W)) throttle += 1f;
        if (Input.GetKey(KeyCode.S)) throttle -= 1f;
        float reverseMult = throttle < 0f ? -1f : 1f;
        if (Input.GetKey(KeyCode.A)) steer -= 1f * reverseMult;
        if (Input.GetKey(KeyCode.D)) steer += 1f * reverseMult;

        c[0] = Mathf.Clamp(throttle, -1f, 1f);
        c[1] = Mathf.Clamp(steer, -1f, 1f);
        LastThrottle = c[0];
        LastSteer = c[1];

        if (recordDemonstrations)
        {
            Debug.Log($"[ShipAgent] Heuristic action: Throttle={c[0]:F2}, Steer={c[1]:F2}");
        }
    }

    void FixedUpdate()
    {
        RequestDecision();
    }

    void LateUpdate()
    {
        if (syncAgentToBoatTransform && boat != null && boat.transform != transform)
        {
            transform.SetPositionAndRotation(boat.transform.position, boat.transform.rotation);
        }
    }

    void HandleEpisodeEnd()
    {
        if (!_goalReached)
        {
            AddReward(goalNotReachedPenalty);
            Debug.Log($"[ShipAgent] Episode ended without reaching goal! Penalty: {goalNotReachedPenalty:F4}, CumReward={GetCumulativeReward():F4}");
        }

        if (recordDemonstrations)
        {
            Debug.Log($"[ShipAgent] Episode ended. Demonstration would be saved to: {Path.Combine(demoDirectory, demoName)}.demo (disabled due to ML-Agents 2.0.1)");
        }
        EndEpisode();
    }

    public void RewardReachGoal()
    {
        _goalReached = true;
        AddReward(reachedGoalReward);
        Debug.Log($"[ShipAgent] Reached goal! Reward: {reachedGoalReward:F4}, CumReward={GetCumulativeReward():F4}, Steps: {_stepCount}");
        if (endEpisodeOnGoal) HandleEpisodeEnd();
    }

    public void RewardHeart(int heartValue)
    {
        AddReward(heartValue * 0.01f);
        Debug.Log($"[ShipAgent] Heart collected! Value: {heartValue}, Reward: {heartValue * 0.01f:F4}, CumReward={GetCumulativeReward():F4}");
    }

    public void PenalizeCollision()
    {
        _collisionCount++;
        AddReward(collisionPenalty);
        Debug.Log($"[ShipAgent] Collision! Penalty: {collisionPenalty:F4}, Collision Count: {_collisionCount}, CumReward={GetCumulativeReward():F4}, Time since last collision: {Time.time - _lastCollisionTime:F2}s");
        _lastCollisionTime = Time.time;

        if (enableRecovery)
        {
            BeginRecovery(-transform.forward);
        }

        if (endEpisodeOnCollision)
        {
            Debug.Log("[ShipAgent] Ending episode due to collision");
            HandleEpisodeEnd();
        }
    }

    void BeginRecovery(Vector3 awayDirectionWorld)
    {
        Vector3 away = awayDirectionWorld;
        away.y = 0f;
        if (away.sqrMagnitude < 1e-4f) away = -transform.forward;
        away.Normalize();
        float ang = Vector3.SignedAngle(transform.forward, away, Vector3.up);
        _recoverySteerSign = ang >= 0f ? -1f : 1f;
        _recoveryUntil = Time.time + recoveryDuration;
        Debug.Log($"[ShipAgent] Starting recovery: Reverse throttle={recoveryBackThrottle:F2}, Steer={_recoverySteerSign * recoverySteerStrength:F2}, Duration={recoveryDuration:F2}s");
    }
}