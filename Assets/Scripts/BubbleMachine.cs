using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Serialization;

public class BubbleMachine : MonoBehaviour, IInteractable, IBubbleInsertTarget
{
    [Header("References")]
    public SpeechBubbleEntity bubblePrefab;

    public Transform bubbleSpawnPoint;

    public MicrophoneSlot inputSlot;

    public Transform insertPoint;

    [Header("Option Buttons")]
    public OptionButton yesButtonPrefab;
    public OptionButton noButtonPrefab;
    public Transform optionSpawnPoint;

    [Header("Settings")]
    public float spawnDelay = 0.6f;

    [Min(1)]
    public int maxMachineButtons = 2;
    public float optionSpacing = 0.5f;
    public string idlePrompt = "Press E to Use Bubble Machine";
    public string insertPrompt = "Press E to Insert Bubble";
    public string occupiedPrompt = "Machine Busy";

    SpeechBubbleEntity insertedBubble;
    readonly List<OptionButton> activeOptionButtons = new List<OptionButton>();

    protected virtual void Awake()
    {
        if (bubblePrefab == null && DialogueSystem.Instance != null)
            bubblePrefab = DialogueSystem.Instance.bubblePrefab;

        if (bubbleSpawnPoint == null)
            bubbleSpawnPoint = transform;

        if (insertPoint == null)
            insertPoint = transform;

        if (optionSpawnPoint == null)
            optionSpawnPoint = insertPoint != null ? insertPoint : transform;

        if (inputSlot == null && insertPoint != null)
            inputSlot = insertPoint.GetComponent<MicrophoneSlot>();

        if (inputSlot == null && insertPoint != null)
            inputSlot = insertPoint.gameObject.AddComponent<MicrophoneSlot>();

        if (inputSlot != null)
        {
            inputSlot.targetMachine = this;
            if (inputSlot.insertAnchor == null)
                inputSlot.insertAnchor = insertPoint;
        }
    }

    protected virtual void OnEnable()
    {
        if (inputSlot != null)
            inputSlot.OnBubbleInserted += HandleBubbleInserted;
    }

    protected virtual void OnDisable()
    {
        if (inputSlot != null)
            inputSlot.OnBubbleInserted -= HandleBubbleInserted;
    }

    void HandleBubbleInserted(SpeechBubbleEntity bubble)
    {
        if (bubble == null) return;
        if (bubble == insertedBubble) return;

        ReceiveInsertedBubble(bubble, inputSlot);
    }

    public bool CanInsert(SpeechBubbleEntity bubble)
    {
        return bubble != null && insertedBubble == null;
    }

    public string GetInsertPrompt(SpeechBubbleEntity bubble)
    {
        return CanInsert(bubble) ? insertPrompt : occupiedPrompt;
    }

    public string GetInteractPrompt()
    {
        return insertedBubble == null ? idlePrompt : "Choose a response button";
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

        if (inputSlot != null)
        {
            if (!inputSlot.CanInsert(bubble))
            {
                interactor.ReturnTakenBubble(bubble);
                return;
            }

            inputSlot.ReceiveBubble(bubble);
            return;
        }

        ReceiveInsertedBubble(bubble, null);
    }

    public void ReceiveInsertedBubble(SpeechBubbleEntity bubble, MicrophoneSlot sourceSlot)
    {
        if (!CanInsert(bubble)) return;

        insertedBubble = bubble;
        insertedBubble.Insert(insertPoint != null ? insertPoint : transform);

        if (sourceSlot != null && sourceSlot != inputSlot)
            sourceSlot.ClearCurrentBubble(bubble);

        StartCoroutine(ProcessInsertedBubbleRoutine(bubble));
    }

    IEnumerator ProcessInsertedBubbleRoutine(SpeechBubbleEntity bubble)
    {
        if (bubble == null)
            yield break;

        yield return new WaitForSeconds(Mathf.Max(0f, spawnDelay));
        ActivateResponseButtons(bubble);
    }

    void ActivateResponseButtons(SpeechBubbleEntity bubble)
    {
        ClearOptionButtons();

        if (bubble == null || bubble.responseBranches == null || bubble.responseBranches.Length == 0)
        {
            Debug.Log("BubbleMachine: inserted bubble has no response branches.");
            return;
        }

        OptionButton fallbackPrefab = yesButtonPrefab != null ? yesButtonPrefab : noButtonPrefab;
        if (fallbackPrefab == null)
        {
            Debug.LogWarning("BubbleMachine: No option button prefab assigned.");
            return;
        }

        List<string> machineResponses = CollectMachineResponses(bubble);
        if (machineResponses.Count == 0)
        {
            Debug.Log("BubbleMachine: no machine-eligible branches found (requires R_ ids in RespondsToXX).");
            return;
        }

        int optionCount = machineResponses.Count;
        float startOffset = (optionCount - 1) * 0.5f;

        for (int i = 0; i < optionCount; i++)
        {
            string responseId = machineResponses[i];

            OptionButton prefab = ResolveOptionPrefab(i, fallbackPrefab);
            Vector3 pos = optionSpawnPoint.position + optionSpawnPoint.right * ((i - startOffset) * optionSpacing);

            string label = BuildButtonLabel(responseId);
            OptionButton button = Instantiate(prefab, pos, optionSpawnPoint.rotation);
            button.Initialize(this, responseId, label);
            activeOptionButtons.Add(button);
        }
    }

