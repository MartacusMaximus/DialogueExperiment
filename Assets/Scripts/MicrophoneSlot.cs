using UnityEngine;

public class MicrophoneSlot : MonoBehaviour
{
    //public event Action<SpeechBubbleEntity> OnBubbleInserted;

    public void ReceiveBubble(SpeechBubbleEntity bubble)
    {
        //OnBubbleInserted?.Invoke(bubble);
    }
}

