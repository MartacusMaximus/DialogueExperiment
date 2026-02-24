using UnityEngine;
using System.Collections;
using System;

public class DialogueUI : MonoBehaviour
{
    public SpeechBubbleStack stack;
    public float defaultRevealDelay = 0.25f; 

    Coroutine dialogueRoutine;
    bool waitingForAdvance;

    SpeechBubbleView currentView;

    public void PlayDialogue(DialogueNode node)
    {
        if (dialogueRoutine != null) StopCoroutine(dialogueRoutine);
        dialogueRoutine = StartCoroutine(PlayNodeSequential(node));
    }


    /// Called by input (via DialogueSystem.TryAdvanceOrConsume)
    /// Probeert de volgende bubble te pakken
 
    public void Advance()
    {
        if (currentView != null)
        {
            currentView.SkipOrAdvance();
        }
        else
        {
            waitingForAdvance = false;
        }
    }

    IEnumerator PlayNodeSequential(DialogueNode node)
    {
        foreach (var bubbleData in node.bubbles)
        {
            float speed = Mathf.Max(1f, bubbleData.revealSpeed);
            float autoFade = Mathf.Max(0f, bubbleData.autoFadeTime);
            float postReveal = Mathf.Max(0f, bubbleData.postRevealDelay);

            currentView = stack.SpawnBubble(bubbleData.text, speed, autoFade, postReveal);

            bool revealed = false;
            Action<SpeechBubbleView> onReveal = (v) => { if (v == currentView) revealed = true; };
            currentView.OnRevealComplete += onReveal;
            yield return new WaitUntil(() => revealed);
            currentView.OnRevealComplete -= onReveal;


            if (postReveal > 0f)
                yield return new WaitForSeconds(postReveal);

            // Gaan we klikken of wachten
            if (bubbleData.expectedResponse == ExpectedResponseType.TimerOnly)
            {
                if (autoFade > 0f)
                {
                    bool finished = false;
                    Action<SpeechBubbleView> onLife2 = (v) => { if (v == currentView) finished = true; };
                    currentView.OnLifeComplete += onLife2;
                    yield return new WaitUntil(() => finished);
                    currentView.OnLifeComplete -= onLife2;
                }
            }
            else if (bubbleData.expectedResponse == ExpectedResponseType.None)
            {
                if (autoFade > 0f)
                {
                    bool finished = false;
                    Action<SpeechBubbleView> onLife = (v) => { if (v == currentView) finished = true; };
                    currentView.OnLifeComplete += onLife;
                    yield return new WaitUntil(() => finished);
                    currentView.OnLifeComplete -= onLife;
                }
                else
                {
                    // press interact to continue
                    waitingForAdvance = true;
                    yield return new WaitUntil(() => waitingForAdvance == false);
                }
            }
            else
            {
                waitingForAdvance = true;
                yield return new WaitUntil(() => waitingForAdvance == false);
            }

            // wacht tot de bubbel klaar is, daarna faden
            if (currentView != null)
            {
                bool finished2 = false;
                Action<SpeechBubbleView> onLife3 = (v) => { if (v == currentView) finished2 = true; };
                currentView.OnLifeComplete += onLife3;

                currentView.StartFadeImmediate(); 
                yield return new WaitUntil(() => finished2);
                currentView.OnLifeComplete -= onLife3;
            }

            currentView = null;

            if (bubbleData.followUp != null)
            {
                yield return PlayNodeSequential(bubbleData.followUp);
            }
        }

        dialogueRoutine = null;
    }
}
