using UnityEngine;
using TMPro;

public class UIHeartsDisplay : MonoBehaviour
{
    public TextMeshProUGUI heartsText;

    void Awake()
    {
        if (heartsText == null)
            heartsText = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        if (heartsText != null)
            heartsText.text = "Scores: " + PlayerStats.hearts;
    }
}
