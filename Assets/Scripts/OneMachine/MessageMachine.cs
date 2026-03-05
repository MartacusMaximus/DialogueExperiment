using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MessageMachine : MonoBehaviour, IInteractable
{
    [Header("References")]
    public SpeechBubbleEntity bubblePrefab;
    public Transform bubbleSpawnPoint;
    public Transform insertPoint;

    [Header("Option Buttons")]
    public OptionButton yesButtonPrefab;
    public OptionButton noButtonPrefab;
    public Transform optionSpawnPoint;

    SpeechBubbleEntity currentInsertedBubble;
    OptionButton spawnedYes;
    OptionButton spawnedNo;

    [Header("Settings")]
    public float spawnDelay = 0.6f;

    bool hasNewMessage = true;

    List<MessageBubbleData> testConversation;

    void Start()
    {
        BuildTestConversation();
        
    }

    void BuildTestConversation()
    {
        testConversation = new List<MessageBubbleData>()
        {
            new MessageBubbleData
            {
                text = "Hello. We have a situation.",
                requiresResponse = false
            },
            new MessageBubbleData
            {
                text = "Do you accept the task?",
                requiresResponse = true,
                options = new List<string>() { "Yes", "No" }
            }
        };
    }

    public string GetInteractPrompt()
    {
        if (hasNewMessage)
            return "Press E - New Message";

        return "Press E - Insert Bubble";
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (interactor.GetHeldGameObject() != null)
        {
            GameObject held = interactor.TakeHeldItemForInsertion();
            SpeechBubbleEntity bubble = held.GetComponent<SpeechBubbleEntity>();

            if (bubble != null)
                HandleInsertedBubble(bubble);

            return;
        }

        if (hasNewMessage)
        {
            StartCoroutine(SpawnConversation());
            hasNewMessage = false;
        }
    }

    IEnumerator SpawnConversation()
    {
        foreach (var msg in testConversation)
        {
            yield return new WaitForSeconds(spawnDelay);

            Vector3 pos = bubbleSpawnPoint.position + Random.insideUnitSphere * 0.4f;
            pos.y = bubbleSpawnPoint.position.y;

            var bubble = Instantiate(bubblePrefab, pos, Quaternion.identity);
            bubble.InitializePrototype(msg);
            bubble.PlayInflate();
        }
    }

    void HandleInsertedBubble(SpeechBubbleEntity bubble)
    {
        bubble.transform.position = insertPoint.position;

        if (bubble.messageData == null)
        {
            Destroy(bubble.gameObject);
            return;
        }

        if (bubble.messageData.requiresResponse &&
            bubble.messageData.options != null &&
            bubble.messageData.options.Count == 2)
        {
            currentInsertedBubble = bubble;
            SpawnOptions();
            return;
        }

        Destroy(bubble.gameObject);
    }

    void SpawnOptions()
    {
        Vector3 basePos = optionSpawnPoint.position;

        spawnedYes = Instantiate(yesButtonPrefab, basePos + Vector3.left * 0.5f, Quaternion.identity);
        spawnedYes.Initialize(this, "Yes");

        spawnedNo = Instantiate(noButtonPrefab, basePos + Vector3.right * 0.5f, Quaternion.identity);
        spawnedNo.Initialize(this, "No");
    }

    public void OptionSelected(string selected)
    {
        if (currentInsertedBubble == null)
            return;

        currentInsertedBubble.messageData.chosenOption = selected;

        SpawnResponseBubble(selected);

        Destroy(currentInsertedBubble.gameObject);

        if (spawnedYes != null) Destroy(spawnedYes.gameObject);
        if (spawnedNo != null) Destroy(spawnedNo.gameObject);

        currentInsertedBubble = null;
    }

    void SpawnResponseBubble(string response)
    {
        var data = new MessageBubbleData
        {
            text = "Response sent: " + response,
            requiresResponse = false
        };

        Vector3 pos = bubbleSpawnPoint.position + Vector3.right;
        var bubble = Instantiate(bubblePrefab, pos, Quaternion.identity);
        bubble.InitializePrototype(data);
        bubble.PlayInflate();
    }
}