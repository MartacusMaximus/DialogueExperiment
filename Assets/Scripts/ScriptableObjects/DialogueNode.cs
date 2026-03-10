using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/Dialogue Node")]
public class DialogueNode : ScriptableObject
{
    [Tooltip("Logical event key (normally the Event Name group from CSV).")]
    public string eventGroupId;

    [Tooltip("Bubbles that can be spawned for this dialogue event.")]
    public SpeechBubble[] bubbles = System.Array.Empty<SpeechBubble>();
}

[System.Serializable]
public class SpeechBubble
{
    [Tooltip("Unique CSV event id (for example: D_Example02 or B_Response01).")]
    public string bubbleId;

    public string speakerName;

    [TextArea(2, 6)]
    public string text;

    [Tooltip("Branch rules from CSV columns like RespondsTo01/FollowUp01.")]
    public DialogueResponseBranch[] responseBranches = System.Array.Empty<DialogueResponseBranch>();

    [Tooltip("Legacy fallback response IDs. Prefer responseBranches for new data.")]
    public string[] appropriateResponses = System.Array.Empty<string>();

    [Tooltip("Trigger names fired when this bubble is delivered into a speaker microphone.")]
    public string[] sceneTriggers = System.Array.Empty<string>();

    [Tooltip("Optional Event/Bubble id to spawn after this bubble is accepted by a speaker.")]
    public string followUpEvent;

    public float autoFadeTime = 0f;
    public float revealSpeed = 60f;
    public float postRevealDelay = 0.25f;
}

[System.Serializable]
public class DialogueResponseBranch
{
    [Tooltip("Optional branch key, for example 01, 02, 03.")]
    public string branchKey;

    [Tooltip("Valid bubble IDs for this branch (respondsto list).")]
    public string[] respondsTo = System.Array.Empty<string>();

    [Tooltip("Follow up event/bubble ID for this branch.")]
    public string followUpEvent;

    [Tooltip("Optional scene trigger IDs for this branch. Use S_ prefix.")]
    public string[] sceneTriggers = System.Array.Empty<string>();
}
