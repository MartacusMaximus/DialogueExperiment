using UnityEngine;
using TMPro;

public class InteractionPromptUI : MonoBehaviour
{
    public static InteractionPromptUI Instance;

    public GameObject root;
    public TMP_Text promptText;

    void Awake()
    {
        Instance = this;
        Hide();
    }

    public void Show(string message)
    {
        root.SetActive(true);
        promptText.text = message;
    }

    public void Hide()
    {
        root.SetActive(false);
    }
}