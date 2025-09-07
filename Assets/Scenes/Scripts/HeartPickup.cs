using UnityEngine;

public class HeartPickup : MonoBehaviour
{
    public int heartValue = 20;           // Value of each heart
    public AudioClip pickupSound;        // Sound to play when picked up
    public float pickupVolume = 1f;      // Volume (0 - 1)

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[HeartPickup] Trigger entered by: {other.name}, Tag: {other.tag}");
        
        // Try to get ShipAgent component directly (more reliable)
        var agent = other.GetComponent<ShipAgent>();
        if (agent != null)
        {
            Debug.Log($"[HeartPickup] Found ShipAgent component, rewarding heart: {heartValue}");
            agent.RewardHeart(heartValue);
            PlaySound();
            Destroy(gameObject);
            return;
        }

        // Check if this is tagged as Agent (backup check)
        if (other.CompareTag("Agent"))
        {
            Debug.Log($"[HeartPickup] Found Agent tag but no ShipAgent component on {other.name}");
            // Try to find ShipAgent in parent or children
            var agentInParent = other.GetComponentInParent<ShipAgent>();
            var agentInChildren = other.GetComponentInChildren<ShipAgent>();
            
            if (agentInParent != null)
            {
                Debug.Log($"[HeartPickup] Found ShipAgent in parent, rewarding heart: {heartValue}");
                agentInParent.RewardHeart(heartValue);
                PlaySound();
                Destroy(gameObject);
                return;
            }
            
            if (agentInChildren != null)
            {
                Debug.Log($"[HeartPickup] Found ShipAgent in children, rewarding heart: {heartValue}");
                agentInChildren.RewardHeart(heartValue);
                PlaySound();
                Destroy(gameObject);
                return;
            }
        }

        // Fallback: human player
        if (other.CompareTag("Player"))
        {
            Debug.Log($"[HeartPickup] Found Player tag, adding to PlayerStats: {heartValue}");
            PlayerStats.hearts += heartValue;
            PlaySound();
            Destroy(gameObject);
            return;
        }
        
        Debug.Log($"[HeartPickup] No valid component found on {other.name} with tag {other.tag}");
    }

    void PlaySound()
    {
        if (pickupSound == null) return;
        Debug.Log("[HeartPickup] Playing pickup sound");
        var cam = Camera.main;
        if (cam == null) return;
        AudioSource audioSource = cam.GetComponent<AudioSource>();
        if (audioSource == null) audioSource = cam.gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
        audioSource.PlayOneShot(pickupSound, pickupVolume);
    }
}
