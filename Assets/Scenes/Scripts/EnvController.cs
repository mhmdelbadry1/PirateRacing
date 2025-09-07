using UnityEngine;

public class EnvController : MonoBehaviour
{
    public Transform startAreaCenter;
    public float startRadius = 30f;
    public Transform goal;
    public Transform killZone;
    public float killRadius = 300f; // Adjusted for complex environment
    public LayerMask obstacleMask = ~0; // Layer mask for obstacles
    public float clearRadius = 7f; // Increased for safer spawning
    public float spawnOffsetMin = 10f; // Min distance from last position
    public float spawnOffsetMax = 20f; // Max distance from last position

    public ObstacleSpawner spawner;
    public int startSpawnCount = 2;
    public bool clearBeforeSpawn = true;

    public bool snapToSeaLevel = true;
    public float yOffset = 0f;

    public void ResetEnvironment(ShipAgent agent)
    {
        Debug.Log("[EnvController] ResetEnvironment START");

        Vector3 spawnPos;
        bool useInitialSpawn = agent.LastPosition == Vector3.zero || Vector3.Distance(agent.LastPosition, goal.position) > killRadius || !agent.GoalReached;

        if (useInitialSpawn)
        {
            // Use initial spawn area if goal not reached or position invalid
            Vector2 rand = Random.insideUnitCircle * startRadius;
            Vector3 start = startAreaCenter != null ? startAreaCenter.position : transform.position;
            spawnPos = new Vector3(start.x + rand.x, start.y, start.z + rand.y);
        }
        else
        {
            // Spawn near last position, closer to goal
            Vector3 dirToGoal = (goal.position - agent.LastPosition).normalized;
            dirToGoal.y = 0f;
            float offset = Random.Range(spawnOffsetMin, spawnOffsetMax);
            spawnPos = agent.LastPosition + dirToGoal * offset;

            // Ensure spawn position is clear of obstacles
            int maxAttempts = 10;
            for (int i = 0; i < maxAttempts; i++)
            {
                if (!Physics.CheckSphere(spawnPos, clearRadius, obstacleMask))
                {
                    break; // Clear position found
                }
                // Try a new position within spawnOffsetMin/Max
                Vector2 randOffset = Random.insideUnitCircle * (spawnOffsetMax - spawnOffsetMin);
                spawnPos = agent.LastPosition + dirToGoal * offset + new Vector3(randOffset.x, 0f, randOffset.y);
                if (i == maxAttempts - 1)
                {
                    Debug.LogWarning("[EnvController] Could not find clear spawn position, using last attempt");
                }
            }
        }

        if (snapToSeaLevel && Crest.OceanRenderer.Instance != null)
            spawnPos.y = Crest.OceanRenderer.Instance.SeaLevel + yOffset;

        agent.transform.position = spawnPos;

        if (goal != null)
        {
            Vector3 dir = goal.position - agent.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.1f)
            {
                float noise = Random.Range(-26f, 26f);
                agent.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, noise, 0f);
            }
        }

        if (spawner != null)
        {
            if (clearBeforeSpawn) spawner.ClearSpawned();
            spawner.spawnCenter = agent.transform;
            spawner.SpawnObstaclesAhead();
        }
        else
        {
            Debug.Log("[EnvController] spawner is null, quick reset.");
        }

        Debug.Log($"[EnvController] ResetEnvironment END. Spawned at: {spawnPos}, Distance to goal: {Vector3.Distance(spawnPos, goal.position):F2}m, InitialSpawn: {useInitialSpawn}");
    }
}