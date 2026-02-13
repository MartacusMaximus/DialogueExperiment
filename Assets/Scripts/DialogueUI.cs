using UnityEngine;
using System.Collections;
using System;

public class DialogueUI : MonoBehaviour
{
    public SpeechBubbleStack stack;
    public float defaultRevealDelay = 1.0f; 

    Coroutine dialogueRoutine;

    private bool waitingForAdvance;

    public void PlayDialogue(DialogueNode node)
    {
        if (dialogueRoutine != null) StopCoroutine(dialogueRoutine);
        dialogueRoutine = StartCoroutine(PlayNodeSequential(node));
    }

    public void Advance()
    {
        waitingForAdvance = false;
    }

    IEnumerator PlayNodeSequential(DialogueNode node)
    {
        foreach (var bubbleData in node.bubbles)
        {
            float speed = Mathf.Max(1f, bubbleData.revealSpeed);
            var view = stack.SpawnBubble(bubbleData.text, speed, bubbleData.autoFadeTime);

            // WAIT
            bool revealed = false;
            Action<SpeechBubbleView> onReveal = (v) => revealed = true;
            view.OnRevealComplete += onReveal;

            yield return new WaitUntil(() => revealed);

            view.OnRevealComplete -= onReveal;

            // Decide waiting
            if (bubbleData.expectedResponse == ExpectedResponseType.TimerOnly)
            {
                float t = Mathf.Max(0f, bubbleData.autoFadeTime);
                yield return new WaitForSeconds(t);
            }
            else if (bubbleData.expectedResponse == ExpectedResponseType.None)
            {
                if (bubbleData.autoFadeTime > 0f)
                {
                    // wait until bubble death
                    bool finished = false;
                    Action<SpeechBubbleView> onLife = (v) => finished = true;
                    view.OnLifeComplete += onLife;
                    yield return new WaitUntil(() => finished);
                    view.OnLifeComplete -= onLife;
                }
                else
                {
                    waitingForAdvance = true;
                    yield return new WaitUntil(() => waitingForAdvance == false);
                }
            }
            else 
            {
                waitingForAdvance = true;
                yield return new WaitUntil(() => waitingForAdvance == false);
            }

            // if the bubble still exists and isn't fading, bubble fades
            if (view != null && !view.IsFinished)
            {
                yield return view.FadeOutAndFinish();
            }

            if (bubbleData.followUp != null)
            {
                yield return PlayNodeSequential(bubbleData.followUp);
            }
        }

        // inform DialogueSystem
        DialogueSystem.Instance?.EndDialogue();
        dialogueRoutine = null;
    }
}
