using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [Header("References")]
    public Camera cam;
    public Transform holdPoint;

    [Header("Settings")]
    public float interactRange = 4f;
    public float throwForce = 8f;
    public LayerMask interactMask;

    IInteractable currentHover;
    IHoldable heldItem;
    Rigidbody heldRb;

    void Update()
    {
        HandleHover();
    }

    void HandleHover()
    {
        // If holding something always show throw prompt
        if (heldItem != null)
        {
            InteractionPromptUI.Instance.Show("Press E to Throw Item");
            return;
        }

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactMask))
        {
            if (hit.collider.TryGetComponent<IInteractable>(out var interactable))
            {
                currentHover = interactable;
                InteractionPromptUI.Instance.Show(interactable.GetInteractPrompt());
                return;
            }
        }

        currentHover = null;
        InteractionPromptUI.Instance.Hide();
    }

    public void TryInteract()
    {
        // If holding something throw it anywhere
        if (heldItem != null)
        {
            ThrowHeldItem();
            return;
        }

        if (currentHover != null)
        {
            currentHover.Interact(this);
        }
    }

    public void PickupItem(GameObject obj)
    {
        heldItem = obj.GetComponent<IHoldable>();
        heldRb = obj.GetComponent<Rigidbody>();

        if (heldItem == null) return;

        heldItem.OnPickup(this);

        if (heldRb != null)
        {
            heldRb.isKinematic = true;
            heldRb.useGravity = false;
        }

        obj.transform.SetParent(holdPoint);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
    }

    void ThrowHeldItem()
    {
        Debug.Log("Throw");
        if (heldItem == null) return;

        Transform obj = ((MonoBehaviour)heldItem).transform;

        obj.SetParent(null);

        if (heldRb != null)
        {
            heldRb.isKinematic = false;
            heldRb.useGravity = true;
            heldRb.linearVelocity = cam.transform.forward * throwForce;
        }

        heldItem.OnDrop();

        heldItem = null;
        heldRb = null;
    }
}