using UnityEngine;

public class HeartPickup : MonoBehaviour
{
    public int heartValue = 20;           // Value of each heart
    public AudioClip pickupSound;        // Sound to play when picked up
    public float pickupVolume = 1f;      // Volume (0 - 1)

    private void OnTriggerEnter(Collider other)
    {
        // First check if the ML agent picked it up
        var agent = other.GetComponent<ShipAgent>();
        if (agent != null)
        {
            agent.RewardHeart(heartValue);
            PlaySound();
            Destroy(gameObject);
            return;
        }

        // Fallback: human player
        if (other.CompareTag("Player"))
        {
            PlayerStats.hearts += heartValue;
            PlaySound();
            Destroy(gameObject);
        }
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
