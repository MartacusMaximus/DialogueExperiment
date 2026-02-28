using UnityEngine;
using System;

public class MicrophoneSlot : MonoBehaviour
{
    public event Action<SpeechBubbleEntity> OnBubbleInserted;

    public void ReceiveBubble(SpeechBubbleEntity bubble)
    {
        if (bubble == null) return;

        bubble.transform.SetParent(transform, false);
        bubble.transform.localPosition = Vector3.zero;
        bubble.transform.localRotation = Quaternion.identity;

        bubble.Insert();
        OnBubbleInserted?.Invoke(bubble);
    }
}