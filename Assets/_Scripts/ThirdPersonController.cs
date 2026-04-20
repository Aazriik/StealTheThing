using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonController : MonoBehaviour
{
    #region Parameters

    [Header("Const Parameters")]
    private const string speedParamName = "Speed";
    private const string jumpParamName = "Jump";
    private const string groundedParamName = "Grounded";
    private const string fallingParamName = "Falling";
    private const float lookThreshold = 0.1f; // lookThreshold is to prevent jitter from micro movements.    

    [Header("Cinemachine")]
    [SerializeField] private Transform cameraTarget; // cameraTarget is a reference to the Transform that the camera will follow.
    [SerializeField] private float topClamp = 70.0f; // topClamp limits the maximum vertical rotation of the camera to prevent it from flipping over.
    [SerializeField] private float bottomClamp = -30.0f; // bottomClamp limits the minimum vertical rotation of the camera to prevent it from flipping under.

    [Header("Speed")]
    [SerializeField] private float lookSpeed = 10f; // lookSpeed determines how quickly the character rotates based on look input.
    [SerializeField] private float moveSpeed = 2f; // moveSpeed determines how fast the character moves based on input.

    [Header("Jump")]
    [SerializeField] private float jumpForce = 5f; // jumpForce determines how high the character will jump when the jump action is triggered.
    [SerializeField] private float jumpDowntime = 1f; // jumpDowntime is the delay before the character can jump again after landing.

    [Header("Grounded")]
    [SerializeField] private Transform groundCheckPoint; // groundCheckPoint is a Transform that will check if the character is grounded.
    [SerializeField] private float groundCheckRadius = 0.2f; // groundCheckRadius is the radius of the sphere used to check for ground collisions.
    [SerializeField] private LayerMask groundLayer; // groundLayer is a LayerMask that specifies which layers are considered ground.

    [Header("Component References")]
    private Animator animator;
    private Rigidbody rb;

    [Header("Input Parameters")]
    private Vector2 move; // move is a Vector2 that stores the input values for character movement.
    private Vector2 look; // look is a Vector2 that stores the input values for character looking/aiming.
    private float yaw; // yaw is the horizontal rotation of the character based on look input.
    private float pitch; // pitch is the vertical rotation of the character based on look input.
    private float currentSpeed; // currentSpeed is the current movement speed of the character, which is used to update the animator's speed parameter.
    private bool isGrounded = true; // isGrounded indicates whether the character is currently on the ground.
    private bool isRunning; // isRunning indicates whether the character is currently running based on input.
    private bool canJump = true; // canJump is a flag to prevent the character from jumping again until the jump downtime has passed after landing.

    #endregion


    #region Input Actions

    /* Move processes the movement input to calculate the target speed and direction of movement based on the camera's orientation,
     * applies smoothing to the speed for a more natural feel, and updates the character's velocity and animation parameters accordingly. */
    private void Move()
    {
        float targetSpeed = (isRunning ? moveSpeed * 2f : moveSpeed) * move.magnitude;
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.fixedDeltaTime * 8f);

        Vector3 forward = cameraTarget.forward;
        Vector3 right = cameraTarget.right;

        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = (forward * move.y + right * move.x).normalized;

        if(moveDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * 10f);

            Vector3 currentVelocity = rb.linearVelocity;
            rb.linearVelocity = new Vector3(moveDirection.x * currentSpeed, currentVelocity.y, moveDirection.z * currentSpeed);
        }
        else
        {
            Vector3 currentVelocity = rb.linearVelocity;
            rb.linearVelocity = new Vector3(0f, currentVelocity.y, 0f);
        }

        float normalizedAnimSpeed = currentSpeed / (moveSpeed * 2f);
        animator.SetFloat(speedParamName, normalizedAnimSpeed);
        animator.SetBool(fallingParamName, !isGrounded && rb.linearVelocity.y < -0.1f);
    }

    /* Jump applies an upward force to the character's Rigidbody to make it jump, triggers the jump animation,
     * and starts a coroutine to manage jump downtime and resetting the ability to jump after landing. */
    private void Jump()
    {
        if (!isGrounded || !canJump) return;
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        canJump = false;

        animator.SetTrigger(jumpParamName);

        StartCoroutine(JumpDowntimeCoroutine());
    }

    /* JumpDowntimeCoroutine manages the timing for when the character can jump again after landing.
     * It waits for a short duration after jumping, then waits until the character is grounded,
     * and finally waits for the specified jump downtime before allowing the character to jump again. */
    private IEnumerator JumpDowntimeCoroutine()
    {
        yield return new WaitForSeconds(0.25f);
        var waitForGrounded = new WaitUntil(() => isGrounded);
        yield return waitForGrounded;
        yield return new WaitForSeconds(jumpDowntime);
        canJump = true;
    }

    /* Look processes the look input to rotate the character and camera target based on the yaw and pitch calculated from the look input,
     * while applying clamping to ensure the camera does not rotate beyond natural limits. */
    private void Look()
    {
        if (look.magnitude >= lookThreshold)
        {
            float deltaTimeMultiplier = Time.deltaTime * lookSpeed;
            yaw += look.x * deltaTimeMultiplier;
            pitch -= look.y * deltaTimeMultiplier;
        }

        yaw = ClampAngle(yaw, float.MinValue, float.MaxValue);
        pitch = ClampAngle(pitch, bottomClamp, topClamp);

        cameraTarget.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    // ClampAngle ensures that the pitch rotation of the camera is clamped between the specified top and bottom limits to prevent unnatural camera angles.
    private float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
        if (lfAngle < -360f) lfAngle += 360f;
        if (lfAngle > 360f) lfAngle -= 360f;
        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }

    /* GroundedCheck uses a sphere check to determine if the character is currently grounded by checking for collisions
     * with the ground layer, and updates the animator's grounded parameter accordingly. */
    private void GroundedCheck()
    {
        isGrounded = Physics.CheckSphere(groundCheckPoint.position, groundCheckRadius, groundLayer);
        animator.SetBool(groundedParamName, isGrounded);
    }

    /* OnDrawGizmosSelected is a Unity method that is called when the object is selected in the editor,
     * and it draws a red sphere at the groundCheckPoint position with the specified radius
     * to visually represent the area being checked for grounding. */
    private void OnDrawGizmosSelected()
    {
        if (groundCheckPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(groundCheckPoint.position, groundCheckRadius);
    }

    private void OnMove(InputValue inputValue)
    {
        move = inputValue.Get<Vector2>();
    }

    private void OnJump()
    {
        Jump();
    }

    private void OnRun(InputValue inputValue)
    {
        isRunning = inputValue.isPressed;
    }

    private void OnLook(InputValue inputValue)
    {
        look = inputValue.Get<Vector2>();
    }

    #endregion


    private void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        GroundedCheck();
    }

    private void LateUpdate()
    {
        Look();
    }

    private void FixedUpdate()
    {
        Move();
    }
}
