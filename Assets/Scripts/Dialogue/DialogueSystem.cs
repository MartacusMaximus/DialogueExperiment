using UnityEngine;
using System.Collections.Generic;
using System.Collections;


public class DialogueSystem : MonoBehaviour
{
    public static DialogueSystem Instance;

    [Header("Bubble Prefab")]
    public SpeechBubbleEntity bubblePrefab;      // prefab (assign)
    public Transform bubbleParent;               // optional parent
    public float spawnRadius = 1.2f;
    public float verticalOffset = 1.1f;
    public int triesForPlacement = 12;

    public float spawnDelayBetweenBubbles = 0.25f;
    // tracking active bubbles (optional)
    private readonly List<SpeechBubbleEntity> activeBubbles = new List<SpeechBubbleEntity>();

    void Awake()
    {
        Instance = this;
    }

    public void SpawnSpeechBubbles(DialogueNode node, DialogueSpeaker speaker)
    {
        if (node == null || bubblePrefab == null) return;

        StartCoroutine(SpawnSequence(node, speaker));
    }

    IEnumerator SpawnSequence(DialogueNode node, DialogueSpeaker speaker)
    {
        Transform anchor = speaker != null && speaker.spawnAnchor != null
            ? speaker.spawnAnchor
            : speaker.transform;

        Vector3 basePos = anchor.position + Vector3.up * verticalOffset;

        for (int i = 0; i < node.bubbles.Length; i++)
        {
            SpeechBubble data = node.bubbles[i];

            Vector3 pos = FindSpawnPosition(basePos, spawnRadius, triesForPlacement);

            SpeechBubbleEntity go =
                Instantiate(
                    bubblePrefab.gameObject,
                    pos,
                    Quaternion.identity,
                    bubbleParent != null ? bubbleParent : anchor
                ).GetComponent<SpeechBubbleEntity>();

            go.Initialize(data, node, i);

            // Start at zero scale
            go.transform.localScale = Vector3.zero;

            // Add float behavior if missing
            if (go.GetComponent<FloatingBubble>() == null)
                go.gameObject.AddComponent<FloatingBubble>();

            go.PlayInflate();

            activeBubbles.Add(go);

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

    public void NotifyBubbleDestroyed(SpeechBubbleEntity bubble)
    {
        if (activeBubbles.Contains(bubble)) activeBubbles.Remove(bubble);
    }

    public IEnumerable<SpeechBubbleEntity> GetActiveBubbles() => activeBubbles;
}
