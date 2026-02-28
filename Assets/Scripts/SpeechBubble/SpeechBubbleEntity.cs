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
    public string speakerName;
    public string message;
    public DialogueNode sourceNode;
    public int bubbleIndex = -1;

    [Header("Response")]
    public ExpectedResponseType expectedResponse = ExpectedResponseType.None;
    public string responseID;

    [Header("Order")]
    public bool isOrder = false;
    public Order orderData;

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

    public void Initialize(SpeechBubble data, DialogueNode originNode, int index)
    {
        sourceNode = originNode;
        bubbleIndex = index;

        speakerName = data.speakerName;
        message = data.text;
        expectedResponse = data.expectedResponse;

        responseID = Guid.NewGuid().ToString();

        isOrder = false;
        orderData = null;

        UpdateText(message);
    }

    public void InitializeOrder(Order order, DialogueNode originNode = null)
    {
        isOrder = true;
        orderData = order;
        sourceNode = originNode;

        speakerName = order.orderName;
        message = FormatOrderBrief(order);

        expectedResponse = ExpectedResponseType.None;
        responseID = Guid.NewGuid().ToString();

        UpdateText(message);
    }

    string FormatOrderBrief(Order o)
    {
        if (o == null) return "Order";

        return $"{o.orderName}\nReward: {o.rewardedGold} Gold";
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
            return "Press E to Throw";

        return "Press E to Pick Up";
    }

    public void Interact(PlayerInteractor interactor)
    {
        interactor.PickupItem(gameObject);
    }

    public void OnPickup(PlayerInteractor interactor)
    {
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
            rb.isKinematic = false;
            rb.useGravity = true;
        }
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
}