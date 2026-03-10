using UnityEngine;
using TMPro;
using System;
using System.Collections;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]

public class SpeechBubbleEntity : MonoBehaviour, IInteractable, IHoldable
{
    [Header("References")]
    public TextMeshPro textMesh;

    [Header("Dialogue Data")]
    public string bubbleId;
    public string speakerName;
    public string message;
    public DialogueNode sourceNode;
    public int bubbleIndex = -1;
    public DialogueSpeaker sourceSpeaker;

    [Header("Branching")]
    public DialogueResponseBranch[] responseBranches = Array.Empty<DialogueResponseBranch>();
    public string[] sceneTriggers = Array.Empty<string>();
    public string followUpEvent;

    [Header("Inflate Animation")]
    public float inflateDuration = 0.35f;
    public AnimationCurve inflateCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public bool IsHeld { get; private set; }
    public bool IsInserted { get; private set; }

    public event Action<SpeechBubbleEntity> OnPricked;
    public event Action<SpeechBubbleEntity> OnPickedUp;
    public event Action<SpeechBubbleEntity> OnInserted;

    Coroutine inflateRoutine;
    FloatingBubble floating;
    Rigidbody rb;

    void Awake()
    {
        floating = GetComponent<FloatingBubble>();
        rb = GetComponent<Rigidbody>();
    }

    void OnDestroy()
    {
        if (DialogueSystem.Instance != null)
            DialogueSystem.Instance.NotifyBubbleDestroyed(this);
    }

    public bool HasResponseBranches => responseBranches != null && responseBranches.Length > 0;

    public void Initialize(SpeechBubble data, DialogueNode originNode, int index, DialogueSpeaker ownerSpeaker = null)
    {
        if (data == null)
        {
            InitializeGenerated(Guid.NewGuid().ToString("N"), "Unknown", string.Empty);
            sourceNode = originNode;
            sourceSpeaker = ownerSpeaker;
            bubbleIndex = index;
            return;
        }

        sourceNode = originNode;
        sourceSpeaker = ownerSpeaker;
        bubbleIndex = index;

        bubbleId = string.IsNullOrWhiteSpace(data.bubbleId)
            ? $"{originNode?.name ?? "Bubble"}_{Mathf.Max(0, index)}"
            : data.bubbleId.Trim();

        speakerName = data.speakerName;
        message = data.text;
        responseBranches = CloneBranches(data.responseBranches);
        if ((responseBranches == null || responseBranches.Length == 0) &&
            data.appropriateResponses != null &&
            data.appropriateResponses.Length > 0)
        {
            responseBranches = new[]
            {
                new DialogueResponseBranch
                {
                    branchKey = "01",
                    respondsTo = CleanArray(data.appropriateResponses),
                    followUpEvent = string.Empty,
                    sceneTriggers = Array.Empty<string>()
                }
            };
        }

        sceneTriggers = CleanArray(data.sceneTriggers);
        followUpEvent = (data.followUpEvent ?? string.Empty).Trim();

        IsInserted = false;
        UpdateText(message);
    }

    public void InitializeGenerated(string id, string speaker, string text, string[] responses = null, string[] triggers = null, string followUp = "")
    {
        bubbleId = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id.Trim();
        sourceNode = null;
        sourceSpeaker = null;
        bubbleIndex = -1;

        speakerName = speaker;
        responseBranches = Array.Empty<DialogueResponseBranch>();
        if (responses != null && responses.Length > 0)
        {
            responseBranches = new[]
            {
                new DialogueResponseBranch
                {
                    branchKey = "01",
                    respondsTo = CleanArray(responses),
                    followUpEvent = string.Empty,
                    sceneTriggers = Array.Empty<string>()
                }
            };
        }

        sceneTriggers = CleanArray(triggers);
        followUpEvent = (followUp ?? string.Empty).Trim();

        IsInserted = false;
        UpdateText(text);
    }

    public void InitializePrototype(MessageBubbleData data)
    {
        if (data == null)
        {
            InitializeGenerated(Guid.NewGuid().ToString("N"), "Unknown", string.Empty);
            return;
        }

        InitializeGenerated(Guid.NewGuid().ToString("N"), "Prototype", data.text, data.options?.ToArray());
    }

