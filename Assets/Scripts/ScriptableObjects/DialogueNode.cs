using UnityEngine;

public enum DialogueBubbleType
{
    Monologue,
    Question,
    ResponseRequired
}

public enum ExpectedResponseType
{
    None,
    DialogueChoice,
    PerformAction,
    TimerOnly
}

[CreateAssetMenu(menuName = "Dialogue/Dialogue Node")]
public class DialogueNode : ScriptableObject
{
    public SpeechBubble[] bubbles;
}


public interface IDialogueResponseListener
{
    void OnDialogueResponse(string responseID);
}

[System.Serializable]
public class SpeechBubble
{
    public string speakerName;
    [TextArea(2, 6)]
    public string text;

    public DialogueBubbleType bubbleType;
    public ExpectedResponseType expectedResponse;

    public float autoFadeTime = 5f;

    public float revealSpeed = 60f;

    public float postRevealDelay = 0.25f;

    public DialogueNode followUp;
}

