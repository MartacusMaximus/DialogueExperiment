using UnityEngine;
using Random = UnityEngine.Random;
using System.Collections.Generic;
using System.Collections;
using System;


public class DialogueSystem : MonoBehaviour
{
    public static DialogueSystem Instance;

    [Header("Bubble Prefab")]
    public SpeechBubbleEntity bubblePrefab;
    public Transform bubbleParent;
    public float spawnRadius = 1.2f;
    public float verticalOffset = 1.1f;
    public int triesForPlacement = 12;
    public float spawnDelayBetweenBubbles = 0.25f;

    [Header("Flow")]
    [Tooltip("If false, speakers spawn one bubble per interaction/follow-up event.")]
    public bool spawnEntireNodeAtOnce = false;

    [Header("Library")]
    [Tooltip("Optional nodes that should always be available for response lookup.")]
    public DialogueNode[] dialogueLibrary = Array.Empty<DialogueNode>();

    readonly List<SpeechBubbleEntity> activeBubbles = new List<SpeechBubbleEntity>();
    readonly HashSet<DialogueNode> registeredNodes = new HashSet<DialogueNode>();
    readonly Dictionary<string, DialogueNode> nodesByGroup = new Dictionary<string, DialogueNode>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, BubbleLookup> bubblesById = new Dictionary<string, BubbleLookup>(StringComparer.OrdinalIgnoreCase);

    public event Action<string, DialogueSpeaker, SpeechBubbleEntity> SceneTriggerRequested;

    struct BubbleLookup
    {
        public DialogueNode node;
        public int bubbleIndex;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        RebuildRegistry();

        var speakers = FindObjectsOfType<DialogueSpeaker>();
        for (int i = 0; i < speakers.Length; i++)
        {
            RegisterNode(speakers[i].dialogueNode);
        }
    }

    public void RebuildRegistry()
    {
        registeredNodes.Clear();
        nodesByGroup.Clear();
        bubblesById.Clear();

        if (dialogueLibrary == null) return;

        for (int i = 0; i < dialogueLibrary.Length; i++)
        {
            RegisterNode(dialogueLibrary[i]);
        }
    }

    public void RegisterNode(DialogueNode node)
    {
        if (node == null) return;
        if (!registeredNodes.Add(node)) return;

        RegisterGroupAlias(node.eventGroupId, node);
        RegisterGroupAlias(node.name, node);

        if (node.bubbles == null) return;

        for (int i = 0; i < node.bubbles.Length; i++)
        {
            SpeechBubble bubble = node.bubbles[i];
            if (bubble == null) continue;

            if (string.IsNullOrWhiteSpace(bubble.bubbleId))
                bubble.bubbleId = $"{NormalizeId(node.name)}_{i:00}";

            string key = NormalizeId(bubble.bubbleId);
            if (string.IsNullOrEmpty(key))
                continue;

            if (!bubblesById.ContainsKey(key))
            {
                bubblesById[key] = new BubbleLookup
                {
                    node = node,
                    bubbleIndex = i
                };
            }
            else
            {
                Debug.LogWarning($"Duplicate bubble id '{bubble.bubbleId}' in node '{node.name}'. Keeping first occurrence.");
            }
        }
    }

    void RegisterGroupAlias(string groupName, DialogueNode node)
    {
        string key = NormalizeId(groupName);
        if (string.IsNullOrEmpty(key)) return;
        nodesByGroup[key] = node;
    }

    public void SpawnSpeechBubbles(DialogueNode node, DialogueSpeaker speaker, int startIndex = 0)
    {
        if (node == null || bubblePrefab == null) return;
        RegisterNode(node);

        StartCoroutine(SpawnSequence(node, speaker, Mathf.Max(0, startIndex)));
    }

    public bool SpawnByEventId(string eventOrBubbleId, DialogueSpeaker speaker)
    {
        if (string.IsNullOrWhiteSpace(eventOrBubbleId)) return false;
        string key = NormalizeId(eventOrBubbleId);

        if (TryGetBubbleLookup(key, out var lookup, out var bubble))
        {
            SpawnSpeechBubbles(lookup.node, speaker, lookup.bubbleIndex);
            return true;
        }

        if (nodesByGroup.TryGetValue(key, out var node) && node != null)
        {
            SpawnSpeechBubbles(node, speaker);
            return true;
        }

        return false;
    }

