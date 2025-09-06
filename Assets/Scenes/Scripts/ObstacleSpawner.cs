using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PrefabSpawnSettings
{
    [Header("Prefab Reference")]
    public GameObject prefab;

    [Header("Placement Settings")]
    public float minRadius = 10f;   // minimum forward offset
    public float maxRadius = 50f;   // maximum forward offset

    [Header("Spawn Options")]
    public bool preserveScale = true;       // keep prefab's original scale
    public bool preserveHeight = false;     // if true, keep prefab's original Y
    public bool followSeaLevel = false;     // if true, object will follow sea level
    public float yOffset = 0f;              // extra vertical offset
}

public class ObstacleSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public List<PrefabSpawnSettings> prefabsWithSettings = new List<PrefabSpawnSettings>();
    public Transform spawnCenter;       // usually the ship
    public int spawnCount = 10;
    public bool spawnOnStart = true;

    [Header("Dynamic Respawn")]
    public float spawnAheadDistance = 200f;
    public float respawnStep = 100f;
    public bool autoClearOld = true;
    public float clearBehindDistance = 150f;

    [Header("Spacing")]
    public float minSpacing = 15f;

    [Header("Final Island Settings")]
    public Transform finalIsland;           // drag the final island here
    public float noSpawnRadius = 300f;      // obstacles won’t spawn inside this radius

    private List<GameObject> spawned = new List<GameObject>();
    private Vector3 lastSpawnPos;

    void Start()
    {
        if (spawnCenter == null) spawnCenter = this.transform;
        lastSpawnPos = spawnCenter.position;
        if (spawnOnStart) SpawnObstaclesAhead();
    }

    void Update()
    {
        if (Vector3.Distance(spawnCenter.position, lastSpawnPos) > respawnStep)
        {
            SpawnObstaclesAhead();
            lastSpawnPos = spawnCenter.position;
        }

        if (autoClearOld) ClearBehindPlayer();
    }

public void SpawnObstaclesAhead()
    {
        if (prefabsWithSettings.Count == 0) return;

        int spawnedThisWave = 0;

        for (int i = 0; i < spawnCount; i++)
        {
            var settings = prefabsWithSettings[Random.Range(0, prefabsWithSettings.Count)];
            if (settings.prefab == null) continue;

            bool foundPos = false;
            Vector3 finalPos = Vector3.zero;
            int attempts = 0;

            while (!foundPos && attempts < 50)
            {
                Vector3 forward = spawnCenter.forward;
                Vector3 side = spawnCenter.right;

                float sideOffset = Random.Range(-settings.maxRadius, settings.maxRadius);
                float forwardOffset = spawnAheadDistance + Random.Range(settings.minRadius, settings.maxRadius);

                Vector3 pos = spawnCenter.position + forward * forwardOffset + side * sideOffset;

                // Y position
                float y;
                if (settings.preserveHeight)
                {
                    y = settings.prefab.transform.position.y;
                }
                else
                {
                    float seaLevel = 0f;
                    if (Crest.OceanRenderer.Instance != null)
                        seaLevel = Crest.OceanRenderer.Instance.SeaLevel;
                    y = seaLevel + settings.yOffset;
                }

                finalPos = new Vector3(pos.x, y, pos.z);

                // --- Check spacing ---
                bool tooClose = false;
                foreach (var obj in spawned)
                {
                    if (Vector3.Distance(obj.transform.position, finalPos) < minSpacing)
                    {
                        tooClose = true;
                        break;
                    }
                }

                // --- Check distance from final island ---
                if (finalIsland != null)
                {
                    if (Vector3.Distance(finalIsland.position, finalPos) < noSpawnRadius)
                    {
                        tooClose = true;
                    }
                }

                if (!tooClose) foundPos = true;
                attempts++;
            }

            if (!foundPos) continue;

            GameObject go = Instantiate(settings.prefab, finalPos, settings.prefab.transform.rotation, transform);

            if (settings.preserveScale)
                go.transform.localScale = settings.prefab.transform.localScale;

            if (settings.followSeaLevel)
                go.AddComponent<SeaLevelFollower>().yOffset = settings.yOffset;

            spawned.Add(go);
            spawnedThisWave++;
        }

        Debug.Log($"[ObstacleSpawner] Spawned {spawnedThisWave} obstacles ahead of ship.");
    }

    void ClearBehindPlayer()
    {
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            if (spawned[i] == null) { spawned.RemoveAt(i); continue; }

            Vector3 toObj = spawned[i].transform.position - spawnCenter.position;

            if (Vector3.Dot(spawnCenter.forward, toObj) < 0f &&
                toObj.magnitude > clearBehindDistance)
            {
                Destroy(spawned[i]);
                spawned.RemoveAt(i);
            }
        }
    }

    [ContextMenu("Clear All Spawned")]
    public void ClearSpawned()
    {
        foreach (var obj in spawned)
        {
            if (obj != null)
            {
                if (Application.isPlaying) Destroy(obj);
                else DestroyImmediate(obj);
            }
        }
        spawned.Clear();
    }

    // --- Scene Gizmos ---
    void OnDrawGizmos()
    {
        if (spawnCenter == null) spawnCenter = this.transform;

        // Show forward spawn marker
        Gizmos.color = Color.green;
        Vector3 forwardPos = spawnCenter.position + spawnCenter.forward * spawnAheadDistance;
        Gizmos.DrawWireSphere(forwardPos, 5f);

        // Show clear-behind radius
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(spawnCenter.position, clearBehindDistance);

        // Show final island safe zone
        if (finalIsland != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(finalIsland.position, noSpawnRadius);
        }
    }
}

public class SeaLevelFollower : MonoBehaviour
{
    public float yOffset = 0f;

    void Update()
    {
        if (Crest.OceanRenderer.Instance != null)
        {
            Vector3 p = transform.position;
            p.y = Crest.OceanRenderer.Instance.SeaLevel + yOffset;
            transform.position = p;
        }
    }
}
