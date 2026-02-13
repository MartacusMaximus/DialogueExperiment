using UnityEngine;
using TMPro;
using System;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class SpeechBubbleView : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text tmpText;
    public CanvasGroup canvasGroup;

    [Header("Fade")]
    public float fadeDuration = 0.35f;

    public bool IsRevealed { get; private set; } = false;
    public bool IsFinished { get; private set; } = false;

    public event Action<SpeechBubbleView> OnRevealComplete;
    public event Action<SpeechBubbleView> OnLifeComplete;

    Coroutine typeRoutine;
    Coroutine lifeRoutine;

    void Reset()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    public void Initialize(string message, float charsPerSecond, float autoFadeTime)
    {
        StopAllCoroutines();
        tmpText.text = string.Empty;
        canvasGroup.alpha = 1f;
        IsRevealed = false;
        IsFinished = false;

        typeRoutine = StartCoroutine(TypewriterRoutine(message, charsPerSecond, autoFadeTime));
    }

    IEnumerator TypewriterRoutine(string message, float cps, float autoFadeTime)
    {
        if (cps <= 0f) cps = 60f; // safety
        float delay = 1f / cps;

        int i = 0;
        while (i < message.Length)
        {
            tmpText.text += message[i];
            i++;
            yield return new WaitForSeconds(delay);
        }

        IsRevealed = true;
        OnRevealComplete?.Invoke(this);

        if (autoFadeTime > 0f)
        {
            lifeRoutine = StartCoroutine(AutoLifeRoutine(autoFadeTime));
        }
    }

    IEnumerator AutoLifeRoutine(float wait)
    {
        yield return new WaitForSeconds(wait);
        yield return FadeOutAndFinish();
    }

    public IEnumerator FadeOutAndFinish()
    {
        float t = 0f;
        float start = canvasGroup.alpha;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, 0f, t / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        IsFinished = true;
        OnLifeComplete?.Invoke(this);

        Destroy(gameObject);
    }

    public void ForceFinishInstant()
    {
        if (typeRoutine != null) StopCoroutine(typeRoutine);
        tmpText.text = tmpText.text + "";
        if (!IsRevealed)
        {
            IsRevealed = true;
            OnRevealComplete?.Invoke(this);
        }
    }
}
