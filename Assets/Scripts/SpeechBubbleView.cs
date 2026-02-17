using UnityEngine;
using TMPro;
using System;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class SpeechBubbleView : MonoBehaviour
{
    public TMP_Text tmpText;
    public CanvasGroup canvasGroup;
    public float fadeDuration = 0.35f;

    public event Action<SpeechBubbleView> OnRevealComplete;
    public event Action<SpeechBubbleView> OnLifeComplete;

    enum State { Idle, Typing, RevealedWaiting, Fading, Finished }
    State state = State.Idle;

    string fullMessage;
    float charsPerSecond;
    float autoFadeTime;
    float postRevealDelay;

    Coroutine typingCoroutine;
    Coroutine autoFadeCoroutine;
    Coroutine fadeCoroutine;

    void Reset()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    public void Initialize(string message, float cps, float autoFade, float postRevealDelaySeconds = 0f)
    {
        StopAllCoroutines();
        typingCoroutine = null;
        autoFadeCoroutine = null;
        fadeCoroutine = null;

        fullMessage = message ?? string.Empty;
        charsPerSecond = Mathf.Max(1f, cps);
        autoFadeTime = Mathf.Max(0f, autoFade);
        postRevealDelay = Mathf.Max(0f, postRevealDelaySeconds);

        tmpText.text = string.Empty;
        canvasGroup.alpha = 1f;
        state = State.Typing;

        typingCoroutine = StartCoroutine(TypewriterRoutine());
    }

    IEnumerator TypewriterRoutine()
    {
        float delay = 1f / charsPerSecond;
        int i = 0;
        while (i < fullMessage.Length)
        {
            tmpText.text += fullMessage[i++];
            yield return new WaitForSeconds(delay);
        }

        state = State.RevealedWaiting;
        OnRevealComplete?.Invoke(this);

        if (postRevealDelay > 0f)
            yield return new WaitForSeconds(postRevealDelay);

        if (autoFadeTime > 0f)
        {
            autoFadeCoroutine = StartCoroutine(AutoFadeRoutine(autoFadeTime));
        }
    }

    IEnumerator AutoFadeRoutine(float wait)
    {
        yield return new WaitForSeconds(wait);
        StartFadeImmediate();
    }


    public void SkipOrAdvance()
    {
        if (state == State.Typing)
        {
            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
                typingCoroutine = null;
            }
            tmpText.text = fullMessage;
            state = State.RevealedWaiting;
            OnRevealComplete?.Invoke(this);

            if (autoFadeTime > 0f)
            {
                if (autoFadeCoroutine != null) StopCoroutine(autoFadeCoroutine);
                autoFadeCoroutine = StartCoroutine(AutoFadeRoutine(autoFadeTime));
            }

            return;
        }

        if (state == State.RevealedWaiting)
        {
            if (autoFadeTime > 0f)
            {
                StartFadeImmediate();
            }
            else
            {
                FinishLifeImmediate();
            }
            return;
        }
    }


    public void StartFadeImmediate()
    {
        if (state == State.Fading || state == State.Finished) return;

        // cancel other coroutines (typing/auto)
        if (typingCoroutine != null) { StopCoroutine(typingCoroutine); typingCoroutine = null; }
        if (autoFadeCoroutine != null) { StopCoroutine(autoFadeCoroutine); autoFadeCoroutine = null; }

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeOutAndFinish());
    }

    IEnumerator FadeOutAndFinish()
    {
        state = State.Fading;
        float t = 0f;
        float start = canvasGroup.alpha;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, 0f, t / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        state = State.Finished;
        OnLifeComplete?.Invoke(this);

        Destroy(gameObject);
    }


    void FinishLifeImmediate()
    {
        if (state == State.Finished) return;

        // stop everything
        StopAllCoroutines();
        state = State.Finished;

        OnLifeComplete?.Invoke(this);
        Destroy(gameObject);
    }
}
