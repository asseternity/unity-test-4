using UnityEngine;
using UnityEngine.InputSystem;

public class playerScript : MonoBehaviour
{
    [SerializeField, Range(0f, 100f)]
    float maxSpeed = 10f,
        maxAcceleration = 10f,
        maxAirAcceleration = 1f,
        maxGroundAngle = 45f,
        maxSnapSpeed = 100f;

    [SerializeField, Range(0f, 10f)]
    float jumpHeight = 2f;

    [SerializeField, Range(0f, 10)]
    int maxAirJumps = 1;

    [SerializeField, Min(0f)]
    float probeDistance = 1f;

    [SerializeField]
    LayerMask probeMask = -1;
    float minGroundDotProduct;
    int jumpPhase = 0;
    Vector3 velocity = Vector3.zero;
    Vector3 desiredVelocity = Vector3.zero;
    Vector3 contactNormal;
    bool desiredJump = false;
    bool onGround = false;
    int stepsSinceLastGrounded,
        stepsSinceLastJump;
    Rigidbody body;

    [SerializeField]
    Transform playerInputSpace = default;
    void OnValidate()
    {
        minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
    }

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        OnValidate();
    }

    bool jumpPressed = false;

    void Update()
    {
        jumpPressed = false;
        Vector2 playerInput;
        playerInput.x = Input.GetAxis("Horizontal");
        playerInput.y = Input.GetAxis("Vertical");
        playerInput = Vector2.ClampMagnitude(playerInput, 1f);

        if (playerInputSpace)
        {
            Vector3 forward = playerInputSpace.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = playerInputSpace.right;
            right.y = 0f;
            right.Normalize();
            desiredVelocity = (forward * playerInput.y + right * playerInput.x) * maxSpeed;
        }
        else
        {
            desiredVelocity = new Vector3(playerInput.x, 0f, playerInput.y) * maxSpeed;
        }
        float jumpDown = jumpButton.ReadValue<float>();
        if (jumpDown > 0f)
        {
            jumpPressed = true;
        }
        desiredJump |= jumpPressed;
    }

    void FixedUpdate()
    {
        UpdateState();
        AdjustVelocity();
        if (desiredJump)
        {
            desiredJump = false;
            Jump();
        }
        body.velocity = velocity;
        ClearState();
    }

    void ClearState()
    {
        onGround = false;
        contactNormal = Vector3.zero;
    }

    void Jump()
    {
        if (onGround || jumpPhase < maxAirJumps)
        {
            stepsSinceLastJump = 0;
            jumpPhase += 1;
            float jumpSpeed = Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight);
            float alignedSpeed = Vector3.Dot(velocity, contactNormal); // this is the dot product (middle) between the two vectors / angles
            if (alignedSpeed > 0f)
            {
                jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f);
            } // it is done to make jumps angled to the contact surface (Physics, 3.4)
            velocity += contactNormal * jumpSpeed;
        }
    }
    void OnCollisionStay(Collision collision)
    {
        EvaluateCollision(collision);
    }

    void EvaluateCollision(Collision collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;
            if (normal.y >= minGroundDotProduct)
            {
                onGround = true;
                contactNormal += normal;
            }
        }
    }

    void UpdateState()
    {
        stepsSinceLastGrounded += 1;
        stepsSinceLastJump += 1;
        velocity = body.velocity; // update the velocity variable with the current velocity of a Rigidbody component (body) attached to a game object.
        if (onGround || SnapToGround())
        {
            stepsSinceLastGrounded = 0;
            jumpPhase = 0;
            contactNormal.Normalize();
        }
        else
        {
            contactNormal = Vector3.up;
        }
    }

    Vector3 ProjectOnContactPlane(Vector3 vector)
    {
        return vector - contactNormal * Vector3.Dot(vector, contactNormal);
    }

    // Method above takes a vector (like the right or forward direction) and returns a new vector that is aligned to be parallel with the slope's surface.

    void AdjustVelocity()
    {
        Vector3 xAxis = ProjectOnContactPlane(Vector3.right).normalized;
        Vector3 zAxis = ProjectOnContactPlane(Vector3.forward).normalized;

        // we want to know how much of the current velocity is in the direction of the slope
        float currentX = Vector3.Dot(velocity, xAxis);
        float currentZ = Vector3.Dot(velocity, zAxis);

        // calculate regular acceleration, maxSpeedChange and regular incremented vector change
        float acceleration = onGround ? maxAcceleration : maxAirAcceleration;
        float maxSpeedChange = acceleration * Time.deltaTime;
        float newX = Mathf.MoveTowards(currentX, desiredVelocity.x, maxSpeedChange);
        float newZ = Mathf.MoveTowards(currentZ, desiredVelocity.z, maxSpeedChange);
        // last line updates the velocity along the slope's plane by adding the change in velocity along the xAxis and zAxis directions
        // multiplied by the difference between the new and current velocities (because that we we calculate how much speed along an axis needs to change)
        // so, xAxis and zAxis are directions, and newX minus currentX is the speed.
        // xAxis and zAxis are directions (unit vectors), and newX - currentX represents the change in speed along those directions
        velocity += xAxis * (newX - currentX) + zAxis * (newZ - currentZ);
    }

    bool SnapToGround()
    {
        if (stepsSinceLastGrounded > 1 || stepsSinceLastJump <= 2)
        {
            return false;
        }
        float speed = velocity.magnitude;
        if (speed > maxSnapSpeed)
        {
            return false;
        }
        if (
            !Physics.Raycast(
                body.position,
                Vector3.down,
                out RaycastHit hit,
                probeDistance,
                probeMask
            )
        )
        {
            return false;
        }
        if (hit.normal.y < minGroundDotProduct)
        {
            return false;
        }

        contactNormal = hit.normal;
        float dot = Vector3.Dot(velocity, hit.normal);
        if (dot > 0f)
        {
            velocity = (velocity - hit.normal * dot).normalized * speed;
        }
        return true;
    }

    public InputAction jumpButton;

    private void OnEnable()
    {
        jumpButton.Enable();
    }

    private void OnDisable()
    {
        jumpButton.Enable();
    }
}