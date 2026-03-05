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
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactMask))
        {
            if (hit.collider.TryGetComponent<IInteractable>(out var interactable))
            {
                currentHover = interactable;

                if (heldItem != null)
                {
                    var machine = interactable as BubbleMachine;
                    if (machine != null)
                    {
                        var heldGO = GetHeldGameObject();
                        var bubble = heldGO != null ? heldGO.GetComponent<SpeechBubbleEntity>() : null;
                        if (bubble != null && bubble.isOrder)
                            InteractionPromptUI.Instance.Show("Press E to Insert Order");
                        else
                            InteractionPromptUI.Instance.Show("Press E to Insert");
                        return;
                    }

                    InteractionPromptUI.Instance.Show("Press E to Throw Item");
                    return;
                }

               InteractionPromptUI.Instance.Show(interactable.GetInteractPrompt());
                return;
            }
        }

        currentHover = null;

        if (heldItem != null)
        {
            InteractionPromptUI.Instance.Show("Press E to Throw Item");
        }
        else
        {
            InteractionPromptUI.Instance.Hide();
        }
    }

    public void TryInteract()
    {
        if (heldItem != null && currentHover != null)
        {
            currentHover.Interact(this);
            return;
        }

        if (heldItem != null && currentHover == null)
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
        if (obj == null) return;

        var holdable = obj.GetComponent<IHoldable>();
        if (holdable == null) return;

        heldItem = holdable;
        heldRb = obj.GetComponent<Rigidbody>();

        heldItem.OnPickup(this); 

        obj.transform.SetParent(holdPoint, false);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
    }

    void ThrowHeldItem()
    {
        if (heldItem == null) return;

        var mb = heldItem as MonoBehaviour;
        if (mb == null)
        {
            ClearHeld();
            return;
        }

        GameObject obj = mb.gameObject;

        obj.transform.SetParent(null);
        if (heldRb != null)
        {
            heldRb.isKinematic = false;
            heldRb.useGravity = true;
            heldRb.linearVelocity = cam.transform.forward * throwForce;
        }

        heldItem.OnDrop();
        ClearHeld();
    }

    void ClearHeld()
    {
        heldItem = null;
        heldRb = null;
    }

    public GameObject GetHeldGameObject()
    {
        if (heldItem == null) return null;
        var mb = heldItem as MonoBehaviour;
        return mb != null ? mb.gameObject : null;
    }

    public GameObject TakeHeldItemForInsertion()
    {
        if (heldItem == null) return null;

        var mb = heldItem as MonoBehaviour;
        if (mb == null) return null;

        GameObject obj = mb.gameObject;

        obj.transform.SetParent(null);

        if (heldRb != null)
        {
            heldRb.isKinematic = false;
            heldRb.useGravity = true;
        }

        heldItem.OnDrop();

        ClearHeld();

        return obj;
    }
}