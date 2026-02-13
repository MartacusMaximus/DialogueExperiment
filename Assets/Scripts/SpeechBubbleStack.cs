using UnityEngine;
using System.Collections.Generic;

public class SpeechBubbleStack : MonoBehaviour
{
    public SpeechBubbleView bubblePrefab;
    public float verticalSpacing = 60f; 

    private readonly List<SpeechBubbleView> activeBubbles = new();

    public SpeechBubbleView SpawnBubble(string text, float typeSpeed, float autoFadeTime)
    {
        var bubble = Instantiate(bubblePrefab, transform);
        bubble.Initialize(text, typeSpeed, autoFadeTime);

        activeBubbles.Add(bubble);
        RepositionBubbles();

        return bubble;
    }

    private void HandleBubbleDestroyed(SpeechBubbleView bubble)
    {
        if (activeBubbles.Contains(bubble))
            activeBubbles.Remove(bubble);

        RepositionBubbles();
    }

    private void RepositionBubbles()
    {
        for (int i = 0; i < activeBubbles.Count; i++)
        {
            activeBubbles[i].transform.localPosition =
                Vector3.up * verticalSpacing * (activeBubbles.Count - 1 - i);
        }
    }
}
