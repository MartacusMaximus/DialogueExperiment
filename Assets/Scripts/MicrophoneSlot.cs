using UnityEngine;
using System;

public class MicrophoneSlot : MonoBehaviour, IInteractable, IBubbleInsertTarget
{
    public event Action<SpeechBubbleEntity> OnBubbleInserted;

    [Header("Slot")]
    public Transform insertAnchor;
    public bool singleBubbleOnly = true;
    public string insertPrompt = "Press E to Insert Bubble";
    public string occupiedPrompt = "Slot Occupied";

    [Header("Optional Forwarding")]
    public DialogueSpeaker targetSpeaker;
    public BubbleMachine targetMachine;

    SpeechBubbleEntity currentBubble;

    public bool HasBubble => currentBubble != null;

    void Awake()
    {
        if (insertAnchor == null)
            insertAnchor = transform;
    }

    public string GetInteractPrompt()
    {
        return HasBubble ? occupiedPrompt : insertPrompt;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (interactor == null) return;

        if (!interactor.TryTakeHeldSpeechBubble(out var bubble))
            return;

        if (!CanInsert(bubble))
        {
            interactor.ReturnTakenBubble(bubble);
            return;
        }

        ReceiveBubble(bubble);
    }

    public bool CanInsert(SpeechBubbleEntity bubble)
    {
        if (bubble == null) return false;
        if (!singleBubbleOnly) return true;
        return currentBubble == null;
    }

    public string GetInsertPrompt(SpeechBubbleEntity bubble)
    {
        if (!CanInsert(bubble))
            return occupiedPrompt;

        return insertPrompt;
    }

    public void ReceiveBubble(SpeechBubbleEntity bubble)
    {
        if (!CanInsert(bubble)) return;

        currentBubble = bubble;
        bubble.Insert(insertAnchor != null ? insertAnchor : transform);
        OnBubbleInserted?.Invoke(bubble);

        if (targetMachine != null)
            targetMachine.ReceiveInsertedBubble(bubble, this);

        if (targetSpeaker != null)
            targetSpeaker.ReceiveBubbleFromMicrophone(bubble, this);
    }

    public void ClearCurrentBubble(SpeechBubbleEntity bubble = null)
    {
        if (bubble != null && currentBubble != bubble) return;
        currentBubble = null;
    }
}
