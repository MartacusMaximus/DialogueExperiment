using UnityEngine;
using System.Collections.Generic;

public class SpeechBubbleStack : MonoBehaviour
{
    public SpeechBubbleView bubblePrefab;
    public float verticalSpacing = 60f; 

    private readonly List<SpeechBubbleView> activeBubbles = new();

    public SpeechBubbleView SpawnBubble(string text, float typeSpeed, float autoFadeTime, float postRevealDelay = 0f)
    {
        var go = Instantiate(bubblePrefab.gameObject, transform);
        var view = go.GetComponent<SpeechBubbleView>();
        view.Initialize(text, typeSpeed, autoFadeTime, postRevealDelay);


        view.OnLifeComplete += v => {
            // implement list removal logic hier
        };

        return view;
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
