using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Demonstrations;

[RequireComponent(typeof(Rigidbody))]
public class ShipAgent : Agent
{
    [Header("References")]
    public EnvController env;
    public Transform goal;
    private Crest.Examples.BoatAlignNormal boat;

    [Header("Movement")]
    public float maxSpeed = 18f;
    public float forceScale = 50f; // Fallback only
    public float torqueScale = 300f; // Fallback only

    [Header("Sensing")]
    public int rays = 9;
    public float rayAngleSpan = 120f;
    public float rayLength = 60f;
    public LayerMask obstacleMask;

    [Header("Rewards")]
    public float stepPenalty = -0.001f;
    public float progressRewardScale = 1.0f;
    public float collisionPenalty = -0.5f;
    public float reachGoalReward = 10f;
    public int maxStepsPerEpisode = 2000;

    [Header("Behavior Cloning")]
    public bool enableBehaviorCloning = true;
    public float demonstrationRewardScale = 1.0f;
    public float humanInputThreshold = 0.1f; // Minimum input to count as demonstration
    
    [Header("Debug")]
    public bool logActions = true;
    public bool testMovementOnStart = false;
    public bool showHumanInputGUI = true;

    private Rigidbody rb;
    private float prevDistToGoal;
    private float currentThrottle = 0f;
    private float currentSteer = 0f;
    private bool isInitialized = false;
    
    // Behavior cloning variables
    private bool isReceivingHumanInput = false;
    private float lastHumanThrottle = 0f;
    private float lastHumanSteer = 0f;
    private int demonstrationSteps = 0;

