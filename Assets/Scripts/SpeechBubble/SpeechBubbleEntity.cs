using UnityEngine;
using TMPro;
using System;
using System.Collections;


[RequireComponent(typeof(Collider))]
public class SpeechBubbleEntity : MonoBehaviour, IInteractable, IHoldable
{
    [Header("References")]
    public TextMeshPro textMesh; // assign in prefab
    public string speakerName;
    public string message;
    public DialogueNode sourceNode;
    public int bubbleIndex = -1;

    public event Action<SpeechBubbleEntity> OnPricked;
    public event Action<SpeechBubbleEntity> OnPickedUp;
    public event Action<SpeechBubbleEntity> OnInserted;

    public bool IsHeld { get; private set; }
    public bool IsInserted { get; private set; }

    [Header("Inflate Animation")]
    public float inflateDuration = 0.35f;
    public AnimationCurve inflateCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    Coroutine inflateRoutine;
    FloatingBubble floating;

    void Awake()
    {
        floating = GetComponent<FloatingBubble>();
    }

    public void Initialize(SpeechBubble data, DialogueNode originNode, int index)
    {
        sourceNode = originNode;
        bubbleIndex = index;
        speakerName = data.speakerName;
        message = data.text;

        if (textMesh != null)
            textMesh.text = message;
    }

    public void PlayInflate()
    {
        if (inflateRoutine != null)
            StopCoroutine(inflateRoutine);

        inflateRoutine = StartCoroutine(InflateRoutine());
    }

    public string GetInteractPrompt()
    {
        return "Press E to Read";
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

    public void Interact(PlayerInteractor interactor)
    {
        interactor.PickupItem(gameObject);
    }

    public void OnPickup(PlayerInteractor interactor)
    {
        if (floating != null)
            floating.SetHeldState(true);
    }

    public void OnDrop()
    {
        if (floating != null)
            floating.SetHeldState(false);
    }

    public void Pickup()
    {
        IsHeld = true;
        OnPickedUp?.Invoke(this);
    }

    public void Insert()
    {
        IsInserted = true;
        OnInserted?.Invoke(this);
    }

    public void Prick()
    {
        OnPricked?.Invoke(this);
        Destroy(gameObject);
    }

    public void UpdateText(string newText)
    {
        message = newText;
        if (textMesh != null) textMesh.text = newText;
    }
}
