using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float gravity = -9.81f;

    [Header("Look")]
    public float mouseSensitivity = 0.1f;
    public Transform cameraPivot;
    public float maxLookAngle = 80f;

    [Header("Interaction")]
    public float interactRange = 3f;

    private CharacterController controller;
    public PlayerInputActions input;
    private Vector2 moveInput;
    private Vector2 lookInput;

    private float yVelocity;
    private float xRotation;

    public bool inputLocked;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        input = new PlayerInputActions();

        input.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        input.Player.Move.canceled += _ => moveInput = Vector2.zero;

        input.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        input.Player.Look.canceled += _ => lookInput = Vector2.zero;
        
        input.Player.Interact.performed += ctx =>
        {
            if (DialogueSystem.Instance != null && DialogueSystem.Instance.TryAdvanceOrConsume())
            {
                return;
            }

           TryInteract();
        };

        input.Player.Previous.performed += ctx =>
        {
            DialogueSystem.Instance?.ForceExit();
        };


        LockCursor(true);
    }

    void OnEnable()
    {
        input.Enable();
    }

    void OnDisable()
    {
        input.Disable();
    }

    void Update()
    {
        if (inputLocked)
            return;

        Look();
        Move();
    }

    void Look()
    {
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);

        cameraPivot.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    void Move()
    {
        Vector3 move =
            transform.right * moveInput.x +
            transform.forward * moveInput.y;

        if (controller.isGrounded && yVelocity < 0f)
            yVelocity = -2f;

        yVelocity += gravity * Time.deltaTime;

        Vector3 velocity = move * moveSpeed + Vector3.up * yVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    void TryInteract()
    {
        Debug.Log("E to Interact");
        if (inputLocked)
            return;

        Ray ray = new Ray(cameraPivot.position, cameraPivot.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
        {
            if (hit.collider.TryGetComponent<IInteractable>(out var interactable))
            {
                interactable.Interact(this);
            }
        }
    }

    public void LockInput(bool locked)
    {
        inputLocked = locked;
        LockCursor(!locked);
    }

    void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
