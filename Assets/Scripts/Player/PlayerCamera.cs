using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCamera : MonoBehaviour {
    [SerializeField] float mouseSensitivity;
    [SerializeField] Transform player;
    public Vector3 offset;
    [SerializeField] float maxInteractDst;
    [SerializeField] LayerMask interactable;
    PlayerMovement playerMovement;
    Vector2 lookInput;
    float xRotation = 0f;
    float yRotation = 0f;

    void Start() {
        Cursor.lockState = CursorLockMode.Locked;
        playerMovement = FindAnyObjectByType<PlayerMovement>();
    }

    void Update() {
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;

        yRotation += mouseX;
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -89f, 89f);

        player.Rotate(player.up, mouseX, Space.World);

        transform.rotation = player.rotation * Quaternion.Euler(xRotation, 0f, 0f);

        transform.position = player.position + (player.rotation * offset);

    }

    void OnLook(InputValue value) {
        lookInput = value.Get<Vector2>();
    }
}
