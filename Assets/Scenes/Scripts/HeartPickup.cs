using UnityEngine;

public class HeartPickup : MonoBehaviour
{
    public int heartValue = 20;           // Value of each heart
    public AudioClip pickupSound;        // Sound to play when picked up
    public float pickupVolume = 1f;      // Volume (0 - 1)

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerStats.hearts += heartValue;

            if (pickupSound != null)
            {
                Debug.Log("[HeartPickup] Playing pickup sound");

                AudioSource audioSource = Camera.main.GetComponent<AudioSource>();
                if (audioSource == null)
                    audioSource = Camera.main.gameObject.AddComponent<AudioSource>();

                audioSource.spatialBlend = 0f; 
                audioSource.PlayOneShot(pickupSound, pickupVolume);
            }

            Destroy(gameObject);
        }
    }
}
