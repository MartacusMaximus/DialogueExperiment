using UnityEngine;

public interface IInteractable
{
    string GetInteractPrompt();
    void Interact(PlayerInteractor interactor);
}

public interface IHoldable
{
    void OnPickup(PlayerInteractor interactor);
    void OnDrop();
}