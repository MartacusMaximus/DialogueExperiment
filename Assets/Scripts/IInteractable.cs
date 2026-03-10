using UnityEngine;

public interface IInteractable
{
    string GetInteractPrompt();
    void Interact(PlayerInteractor interactor);
}

public interface IBubbleInsertTarget
{
    bool CanInsert(SpeechBubbleEntity bubble);
    string GetInsertPrompt(SpeechBubbleEntity bubble);
}

public interface IHoldable
{
    void OnPickup(PlayerInteractor interactor);
    void OnDrop();
}
