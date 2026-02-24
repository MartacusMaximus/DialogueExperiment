using UnityEngine;

public class SpikeProjectile : MonoBehaviour, IInteractable, IHoldable
{
    void OnCollisionEnter(Collision collision)
    {
        SpeechBubbleEntity bubble = collision.collider.GetComponent<SpeechBubbleEntity>();

        if (bubble != null)
        {
            Destroy(bubble.gameObject);
            Destroy(gameObject); // spike consumed
        }
    }
    public string GetInteractPrompt()
    {
        return "Press E to Pick Up Spike";
    }

    public void Interact(PlayerInteractor interactor)
    {
        interactor.PickupItem(gameObject);
    }

    public void OnPickup(PlayerInteractor interactor) { }
    public void OnDrop() { }
}