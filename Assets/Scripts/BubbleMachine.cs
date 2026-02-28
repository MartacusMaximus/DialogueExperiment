using UnityEngine;
using System.Collections;

public class BubbleMachine : MonoBehaviour
{
    [Header("References")]
    public MicrophoneSlot inputSlot;            
    public GameObject orderPaperPrefab;         
    public SpeechBubbleEntity responseBubblePrefab; 
    public Transform outputSpawn;               
    public float processingDuration = 0.9f;     

    void OnEnable()
    {
        if (inputSlot != null)
            inputSlot.OnBubbleInserted += HandleBubbleInserted;
    }

    void OnDisable()
    {
        if (inputSlot != null)
            inputSlot.OnBubbleInserted -= HandleBubbleInserted;
    }

    void HandleBubbleInserted(SpeechBubbleEntity bubble)
    {
        StartCoroutine(ProcessBubbleRoutine(bubble));
    }

    IEnumerator ProcessBubbleRoutine(SpeechBubbleEntity bubble)
    {
        if (bubble == null) yield break;

        yield return new WaitForSeconds(processingDuration);

        if (bubble.isOrder && bubble.orderData != null)
        {
            CreateOrderPaper(bubble.orderData);
            Destroy(bubble.gameObject);
            yield break;
        }

        // If bubble requires a response (expectedResponse != None), create response bubbles
        if (bubble.expectedResponse != ExpectedResponseType.None)
        {
            CreateResponseBubble($"Acknowledged: {bubble.speakerName}", bubble.sourceNode);
            Destroy(bubble.gameObject);
            yield break;
        }

        Destroy(bubble.gameObject);
    }

    public void CreateOrderPaper(Order order)
    {
        if (orderPaperPrefab == null) return;
        Vector3 spawnPos = outputSpawn != null ? outputSpawn.position : transform.position + Vector3.forward * 0.5f;
        var paperGo = Instantiate(orderPaperPrefab, spawnPos, Quaternion.identity);
        var paper = paperGo.GetComponent<OrderPaper>();
        if (paper != null)
            paper.Initialize(order);
    }

    public SpeechBubbleEntity CreateResponseBubble(string text, DialogueNode originNode = null)
    {
        if (responseBubblePrefab == null) return null;

        Vector3 spawnPos = outputSpawn != null ? outputSpawn.position : transform.position + Vector3.forward * 0.5f;
        var go = Instantiate(responseBubblePrefab.gameObject, spawnPos, Quaternion.identity, null).GetComponent<SpeechBubbleEntity>();

        var fakeSpeech = new SpeechBubble()
        {
            speakerName = "Player",
            text = text,
            expectedResponse = ExpectedResponseType.None,
            autoFadeTime = 0,
            revealSpeed = 60,
            postRevealDelay = 0
        };
        go.Initialize(fakeSpeech, originNode, -1);
        return go;
    }
}