    List<string> CollectMachineResponses(SpeechBubbleEntity bubble)
    {
        var result = new List<string>();
        if (bubble == null || bubble.responseBranches == null)
            return result;

        int max = Mathf.Max(1, maxMachineButtons);
        for (int i = 0; i < bubble.responseBranches.Length; i++)
        {
            DialogueResponseBranch branch = bubble.responseBranches[i];
            if (branch == null)
                continue;

            if (!IsMachineBranch(branch, max))
                continue;

            if (!SpeechBubbleEntity.TryGetMachineResponseId(branch, out string responseId))
                continue;

            if (string.IsNullOrWhiteSpace(responseId))
                continue;

            bool duplicate = false;
            for (int j = 0; j < result.Count; j++)
            {
                if (string.Equals(result[j], responseId, System.StringComparison.OrdinalIgnoreCase))
                {
                    duplicate = true;
                    break;
                }
            }

            if (duplicate)
                continue;

            result.Add(responseId);
            if (result.Count >= max)
                break;
        }

        return result;
    }

    bool IsMachineBranch(DialogueResponseBranch branch, int maxButtons)
    {
        if (branch == null)
            return false;

        if (string.IsNullOrWhiteSpace(branch.branchKey))
            return true;

        if (!int.TryParse(branch.branchKey, out int branchNumber))
            return true;

        return branchNumber >= 1 && branchNumber <= maxButtons;
    }

    string BuildButtonLabel(string responseId)
    {
        if (DialogueSystem.Instance != null &&
            DialogueSystem.Instance.TryGetBubbleById(responseId, out SpeechBubble responseBubble) &&
            !string.IsNullOrWhiteSpace(responseBubble.text))
        {
            string text = responseBubble.text.Trim();
            if (text.Length > 28)
                return text.Substring(0, 28) + "...";
            return text;
        }

        return responseId;
    }

    OptionButton ResolveOptionPrefab(int optionIndex, OptionButton fallbackPrefab)
    {
        if (optionIndex == 0 && yesButtonPrefab != null) return yesButtonPrefab;
        if (optionIndex == 1 && noButtonPrefab != null) return noButtonPrefab;
        if (yesButtonPrefab != null) return yesButtonPrefab;
        if (noButtonPrefab != null) return noButtonPrefab;
        return fallbackPrefab;
    }

    public void OptionSelected(string selectedResponseId)
    {
        if (insertedBubble == null || string.IsNullOrWhiteSpace(selectedResponseId))
            return;

        SpawnResponseBubble(selectedResponseId.Trim());
        ConsumeInsertedBubble();
    }

    void SpawnResponseBubble(string responseId)
    {
        Transform spawnTransform = bubbleSpawnPoint != null ? bubbleSpawnPoint : transform;

        if (DialogueSystem.Instance != null)
        {
            DialogueSystem.Instance.SpawnBubbleFromReference(responseId, spawnTransform);
            return;
        }

        if (bubblePrefab == null) return;

        Vector3 spawnPos = spawnTransform.position;
        var go = Instantiate(bubblePrefab.gameObject, spawnPos, Quaternion.identity, null).GetComponent<SpeechBubbleEntity>();
        go.InitializeGenerated(responseId, "Bubble Machine", $"Response: {responseId}");
        go.transform.localScale = Vector3.zero;
        go.PlayInflate();
    }

    void ConsumeInsertedBubble()
    {
        SpeechBubbleEntity consumed = insertedBubble;
        insertedBubble = null;

        ClearOptionButtons();
        if (inputSlot != null)
            inputSlot.ClearCurrentBubble(consumed);

        if (consumed != null)
            consumed.Consume();
    }

    void ClearOptionButtons()
    {
        for (int i = 0; i < activeOptionButtons.Count; i++)
        {
            if (activeOptionButtons[i] != null)
                Destroy(activeOptionButtons[i].gameObject);
        }

        activeOptionButtons.Clear();
    }

    public void CancelCurrentInsert(bool ejectToWorld)
    {
        if (insertedBubble == null)
            return;

        SpeechBubbleEntity bubble = insertedBubble;
        insertedBubble = null;

        if (inputSlot != null)
            inputSlot.ClearCurrentBubble(bubble);

        ClearOptionButtons();

        if (ejectToWorld)
        {
            Vector3 releasePos = (insertPoint != null ? insertPoint.position : transform.position) + transform.forward * 0.6f;
            bubble.ReleaseFromSlot(releasePos);
        }
        else
        {
            bubble.Consume();
        }
    }
}
