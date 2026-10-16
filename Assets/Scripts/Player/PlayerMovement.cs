using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] float walkSpeed;
    [SerializeField] float sprintSpeed;
    [SerializeField] float drag;
    [SerializeField] float jumpSpeed;
    [SerializeField] LayerMask ground;
    PlayerCamera playerCam;
    public Vector2 moveInput;
    public bool jump;
    bool crouch;
    public bool sprint;
    [SerializeField] Vector3 velocity;
    [SerializeField] Vector2 maxDstFromOrigin;
    Camera mainCamera;
    float speed;

    public Rigidbody rb;
    void Start() {
        playerCam = FindAnyObjectByType<PlayerCamera>();
        mainCamera = Camera.main;
    }

    public void Spawn(Vector3 position) {
        rb.position = position;
    }

    void Update() {

        Vector3 localHorizontalVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, transform.up);

        if(localHorizontalVelocity.magnitude > walkSpeed) {
            Vector3 clampedHorizontal = localHorizontalVelocity.normalized * speed;
            Vector3 verticalVelocity = Vector3.Project(rb.linearVelocity, transform.up);

            rb.linearVelocity = clampedHorizontal + verticalVelocity;
        }

        speed = sprint ? sprintSpeed : walkSpeed;
    }
    // Update is called once per frame
    void FixedUpdate() {

        velocity = rb.linearVelocity;

        //if(Physics.Raycast(transform.position, -transform.up, 1.1f, ground)) {
            if (jump) {
                rb.AddForce(transform.up * jumpSpeed, ForceMode.Force);
            }
        //}
        rb.AddForce((transform.forward * moveInput.y + transform.right * moveInput.x).normalized * speed, ForceMode.Impulse);

        Vector3 surfaceHorizontalVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, transform.up);
        rb.AddForce(-surfaceHorizontalVelocity * drag);
        
    }

    void OnMove(InputValue value) {
        moveInput = value.Get<Vector2>();
    }

    void OnJump(InputValue value) {
        jump = value.isPressed;
    }
    void OnCrouch(InputValue value) {
        crouch = value.isPressed;
    }
    void OnSprint(InputValue value) {
        sprint = value.isPressed;
    }

}
