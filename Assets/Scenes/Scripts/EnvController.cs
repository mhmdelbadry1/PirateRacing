using UnityEngine;

public class EnvController : MonoBehaviour
{
    public Transform startAreaCenter;
    public float startRadius = 30f;
    public Transform goal;
    public Transform killZone;
    public float killRadius = 3000f;

    public ObstacleSpawner spawner;
    public int startSpawnCount = 20;
    public bool clearBeforeSpawn = true;

    public bool snapToSeaLevel = true;
    public float yOffset = 0f;

    public void ResetEnvironment(ShipAgent agent)
    {
        Vector2 rand = Random.insideUnitCircle * startRadius;
        Vector3 start = startAreaCenter != null ? startAreaCenter.position : transform.position;
        Vector3 pos = new Vector3(start.x + rand.x, start.y, start.z + rand.y);

        if (snapToSeaLevel && Crest.OceanRenderer.Instance != null)
            pos.y = Crest.OceanRenderer.Instance.SeaLevel + yOffset;

        agent.transform.position = pos;

        if (goal != null)
        {
            Vector3 dir = goal.position - agent.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.1f)
            {
                float noise = Random.Range(-25f, 25f);
                agent.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, noise, 0f);
            }
        }

        if (spawner != null)
        {
            if (clearBeforeSpawn) spawner.ClearSpawned();
            spawner.spawnCenter = agent.transform;
            spawner.spawnCount = startSpawnCount;
            spawner.SpawnObstaclesAhead();
        }
    }
}
