using UnityEngine;
using System.Collections;

public class DialogueSystem : MonoBehaviour
{
    public static DialogueSystem Instance;

    public DialogueUI uiController;
    public Camera mainCamera;
    public float cameraLerpTime = 0.6f;

    bool isActive = false;
    Transform oldCameraParent;
    Vector3 oldLocalPos;
    Quaternion oldLocalRot;

    Coroutine cameraRoutine;

    void Awake()
    {
        Instance = this;
        if (mainCamera == null) mainCamera = Camera.main;
    }

    public void StartDialogue(DialogueNode node, DialogueSpeaker speaker)
    {
        if (isActive) return;

        isActive = true;
        if (mainCamera != null && speaker.dialogueCameraAnchor != null)
        {
            oldCameraParent = mainCamera.transform.parent;
            oldLocalPos = mainCamera.transform.localPosition;
            oldLocalRot = mainCamera.transform.localRotation;

            mainCamera.transform.SetParent(null);

            if (cameraRoutine != null) StopCoroutine(cameraRoutine);
            cameraRoutine = StartCoroutine(LerpCameraToAnchor(mainCamera.transform, speaker.dialogueCameraAnchor, cameraLerpTime));
        }

        uiController.PlayDialogue(node);
    }

    IEnumerator LerpCameraToAnchor(Transform cam, Transform anchor, float duration)
    {
        if (cam == null || anchor == null)
            yield break;

        Vector3 startPos = cam.position;
        Quaternion startRot = cam.rotation;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float f = Mathf.SmoothStep(0f, 1f, t / duration);
            cam.position = Vector3.Lerp(startPos, anchor.position, f);
            cam.rotation = Quaternion.Slerp(startRot, anchor.rotation, f);
            yield return null;
        }

        cam.position = anchor.position;
        cam.rotation = anchor.rotation;
    }

    IEnumerator LerpCameraBack(float duration)
    {
        if (mainCamera == null) yield break;
        Transform cam = mainCamera.transform;

        Vector3 startPos = cam.position;
        Quaternion startRot = cam.rotation;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float f = Mathf.SmoothStep(0f, 1f, t / duration);
            cam.position = Vector3.Lerp(startPos, oldCameraParent.TransformPoint(oldLocalPos), f);
            cam.rotation = Quaternion.Slerp(startRot, oldCameraParent.rotation * oldLocalRot, f);
            yield return null;
        }

        // reparent camera
        cam.SetParent(oldCameraParent);
        cam.localPosition = oldLocalPos;
        cam.localRotation = oldLocalRot;
    }

    public void EndDialogue()
    {
        if (!isActive) return;
        isActive = false;

        // restore camera
        if (cameraRoutine != null) StopCoroutine(cameraRoutine);
        StartCoroutine(LerpCameraBack(cameraLerpTime));
    }

    public void ForceExit()
    {
        if (!isActive) return;

        uiController?.StopAllCoroutines();
        EndDialogue();
    }

    public bool TryAdvanceOrConsume()
    {
        if (!isActive) return false;

        uiController?.Advance();
        return true;
    }
}
