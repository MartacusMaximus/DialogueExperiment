using UnityEngine;

public class DialogueSpeaker : MonoBehaviour, IInteractable
{
    public DialogueNode startingDialogue;
    public Transform dialogueCameraAnchor;

    public void Interact(FirstPersonController player)
    {
        DialogueSystem.Instance.StartDialogue(startingDialogue, this);
    }
}
