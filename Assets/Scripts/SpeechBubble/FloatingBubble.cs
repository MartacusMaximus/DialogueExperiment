using UnityEngine;

public class FloatingBubble : MonoBehaviour
{
    [Header("Motion")]
    public float bobAmplitude = 0.15f;
    public float bobFrequency = 1.0f;
    public float orbitRadius = 0.25f;
    public float orbitSpeed = 20f;
    public Vector3 orbitAxis = Vector3.up;

    [Header("Facing")]
    public bool faceCamera = true;
    public Camera targetCamera;

    Vector3 startLocalPos;
    float orbitAngle;

    bool isHeld = false;

    void Awake()
    {
        startLocalPos = transform.localPosition;

        if (targetCamera == null && Camera.main != null)
            targetCamera = Camera.main;

        orbitAngle = Random.Range(0f, 360f);
    }

    void OnEnable()
    {
        startLocalPos = transform.localPosition;
    }

    void Update()
    {
        if (isHeld)
            return;

        float t = Time.time;

        float bob = Mathf.Sin(t * bobFrequency * Mathf.PI * 2f) * bobAmplitude;

        orbitAngle += orbitSpeed * Time.deltaTime;
        float rad = Mathf.Deg2Rad * orbitAngle;

        Vector3 orbitOffset =
            (transform.right * Mathf.Cos(rad) +
             transform.forward * Mathf.Sin(rad)) * orbitRadius;

        transform.localPosition = startLocalPos + Vector3.up * bob + orbitOffset;

        if (faceCamera && targetCamera != null)
        {
            transform.rotation =
                Quaternion.LookRotation(transform.position - targetCamera.transform.position, Vector3.up);
        }
    }

    public void SetHeldState(bool held)
    {
        isHeld = held;

        if (held)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }
        else
        {
            startLocalPos = transform.localPosition;
        }
    }
}