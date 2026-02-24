using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DialogueSpeaker : MonoBehaviour, IInteractable
{
    [Header("Dialogue")]
    public DialogueNode dialogueNode;

    [Header("Spawn")]
    public Transform spawnAnchor;

    public string interactPrompt = "Press E to Talk";


    public string GetInteractPrompt()
    {
        return interactPrompt;
    }
    public void Interact(PlayerInteractor interactor)
    {
        if (dialogueNode == null) return;

        DialogueSystem.Instance.SpawnSpeechBubbles(dialogueNode, this);
    }

    // convenience: if you want to draw a gizmo for spawn anchor
    void OnDrawGizmosSelected()
    {
        if (spawnAnchor != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(spawnAnchor.position, 0.25f);
        }
    }
}
