using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class SceneTriggerListener : MonoBehaviour
{
    public enum TriggerActionType
    {
        AnimatorTrigger,
        InstantiatePrefab
    }

    [Serializable]
    public class TriggerAction
    {
        public string triggerId;
        public TriggerActionType actionType = TriggerActionType.AnimatorTrigger;

        [Header("Animator")]
        public Animator animator;
        public string animatorTrigger = "Open";

        [Header("Instantiation")]
        public GameObject prefab;
        public Transform spawnOverride;
        public Vector3 spawnOffset;
        public bool useBubblePosition = true;
        public bool parentToListener = false;
        public float destroyAfterSeconds = 0f;

        [Header("Queue")]
        public float delayBeforeAction = 0f;
    }

    [Header("Actions")]
    public List<TriggerAction> actions = new List<TriggerAction>();

    readonly Queue<QueuedAction> pending = new Queue<QueuedAction>();
    Coroutine processRoutine;
    bool isBound;

    struct QueuedAction
    {
        public TriggerAction action;
        public DialogueSpeaker sourceSpeaker;
        public SpeechBubbleEntity sourceBubble;
    }

    void OnEnable()
    {
        Bind();
    }

    void OnDisable()
    {
        Unbind();
        pending.Clear();
        if (processRoutine != null)
        {
            StopCoroutine(processRoutine);
            processRoutine = null;
        }
    }

    void Bind()
    {
        if (isBound)
            return;

        if (DialogueSystem.Instance == null)
        {
            StartCoroutine(DelayedBind());
            return;
        }

        DialogueSystem.Instance.SceneTriggerRequested += HandleSceneTrigger;
        isBound = true;
    }

    IEnumerator DelayedBind()
    {
        yield return null;
        if (!isBound && DialogueSystem.Instance != null)
            Bind();
    }

    void Unbind()
    {
        if (!isBound)
            return;

        if (DialogueSystem.Instance != null)
            DialogueSystem.Instance.SceneTriggerRequested -= HandleSceneTrigger;

        isBound = false;
    }

    void HandleSceneTrigger(string triggerId, DialogueSpeaker speaker, SpeechBubbleEntity bubble)
    {
        if (string.IsNullOrWhiteSpace(triggerId) || actions == null || actions.Count == 0)
            return;

        for (int i = 0; i < actions.Count; i++)
        {
            TriggerAction action = actions[i];
            if (action == null || string.IsNullOrWhiteSpace(action.triggerId))
                continue;

            if (!string.Equals(action.triggerId.Trim(), triggerId.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;

            pending.Enqueue(new QueuedAction
            {
                action = action,
                sourceSpeaker = speaker,
                sourceBubble = bubble
            });
        }

        if (processRoutine == null && pending.Count > 0)
            processRoutine = StartCoroutine(ProcessQueue());
    }

    IEnumerator ProcessQueue()
    {
        while (pending.Count > 0)
        {
            QueuedAction queued = pending.Dequeue();
            TriggerAction action = queued.action;

            if (action.delayBeforeAction > 0f)
                yield return new WaitForSeconds(action.delayBeforeAction);

            ExecuteAction(action, queued.sourceSpeaker, queued.sourceBubble);
        }

        processRoutine = null;
    }

    void ExecuteAction(TriggerAction action, DialogueSpeaker speaker, SpeechBubbleEntity bubble)
    {
        if (action == null)
            return;

        switch (action.actionType)
        {
            case TriggerActionType.AnimatorTrigger:
                if (action.animator == null || string.IsNullOrWhiteSpace(action.animatorTrigger))
                    return;
                action.animator.ResetTrigger(action.animatorTrigger);
                action.animator.SetTrigger(action.animatorTrigger);
                break;

            case TriggerActionType.InstantiatePrefab:
                if (action.prefab == null)
                    return;

                Vector3 spawnPos = ResolveSpawnPosition(action, speaker, bubble);
                Quaternion spawnRot = action.spawnOverride != null ? action.spawnOverride.rotation : Quaternion.identity;
                Transform parent = action.parentToListener ? transform : null;

                GameObject instance = Instantiate(action.prefab, spawnPos, spawnRot, parent);
                if (action.destroyAfterSeconds > 0f)
                    Destroy(instance, action.destroyAfterSeconds);
                break;
        }
    }

    Vector3 ResolveSpawnPosition(TriggerAction action, DialogueSpeaker speaker, SpeechBubbleEntity bubble)
    {
        Vector3 basePos;

        if (action.spawnOverride != null)
            basePos = action.spawnOverride.position;
        else if (action.useBubblePosition && bubble != null)
            basePos = bubble.transform.position;
        else if (speaker != null)
            basePos = speaker.transform.position;
        else
            basePos = transform.position;

        return basePos + action.spawnOffset;
    }
}
