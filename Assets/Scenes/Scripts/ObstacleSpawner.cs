using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PrefabSpawnSettings
{
    [Header("Prefab Reference")]
    public GameObject prefab;

    [Header("Placement Settings")]
    public float minRadius = 10f;
    public float maxRadius = 50f;

    [Header("Spawn Options")]
    public bool preserveScale = true;
    public bool preserveHeight = false;
    public bool followSeaLevel = false;
    public float yOffset = 0f;

    [Header("Pooling")]
    public int poolSize = 8; // how many instances to pre-create for this prefab
}

public class ObstacleSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public List<PrefabSpawnSettings> prefabsWithSettings = new List<PrefabSpawnSettings>();
    public Transform spawnCenter; // usually the ship
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
    public Transform finalIsland;
    public float noSpawnRadius = 300f;

    // Pools: one list per prefab setting
    List<List<GameObject>> pools = new List<List<GameObject>>();
    List<PrefabSpawnSettings> settingsList = new List<PrefabSpawnSettings>();
    private List<GameObject> spawnedActive = new List<GameObject>();
    private Vector3 lastSpawnPos;

    void Start()
    {
        if (spawnCenter == null) spawnCenter = this.transform;
        lastSpawnPos = spawnCenter.position;
        SetupPools();
        if (spawnOnStart) SpawnObstaclesAhead();
    }

    void SetupPools()
    {
        pools.Clear();
        settingsList.Clear();
        for (int i = 0; i < prefabsWithSettings.Count; i++)
        {
            var s = prefabsWithSettings[i];
            settingsList.Add(s);
            var pool = new List<GameObject>();
            if (s.prefab != null)
            {
                GameObject root = new GameObject($"Pool_{s.prefab.name}");
                root.transform.SetParent(transform);
                for (int n = 0; n < Mathf.Max(1, s.poolSize); n++)
                {
                    var go = Instantiate(s.prefab, Vector3.zero, s.prefab.transform.rotation, root.transform);
                    go.SetActive(false);
                    pool.Add(go);
                }
            }
            pools.Add(pool);
        }
    }

    GameObject GetFromPool(int prefabIndex, Vector3 pos, Quaternion rot)
    {
        if (prefabIndex < 0 || prefabIndex >= pools.Count) return null;
        var pool = pools[prefabIndex];
        foreach (var go in pool)
        {
            if (go == null) continue;
            if (!go.activeSelf)
            {
                go.transform.position = pos;
                go.transform.rotation = rot;
                go.SetActive(true);
                return go;
            }
        }
        // None available, optionally expand pool (cheap)
        var s = settingsList[prefabIndex];
        var newGo = Instantiate(s.prefab, pos, s.prefab.transform.rotation, transform);
        newGo.SetActive(true);
        pool.Add(newGo);
        return newGo;
    }

    void ReturnToPool(GameObject go)
    {
        if (go == null) return;
        // simply deactivate
        go.SetActive(false);
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
            int idx = Random.Range(0, prefabsWithSettings.Count);
            var settings = prefabsWithSettings[idx];
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

                bool tooClose = false;
                foreach (var obj in spawnedActive)
                {
                    if (obj == null) continue;
                    if (Vector3.Distance(obj.transform.position, finalPos) < minSpacing)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (finalIsland != null && Vector3.Distance(finalIsland.position, finalPos) < noSpawnRadius)
                {
                    tooClose = true;
                }

                if (!tooClose) foundPos = true;
                attempts++;
            }

            if (!foundPos) continue;

            var rot = settings.prefab.transform.rotation;
            GameObject go = GetFromPool(idx, finalPos, rot);

            if (settings.preserveScale && go != null)
                go.transform.localScale = settings.prefab.transform.localScale;

            if (settings.followSeaLevel && go != null)
            {
                var sl = go.GetComponent<SeaLevelFollower>();
                if (sl == null) sl = go.AddComponent<SeaLevelFollower>();
                sl.yOffset = settings.yOffset;
            }

            spawnedActive.Add(go);
            spawnedThisWave++;
        }

        Debug.Log($"[ObstacleSpawner] Spawned {spawnedThisWave} obstacles ahead of ship.");
    }

    void ClearBehindPlayer()
    {
        for (int i = spawnedActive.Count - 1; i >= 0; i--)
        {
            var obj = spawnedActive[i];
            if (obj == null) { spawnedActive.RemoveAt(i); continue; }

            Vector3 toObj = obj.transform.position - spawnCenter.position;
            if (Vector3.Dot(spawnCenter.forward, toObj) < 0f && toObj.magnitude > clearBehindDistance)
            {
                // deactivate and keep in pool
                ReturnToPool(obj);
                spawnedActive.RemoveAt(i);
            }
        }
    }

    [ContextMenu("Clear All Spawned")]
    public void ClearSpawned()
    {
        // Deactivate all active objects
        for (int i = spawnedActive.Count - 1; i >= 0; i--)
        {
            if (spawnedActive[i] != null) ReturnToPool(spawnedActive[i]);
        }
        spawnedActive.Clear();
    }

    void OnDrawGizmos()
    {
        if (spawnCenter == null) spawnCenter = this.transform;
        Gizmos.color = Color.green;
        Vector3 forwardPos = spawnCenter.position + spawnCenter.forward * spawnAheadDistance;
        Gizmos.DrawWireSphere(forwardPos, 5f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(spawnCenter.position, clearBehindDistance);
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