    IEnumerator SpawnSequence(DialogueNode node, DialogueSpeaker speaker, int startIndex)
    {
        Transform anchor = speaker != null && speaker.spawnAnchor != null
            ? speaker.spawnAnchor
            : (speaker != null ? speaker.transform : transform);

        Vector3 basePos = anchor.position + Vector3.up * verticalOffset;

        if (node.bubbles == null || node.bubbles.Length == 0)
            yield break;

        int firstIndex = Mathf.Clamp(startIndex, 0, node.bubbles.Length - 1);
        int lastIndex = spawnEntireNodeAtOnce ? node.bubbles.Length - 1 : firstIndex;

        for (int i = firstIndex; i <= lastIndex; i++)
        {
            SpeechBubble data = node.bubbles[i];
            if (data == null) continue;

            Vector3 pos = FindSpawnPosition(basePos, spawnRadius, triesForPlacement);

            SpeechBubbleEntity go =
                Instantiate(
                    bubblePrefab.gameObject,
                    pos,
                    Quaternion.identity,
                    bubbleParent != null ? bubbleParent : anchor
                ).GetComponent<SpeechBubbleEntity>();

            go.Initialize(data, node, i, speaker);

            go.transform.localScale = Vector3.zero;

            if (go.GetComponent<FloatingBubble>() == null)
                go.gameObject.AddComponent<FloatingBubble>();

            go.PlayInflate();
            activeBubbles.Add(go);
            speaker?.OnBubbleSpawned(go);

            yield return new WaitForSeconds(spawnDelayBetweenBubbles);
        }
    }

    Vector3 FindSpawnPosition(Vector3 center, float radius, int tries)
    {
        for (int i = 0; i < tries; i++)
        {
            Vector2 circle = Random.insideUnitCircle * radius;
            Vector3 candidate = center + new Vector3(circle.x, Random.Range(-0.1f, 0.3f), circle.y);
            if (!Physics.CheckSphere(candidate, 0.15f))
                return candidate;
        }
        // fallback
        return center + Random.onUnitSphere * (radius * 0.5f);
    }

    public bool TryGetBubbleById(string bubbleId, out SpeechBubble bubble)
    {
        bubble = null;
        if (!TryGetBubbleLookup(NormalizeId(bubbleId), out var lookup, out var found))
            return false;

        bubble = found;
        return true;
    }

    bool TryGetBubbleLookup(string normalizedId, out BubbleLookup lookup, out SpeechBubble bubble)
    {
        lookup = default;
        bubble = null;

        if (string.IsNullOrEmpty(normalizedId))
            return false;

        if (!bubblesById.TryGetValue(normalizedId, out lookup))
            return false;

        if (lookup.node == null || lookup.node.bubbles == null)
            return false;

        if (lookup.bubbleIndex < 0 || lookup.bubbleIndex >= lookup.node.bubbles.Length)
            return false;

        bubble = lookup.node.bubbles[lookup.bubbleIndex];
        return bubble != null;
    }

    public SpeechBubbleEntity SpawnBubbleFromReference(string bubbleId, Transform spawnTransform)
    {
        if (bubblePrefab == null) return null;

        Vector3 spawnPos = spawnTransform != null
            ? spawnTransform.position
            : transform.position + Vector3.forward * 0.75f;

        var go = Instantiate(
            bubblePrefab.gameObject,
            spawnPos,
            Quaternion.identity,
            bubbleParent
        ).GetComponent<SpeechBubbleEntity>();

        string normalized = NormalizeId(bubbleId);
        if (TryGetBubbleLookup(normalized, out var lookup, out var bubbleData))
        {
            go.Initialize(bubbleData, lookup.node, lookup.bubbleIndex);
        }
        else
        {
            go.InitializeGenerated(
                bubbleId,
                "Response",
                $"Response: {bubbleId}"
            );
        }

        go.transform.localScale = Vector3.zero;
        if (go.GetComponent<FloatingBubble>() == null)
            go.gameObject.AddComponent<FloatingBubble>();
        go.PlayInflate();

        activeBubbles.Add(go);
        return go;
    }

    public void InvokeSceneTriggers(string[] sceneTriggers, DialogueSpeaker sourceSpeaker, SpeechBubbleEntity sourceBubble)
    {
        if (sceneTriggers == null || sceneTriggers.Length == 0) return;

        for (int i = 0; i < sceneTriggers.Length; i++)
        {
            string trigger = sceneTriggers[i];
            if (string.IsNullOrWhiteSpace(trigger)) continue;

            string trimmed = trigger.Trim();
            if (!trimmed.StartsWith("S_", StringComparison.OrdinalIgnoreCase))
                continue;

            Debug.Log($"Dialogue trigger fired: {trimmed}");
            SceneTriggerRequested?.Invoke(trimmed, sourceSpeaker, sourceBubble);
        }
    }

    static string NormalizeId(string value)
    {
        return (value ?? string.Empty).Trim();
    }

    public void NotifyBubbleDestroyed(SpeechBubbleEntity bubble)
    {
        if (activeBubbles.Contains(bubble)) activeBubbles.Remove(bubble);
    }

    public IEnumerable<SpeechBubbleEntity> GetActiveBubbles() => activeBubbles;
}