    static string[] CleanArray(string[] source)
    {
        if (source == null || source.Length == 0) return Array.Empty<string>();

        var list = new System.Collections.Generic.List<string>(source.Length);
        for (int i = 0; i < source.Length; i++)
        {
            string value = source[i];
            if (string.IsNullOrWhiteSpace(value)) continue;
            list.Add(value.Trim());
        }

        return list.Count == 0 ? Array.Empty<string>() : list.ToArray();
    }

    static DialogueResponseBranch[] CloneBranches(DialogueResponseBranch[] source)
    {
        if (source == null || source.Length == 0)
            return Array.Empty<DialogueResponseBranch>();

        var cloned = new DialogueResponseBranch[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            DialogueResponseBranch branch = source[i];
            if (branch == null)
            {
                cloned[i] = new DialogueResponseBranch();
                continue;
            }

            cloned[i] = new DialogueResponseBranch
            {
                branchKey = branch.branchKey,
                respondsTo = CleanArray(branch.respondsTo),
                followUpEvent = (branch.followUpEvent ?? string.Empty).Trim(),
                sceneTriggers = CleanArray(branch.sceneTriggers)
            };
        }

        return cloned;
    }

    public static bool TryGetMachineResponseId(DialogueResponseBranch branch, out string responseId)
    {
        responseId = string.Empty;
        if (branch == null || branch.respondsTo == null || branch.respondsTo.Length == 0)
            return false;

        for (int i = 0; i < branch.respondsTo.Length; i++)
        {
            string id = branch.respondsTo[i];
            if (string.IsNullOrWhiteSpace(id)) continue;

            string trimmed = id.Trim();
            if (!trimmed.StartsWith("R_", StringComparison.OrdinalIgnoreCase))
                continue;

            responseId = trimmed;
            return true;
        }

        return false;
    }

    void UpdateText(string newText)
    {
        message = newText;
        if (textMesh != null)
            textMesh.text = newText;
    }

    public void PlayInflate()
    {
        if (inflateRoutine != null)
            StopCoroutine(inflateRoutine);

        inflateRoutine = StartCoroutine(InflateRoutine());
    }

    IEnumerator InflateRoutine()
    {
        float t = 0f;
        transform.localScale = Vector3.zero;

        while (t < inflateDuration)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / inflateDuration);
            float scale = inflateCurve.Evaluate(normalized);
            transform.localScale = Vector3.one * scale;
            yield return null;
        }

        transform.localScale = Vector3.one;
    }

    public string GetInteractPrompt()
    {
        if (IsHeld)
            return "Press E to Throw Bubble";

        if (IsInserted)
            return "Bubble is inserted";

        return "Press E to Pick Up";
    }

    public void Interact(PlayerInteractor interactor)
    {
        interactor.PickupItem(gameObject);
    }

    public void OnPickup(PlayerInteractor interactor)
    {
        IsInserted = false;
        IsHeld = true;
        OnPickedUp?.Invoke(this);

        if (floating != null)
            floating.SetHeldState(true);

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    public void OnDrop()
    {
        IsHeld = false;

        if (floating != null)
            floating.SetHeldState(false);

        if (rb != null)
        {
            rb.isKinematic = IsInserted;
            rb.useGravity = !IsInserted;
        }
    }

    public void Insert(Transform slotParent = null)
    {
        IsInserted = true;

        if (slotParent != null)
        {
            transform.SetParent(slotParent, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        if (floating != null)
            floating.SetHeldState(true);

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        OnInserted?.Invoke(this);
    }

    public void ReleaseFromSlot(Vector3 worldPosition)
    {
        IsInserted = false;
        transform.SetParent(null);
        transform.position = worldPosition;
        transform.rotation = Quaternion.identity;

        if (floating != null)
            floating.SetHeldState(false);

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
    }

    public void Consume()
    {
        Destroy(gameObject);
    }

    public void Prick()
    {
        OnPricked?.Invoke(this);
        Destroy(gameObject);
    }
}