    public override void Initialize()
    {
        try
        {
            Debug.Log($"[ShipAgent] Initialize called");
            
            // Get components with null checks
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                Debug.LogError("[ShipAgent] Rigidbody component is missing!");
                return;
            }

            boat = GetComponent<Crest.Examples.BoatAlignNormal>();
            if (boat == null)
            {
                Debug.LogError("[ShipAgent] BoatAlignNormal component is missing!");
                return;
            }

            Debug.Log("[ShipAgent] BoatAlignNormal component found - configuring for agent control");
            
            // Configure the boat for agent control
            boat.useAgentControls = true;
            boat._playerControlled = false;
            
            // Reset control values
            boat.AgentThrottle = 0f;
            boat.AgentSteer = 0f;
            
            rb.maxAngularVelocity = 10f;
            isInitialized = true;
            
            Debug.Log($"[ShipAgent] Boat configured successfully: useAgentControls={boat.useAgentControls}, _playerControlled={boat._playerControlled}");
            Debug.Log($"[ShipAgent] Expected observation size = {5 + rays}");
            
            if (enableBehaviorCloning)
            {
                Debug.Log("[ShipAgent] Behavior cloning enabled - Use WASD/Arrow keys to demonstrate!");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ShipAgent] Error during initialization: {e.Message}");
            isInitialized = false;
        }
    }

    public override void OnEpisodeBegin()
    {
        if (!isInitialized)
        {
            Debug.LogError("[ShipAgent] Agent not properly initialized, skipping episode begin");
            return;
        }

        Debug.Log("[ShipAgent] === EPISODE BEGIN ===");
        
        try
        {
            // Reset physics
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            
            currentThrottle = 0f;
            currentSteer = 0f;
            
            // Reset boat control inputs
            if (boat != null)
            {
                boat.AgentThrottle = 0f;
                boat.AgentSteer = 0f;
            }
            
            if (env != null) 
            {
                env.ResetEnvironment(this);
            }
            else
            {
                Debug.LogWarning("[ShipAgent] EnvController is null!");
            }
            
            if (goal == null && env != null) goal = env.goal;
            
            if (goal == null)
            {
                Debug.LogError("[ShipAgent] Goal is null! Agent won't work properly.");
            }
            
            prevDistToGoal = DistanceToGoal();
            Debug.Log($"[ShipAgent] Episode begin: pos={transform.position}, goal distance={prevDistToGoal:F2}");

            if (testMovementOnStart)
            {
                Invoke("TestMovementDelayed", 1f);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ShipAgent] Error during episode begin: {e.Message}");
        }
    }

    void TestMovementDelayed()
    {
        Debug.Log("[ShipAgent] Testing movement with fixed values...");
        if (boat != null)
        {
            boat.AgentThrottle = 0.8f;
            boat.AgentSteer = 0.3f;
            Debug.Log($"[ShipAgent] Set test values: throttle=0.8, steer=0.3");
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (!isInitialized || rb == null)
        {
            // Add safe default observations
            for (int i = 0; i < 5 + rays; i++)
            {
                sensor.AddObservation(0f);
            }
            return;
        }

        try
        {
            if (goal == null)
            {
                // Add default observations if goal is missing
                sensor.AddObservation(0f); // toGoalLocal.x
                sensor.AddObservation(1f); // toGoalLocal.z (forward)
                sensor.AddObservation(1f); // distance (max distance)
                sensor.AddObservation(0f); // velocity.x
                sensor.AddObservation(0f); // velocity.z
            }
            else
            {
                Vector3 toGoal = (goal.position - transform.position).normalized;
                Vector3 toGoalLocal = transform.InverseTransformDirection(toGoal);
                sensor.AddObservation(toGoalLocal.x);
                sensor.AddObservation(toGoalLocal.z);

                float dist = Mathf.Clamp01(Vector3.Distance(transform.position, goal.position) / 1000f);
                sensor.AddObservation(dist);
            }

            Vector3 velLocal = transform.InverseTransformDirection(rb.velocity);
            sensor.AddObservation(Mathf.Clamp(velLocal.x / maxSpeed, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(velLocal.z / maxSpeed, -1f, 1f));

            // Raycast observations
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
            
            if (StepCount % 200 == 0) // Reduce logging frequency
            {
                Debug.Log($"[ShipAgent] Observations: dist={DistanceToGoal():F2}, vel=({velLocal.x:F2}, {velLocal.y:F2}, {velLocal.z:F2})");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ShipAgent] Error collecting observations: {e.Message}");
            // Add safe defaults
            for (int i = 0; i < 5 + rays; i++)
            {
                sensor.AddObservation(0f);
            }
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!isInitialized)
        {
            Debug.LogError("[ShipAgent] Agent not initialized, cannot process actions");
            return;
        }
        
        try
        {
            // Validate action buffer
            if (actions.ContinuousActions.Length < 2)
            {
                Debug.LogError("[ShipAgent] Invalid action buffer - expected 2 continuous actions!");
                return;
            }

            float throttle = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
            float steer = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
            
            // Check for human input override for behavior cloning
            if (enableBehaviorCloning)
            {
                float humanThrottle, humanSteer;
                if (GetHumanInput(out humanThrottle, out humanSteer))
                {
                    // Use human input and provide demonstration reward
                    throttle = humanThrottle;
                    steer = humanSteer;
                    isReceivingHumanInput = true;
                    demonstrationSteps++;
                    
                    // Reward for providing good demonstrations
                    AddReward(demonstrationRewardScale * 0.01f);
                    
                    if (StepCount % 50 == 0)
                    {
                        Debug.Log($"[ShipAgent] Using human demonstration: throttle={throttle:F2}, steer={steer:F2}");
                    }
                }
                else
                {
                    isReceivingHumanInput = false;
                }
            }
            
            currentThrottle = throttle;
            currentSteer = steer;

            // Apply to Crest BoatAlignNormal controller
            if (boat != null)
            {
                boat.AgentThrottle = throttle;
                boat.AgentSteer = steer;
                
                if (logActions && StepCount % 50 == 0)
                {
                    string inputSource = isReceivingHumanInput ? "HUMAN" : "AI";
                    Debug.Log($"[ShipAgent] Applied to BoatAlignNormal ({inputSource}): throttle={throttle:F3}, steer={steer:F3}");
                }
            }
            else
            {
                Debug.LogError("[ShipAgent] BoatAlignNormal is null! Cannot control boat.");
                
                // Emergency fallback to direct rigidbody control
                if (rb != null)
                {
                    Vector3 forceDirection = transform.forward * throttle * forceScale;
                    Vector3 torqueDirection = Vector3.up * steer * torqueScale;
                    
                    rb.AddForce(forceDirection, ForceMode.Force);
                    rb.AddTorque(torqueDirection, ForceMode.Force);
                }
            }

            // Speed limiting (let Crest handle most physics, but prevent extreme speeds)
            if (rb != null && rb.velocity.magnitude > maxSpeed * 1.5f)
            {
                rb.velocity = rb.velocity.normalized * maxSpeed * 1.5f;
            }

            // Calculate rewards
            float currentDist = DistanceToGoal();
            float progress = (prevDistToGoal - currentDist);
            
            // Basic step penalty
            AddReward(stepPenalty);
            
            // Progress reward (positive when getting closer)
            if (Mathf.Abs(progress) > 0.1f)
            {
                float progressReward = progress * progressRewardScale;
                AddReward(progressReward);
                
                // Extra reward for progress during human demonstration
                if (isReceivingHumanInput)
                {
                    AddReward(progressReward * 0.5f); // Bonus for good human demonstration
                }
            }
            
            // Speed reward to encourage movement
            float speed = rb.velocity.magnitude;
            if (speed > 1f)
            {
                AddReward(0.001f * speed);
            }
            
            prevDistToGoal = currentDist;

            if (StepCount % 100 == 0)
            {
                string demoInfo = enableBehaviorCloning ? $", DemoSteps={demonstrationSteps}" : "";
                Debug.Log($"[ShipAgent] Step {StepCount}: Distance={currentDist:F1}, Speed={speed:F1}, Progress={progress:F3}, TotalReward={GetCumulativeReward():F2}{demoInfo}");
            }

            // Check bounds
            if (env != null && env.killZone != null)
            {
                float maxRange = env.killRadius;
                if ((transform.position - env.killZone.position).sqrMagnitude > maxRange * maxRange)
                {
                    Debug.Log("[ShipAgent] Out of bounds, ending episode");
                    AddReward(-1f);
                    EndEpisode();
                    return;
                }
            }

            // Check max steps
            if (StepCount >= maxStepsPerEpisode)
            {
                Debug.Log($"[ShipAgent] Max steps ({maxStepsPerEpisode}) reached, ending episode");
                EndEpisode();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ShipAgent] Error processing actions: {e.Message}");
        }
    }

    /// <summary>
    /// Captures human input for behavior cloning demonstrations
    /// </summary>
    private bool GetHumanInput(out float throttle, out float steer)
    {
        throttle = 0f;
        steer = 0f;
        
        // Capture keyboard input
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) throttle += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) throttle -= 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) steer -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) steer += 1f;
        
        // Check if input is significant enough to count as demonstration
        bool hasInput = Mathf.Abs(throttle) > humanInputThreshold || Mathf.Abs(steer) > humanInputThreshold;
        
        if (hasInput)
        {
            lastHumanThrottle = throttle;
            lastHumanSteer = steer;
        }
        
        return hasInput;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        
        // Use the same human input method for consistency
        float throttle, steer;
        GetHumanInput(out throttle, out steer);
        
        continuousActionsOut[0] = throttle;
        continuousActionsOut[1] = steer;
        
        if (throttle != 0f || steer != 0f)
        {
            Debug.Log($"[ShipAgent] Heuristic input: throttle={throttle:F2}, steer={steer:F2}");
        }
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
        Debug.Log("[ShipAgent] 🎯 Goal reached! Adding reward = " + reachGoalReward);
        AddReward(reachGoalReward);
        EndEpisode();
    }

    public void PenalizeCollision()
    {
        Debug.Log("[ShipAgent] 💥 Collision! Adding penalty = " + collisionPenalty);
        AddReward(collisionPenalty);
    }

    // Helper method to test movement manually
    [ContextMenu("Test Forward Movement")]
    public void TestForwardMovement()
    {
        if (boat != null)
        {
            boat.AgentThrottle = 1f;
            boat.AgentSteer = 0f;
            Debug.Log("[ShipAgent] Applied test forward movement to BoatAlignNormal");
        }
        else
        {
            Debug.LogError("[ShipAgent] BoatAlignNormal is null!");
        }
    }

    [ContextMenu("Test Steering")]
    public void TestSteering()
    {
        if (boat != null)
        {
            boat.AgentThrottle = 0.5f;
            boat.AgentSteer = 1f;
            Debug.Log("[ShipAgent] Applied test steering to BoatAlignNormal");
        }
        else
        {
            Debug.LogError("[ShipAgent] BoatAlignNormal is null!");
        }
    }

    [ContextMenu("Reset Boat Controls")]
    public void ResetBoatControls()
    {
        if (boat != null)
        {
            boat.AgentThrottle = 0f;
            boat.AgentSteer = 0f;
            Debug.Log("[ShipAgent] Reset boat controls to zero");
        }
    }

    void OnGUI()
    {
        if (logActions && isInitialized)
        {
            GUI.Label(new Rect(10, 10, 300, 20), $"Agent Throttle: {currentThrottle:F2}");
            GUI.Label(new Rect(10, 30, 300, 20), $"Agent Steer: {currentSteer:F2}");
            GUI.Label(new Rect(10, 50, 300, 20), $"Velocity: {(rb != null ? rb.velocity.magnitude : 0):F2}");
            GUI.Label(new Rect(10, 70, 300, 20), $"Distance to Goal: {DistanceToGoal():F2}");
            
            if (enableBehaviorCloning && showHumanInputGUI)
            {
                if (isReceivingHumanInput)
                {
                    GUI.color = Color.green;
                    GUI.Label(new Rect(10, 90, 300, 20), "🎮 HUMAN CONTROL ACTIVE");
                    GUI.Label(new Rect(10, 110, 300, 20), $"Demo Steps: {demonstrationSteps}");
                }
                else
                {
                    GUI.color = Color.yellow;
                    GUI.Label(new Rect(10, 90, 300, 20), "🤖 AI CONTROL - Use WASD to demonstrate");
                }
                GUI.color = Color.white;
                GUI.Label(new Rect(10, 130, 300, 20), "W/S: Throttle, A/D: Steer");
            }
            
            if (boat != null)
            {
                GUI.Label(new Rect(10, 150, 300, 20), $"Boat Throttle: {boat.AgentThrottle:F2}");
                GUI.Label(new Rect(10, 170, 300, 20), $"Boat Steer: {boat.AgentSteer:F2}");
                GUI.Label(new Rect(10, 190, 300, 20), $"Use Agent Controls: {boat.useAgentControls}");
            }
            
            GUI.Label(new Rect(10, 210, 300, 20), $"Initialized: {isInitialized}");
        }
    }
}