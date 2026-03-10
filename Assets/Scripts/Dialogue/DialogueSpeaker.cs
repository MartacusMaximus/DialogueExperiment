using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class DialogueSpeaker : MonoBehaviour, IInteractable, IBubbleInsertTarget
{
    [Header("Dialogue")]
    public DialogueNode dialogueNode;

    [Header("Spawn")]
    public Transform spawnAnchor;
    public MicrophoneSlot microphoneSlot;
    public float autoBindMicrophoneDistance = 2f;
    public string interactPrompt = "Press E to Talk";
    public string deliverPrompt = "Press E to Deliver Bubble";
    public bool clearExpectedResponsesOnTalk = true;
    public bool requireExpectedResponse = true;
    [Tooltip("Prevents spawning new talk bubbles while this speaker still waits for a valid branch response.")]
    public bool blockTalkWhileAwaitingResponse = true;

    readonly List<PendingBranch> pendingBranches = new List<PendingBranch>();
    int nextPendingGroupId = 1;

    class PendingBranch
    {
        public int groupId;
        public string sourceBubbleId;
        public string branchKey;
        public string[] respondsTo;
        public string followUpEvent;
        public string[] sceneTriggers;
    }

    void Start()
    {
        TryAutoBindMicrophone();

        if (DialogueSystem.Instance != null)
            DialogueSystem.Instance.RegisterNode(dialogueNode);
    }

    void OnEnable()
    {
        TryAutoBindMicrophone();
    }

    void OnDisable()
    {
        if (microphoneSlot != null && microphoneSlot.targetSpeaker == this)
            microphoneSlot.targetSpeaker = null;

        pendingBranches.Clear();
    }

    public string GetInteractPrompt()
    {
        return interactPrompt;
    }

    public bool CanInsert(SpeechBubbleEntity bubble)
    {
        return bubble != null;
    }

    public string GetInsertPrompt(SpeechBubbleEntity bubble)
    {
        return CanInsert(bubble) ? deliverPrompt : interactPrompt;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (interactor != null && interactor.TryTakeHeldSpeechBubble(out var heldBubble))
        {
            ReceiveBubbleFromMicrophone(heldBubble, null);
            return;
        }

        Talk();
    }

    public void Talk()
    {
        if (dialogueNode == null || DialogueSystem.Instance == null)
            return;

        if (blockTalkWhileAwaitingResponse && requireExpectedResponse && pendingBranches.Count > 0)
            return;

        if (clearExpectedResponsesOnTalk)
            pendingBranches.Clear();

        DialogueSystem.Instance.RegisterNode(dialogueNode);
        DialogueSystem.Instance.SpawnSpeechBubbles(dialogueNode, this);
    }

    public void OnBubbleSpawned(SpeechBubbleEntity bubble)
    {
        if (bubble == null || bubble.responseBranches == null || bubble.responseBranches.Length == 0)
            return;

        int groupId = nextPendingGroupId++;

        for (int i = 0; i < bubble.responseBranches.Length; i++)
        {
            DialogueResponseBranch branch = bubble.responseBranches[i];
            if (branch == null) continue;

            string[] branchRespondsTo = CleanTokens(branch.respondsTo);
            if (branchRespondsTo.Length == 0) continue;

            pendingBranches.Add(new PendingBranch
            {
                groupId = groupId,
                sourceBubbleId = bubble.bubbleId,
                branchKey = (branch.branchKey ?? string.Empty).Trim(),
                respondsTo = branchRespondsTo,
                followUpEvent = (branch.followUpEvent ?? string.Empty).Trim(),
                sceneTriggers = CleanTokens(branch.sceneTriggers)
            });
        }
    }

    public void ReceiveBubbleFromMicrophone(SpeechBubbleEntity bubble, MicrophoneSlot slot)
    {
        if (bubble == null)
            return;

        string bubbleId = (bubble.bubbleId ?? string.Empty).Trim();
        bool hadPendingBranches = pendingBranches.Count > 0;

        PendingBranch matchedBranch = FindMatchedBranch(bubbleId);
        if (requireExpectedResponse && hadPendingBranches && matchedBranch == null)
        {
            RejectBubble(bubble, slot);
            return;
        }

        string followUpEvent = bubble.followUpEvent;
        string[] sceneTriggers = CleanTokens(bubble.sceneTriggers);

        if (matchedBranch != null)
        {
            if (!string.IsNullOrWhiteSpace(matchedBranch.followUpEvent))
                followUpEvent = matchedBranch.followUpEvent;

            sceneTriggers = MergeTokens(sceneTriggers, matchedBranch.sceneTriggers);
            RemovePendingBranchGroup(matchedBranch.groupId);
        }

        if (DialogueSystem.Instance != null && DialogueSystem.Instance.TryGetBubbleById(bubbleId, out var registeredBubble))
        {
            if (string.IsNullOrWhiteSpace(followUpEvent))
                followUpEvent = registeredBubble.followUpEvent;

            sceneTriggers = MergeTokens(sceneTriggers, registeredBubble.sceneTriggers);

            if (string.IsNullOrWhiteSpace(followUpEvent) &&
                bubbleId.StartsWith("D_", System.StringComparison.OrdinalIgnoreCase))
            {
                followUpEvent = bubbleId;
            }
        }

        DialogueSystem.Instance?.InvokeSceneTriggers(sceneTriggers, this, bubble);

        if (!string.IsNullOrWhiteSpace(followUpEvent))
        {
            bool spawned = DialogueSystem.Instance != null &&
                           DialogueSystem.Instance.SpawnByEventId(followUpEvent, this);
            if (!spawned)
                Debug.LogWarning($"DialogueSpeaker '{name}' could not resolve follow-up event '{followUpEvent}'.");
        }

        if (slot != null)
            slot.ClearCurrentBubble(bubble);

        bubble.Consume();
    }

    PendingBranch FindMatchedBranch(string responseBubbleId)
    {
        if (string.IsNullOrWhiteSpace(responseBubbleId))
            return null;

        for (int i = 0; i < pendingBranches.Count; i++)
        {
            PendingBranch branch = pendingBranches[i];
            if (branch == null || branch.respondsTo == null) continue;

            for (int j = 0; j < branch.respondsTo.Length; j++)
            {
                if (!string.Equals(branch.respondsTo[j], responseBubbleId, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                return branch;
            }
        }

        return null;
    }

    void RemovePendingBranchGroup(int groupId)
    {
        for (int i = pendingBranches.Count - 1; i >= 0; i--)
        {
            if (pendingBranches[i].groupId == groupId)
                pendingBranches.RemoveAt(i);
        }
    }

    static string[] CleanTokens(string[] tokens)
    {
        if (tokens == null || tokens.Length == 0)
            return System.Array.Empty<string>();

        var cleaned = new List<string>(tokens.Length);
        for (int i = 0; i < tokens.Length; i++)
        {
            string value = tokens[i];
            if (string.IsNullOrWhiteSpace(value)) continue;

            string trimmed = value.Trim();
            if (trimmed.Length == 0) continue;
            cleaned.Add(trimmed);
        }

        return cleaned.Count == 0 ? System.Array.Empty<string>() : cleaned.ToArray();
    }

    static string[] MergeTokens(string[] first, string[] second)
    {
        if ((first == null || first.Length == 0) && (second == null || second.Length == 0))
            return System.Array.Empty<string>();

        if (first == null || first.Length == 0)
            return CleanTokens(second);

        if (second == null || second.Length == 0)
            return CleanTokens(first);

        var merged = new List<string>();
        string[] cleanFirst = CleanTokens(first);
        string[] cleanSecond = CleanTokens(second);

        AddUnique(merged, cleanFirst);
        AddUnique(merged, cleanSecond);

        return merged.Count == 0 ? System.Array.Empty<string>() : merged.ToArray();
    }

    static void AddUnique(List<string> destination, string[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            string value = values[i];
            bool exists = false;
            for (int j = 0; j < destination.Count; j++)
            {
                if (string.Equals(destination[j], value, System.StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
                destination.Add(value);
        }
    }

    void TryAutoBindMicrophone()
    {
        if (microphoneSlot == null)
        {
            MicrophoneSlot[] slots = FindObjectsOfType<MicrophoneSlot>();
            float maxDistSq = autoBindMicrophoneDistance * autoBindMicrophoneDistance;
            float bestDistSq = float.MaxValue;

            for (int i = 0; i < slots.Length; i++)
            {
                MicrophoneSlot slot = slots[i];
                if (slot == null) continue;
                if (slot.targetMachine != null) continue;
                if (slot.targetSpeaker != null && slot.targetSpeaker != this) continue;

                float distSq = (slot.transform.position - transform.position).sqrMagnitude;
                if (distSq > maxDistSq) continue;
                if (distSq >= bestDistSq) continue;

                bestDistSq = distSq;
                microphoneSlot = slot;
            }
        }

        if (microphoneSlot != null && microphoneSlot.targetMachine == null)
            microphoneSlot.targetSpeaker = this;
    }

    void RejectBubble(SpeechBubbleEntity bubble, MicrophoneSlot slot)
    {
        if (bubble == null) return;

        if (slot != null)
            slot.ClearCurrentBubble(bubble);

        Vector3 releaseOrigin = slot != null && slot.insertAnchor != null
            ? slot.insertAnchor.position
            : transform.position;
        Vector3 releasePos = releaseOrigin + transform.forward * 0.5f + Vector3.up * 0.1f;

        bubble.ReleaseFromSlot(releasePos);
    }

    void OnDrawGizmosSelected()
    {
        if (spawnAnchor != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(spawnAnchor.position, 0.25f);
        }
    }
}
