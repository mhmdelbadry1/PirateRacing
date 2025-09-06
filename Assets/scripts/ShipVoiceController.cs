using UnityEngine;

[DisallowMultipleComponent]
public class ShipVoiceController : MonoBehaviour
{
    [Header("Audio")]
    public AudioSource voiceSource;
    public AudioClip[] impactClips;
    public AudioClip[] steerRightClips;
    public AudioClip[] steerLeftClips;
    public AudioClip[] nearCollisionClips;
    public AudioClip[] proximityShipClips;

    [Header("Physics & Detection")]
    public Rigidbody shipRb;
    public float forwardCheckDistance = 25f;
    public float sideCheckDistance = 12f;
    public float proximityRadius = 18f;
    public LayerMask obstacleMask;
    public LayerMask shipMask;

    [Header("Thresholds")]
    public float impactMinSpeed = 2.5f;
    public float nearCollisionDangerDist = 10f;
    public float clearSideDotThreshold = 0.2f;

    [Header("Cooldowns (seconds)")]
    public float impactCooldown = 0.5f;
    public float steerCooldown = 3.0f;
    public float nearCollisionCooldown = 2.5f;
    public float proximityCooldown = 3.0f;

    [Header("Volumes")]
    [Range(0f, 1f)] public float impactVolume = 1f;
    [Range(0f, 1f)] public float steerVolume = 1f;
    [Range(0f, 1f)] public float nearCollisionVolume = 1f;
    [Range(0f, 1f)] public float proximityVolume = 1f;

    float impactTimer;
    float steerTimer;
    float nearCollisionTimer;
    float proximityTimer;

    void Reset()
    {
        if (!voiceSource) voiceSource = GetComponent<AudioSource>();
        if (!shipRb) shipRb = GetComponentInParent<Rigidbody>() ?? GetComponent<Rigidbody>();
    }

    void Awake()
    {
        if (!voiceSource)
        {
            voiceSource = gameObject.AddComponent<AudioSource>();
            voiceSource.playOnAwake = false;
            voiceSource.spatialBlend = 1f;
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        impactTimer = Mathf.Max(0, impactTimer - dt);
        steerTimer = Mathf.Max(0, steerTimer - dt);
        nearCollisionTimer = Mathf.Max(0, nearCollisionTimer - dt);
        proximityTimer = Mathf.Max(0, proximityTimer - dt);

        ForwardChecks();
        ProximityCheck();
    }

    void ForwardChecks()
    {
        Vector3 origin = transform.position + Vector3.up * 1.0f;
        Vector3 fwd = transform.forward;

        if (Physics.Raycast(origin, fwd, out RaycastHit hit, forwardCheckDistance, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            Debug.Log($"[ShipVoiceController] Obstacle detected at distance {hit.distance}");

            float dist = hit.distance;
            if (dist <= nearCollisionDangerDist && nearCollisionTimer <= 0f)
            {
                Debug.Log("[ShipVoiceController] Playing near-collision warning");
                PlayRandom(nearCollisionClips, nearCollisionVolume);
                nearCollisionTimer = nearCollisionCooldown;
            }

            if (steerTimer <= 0f)
            {
                bool rightClear = !Physics.Raycast(origin, transform.right, sideCheckDistance, obstacleMask, QueryTriggerInteraction.Ignore);
                bool leftClear = !Physics.Raycast(origin, -transform.right, sideCheckDistance, obstacleMask, QueryTriggerInteraction.Ignore);

                if (rightClear && !leftClear)
                {
                    Debug.Log("[ShipVoiceController] Suggesting RIGHT");
                    PlayRandom(steerRightClips, steerVolume);
                    steerTimer = steerCooldown;
                }
                else if (leftClear && !rightClear)
                {
                    Debug.Log("[ShipVoiceController] Suggesting LEFT");
                    PlayRandom(steerLeftClips, steerVolume);
                    steerTimer = steerCooldown;
                }
                else if (leftClear && rightClear)
                {
                    Debug.Log("[ShipVoiceController] Both sides clear, suggesting RIGHT");
                    PlayRandom(steerRightClips, steerVolume);
                    steerTimer = steerCooldown;
                }
                else
                {
                    Debug.Log("[ShipVoiceController] No clear side, staying silent");
                }
            }
        }
    }

    void ProximityCheck()
    {
        if (proximityTimer > 0f) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, proximityRadius, shipMask, QueryTriggerInteraction.Ignore);
        if (hits.Length > 0)
        {
            Debug.Log($"[ShipVoiceController] Detected {hits.Length} nearby ships");
        }

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].attachedRigidbody && hits[i].attachedRigidbody == shipRb) continue;
            if (hits[i].transform == transform) continue;

            Debug.Log("[ShipVoiceController] Playing proximity warning");
            PlayRandom(proximityShipClips, proximityVolume);
            proximityTimer = proximityCooldown;
            break;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (impactTimer > 0f || impactClips == null || impactClips.Length == 0)
        {
            Debug.Log("[ShipVoiceController] Impact ignored (cooldown or no clips)");
            return;
        }

        float relSpeed = collision.relativeVelocity.magnitude;
        Debug.Log($"[ShipVoiceController] Collision detected with {collision.gameObject.name}, speed={relSpeed}");

        if (relSpeed < impactMinSpeed)
        {
            Debug.Log("[ShipVoiceController] Impact too weak, no sound");
            return;
        }

        float vol = Mathf.Clamp01(relSpeed / (impactMinSpeed * 3f)) * impactVolume;

        Debug.Log("[ShipVoiceController] Playing impact sound");
        PlayRandom(impactClips, vol);
        impactTimer = impactCooldown;
    }

    void PlayRandom(AudioClip[] clips, float volume)
    {
        if (clips == null || clips.Length == 0)
        {
            Debug.LogWarning("[ShipVoiceController] No clips assigned!");
            return;
        }

        if (voiceSource == null)
        {
            Debug.LogError("[ShipVoiceController] No AudioSource assigned!");
            return;
        }

        int idx = Random.Range(0, clips.Length);
        Debug.Log($"[ShipVoiceController] Playing clip {clips[idx].name} at volume {volume}");
        voiceSource.PlayOneShot(clips[idx], volume);
    }
}
