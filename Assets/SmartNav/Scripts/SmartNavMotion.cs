using UnityEngine;

using Unity.MLAgents.Sensors;

using LaForge.MapGenerator;

public class SmartNavMotion : MonoBehaviour, ISmartNavMotion
{
    [Header("Movement Parameters")]
    [Tooltip("Agent run speed, recommended value is 11.")]
    public float agentRunAcceleration = 11.0f;
    [Tooltip("Agent jump force, recommended value is 10.")]
    public float agentJumpForce = 10.0f;
    [Tooltip("This is a downward force applied when falling to make jumps look less floaty recommended value is 20.")]
    public float fallingForce = 20f;
    [Tooltip("Physic layers checked to consider the player grounded")]
    public LayerMask RayCastLayerMask = -1;

    public float jumpPadOffset = 10;


    // State computation variables
    private int jumpingTime;
    private int doubleJumpingTime;
    private bool canDoubleJump;
    private bool isAgentGroundedRayCast;
    private bool isAgentGroundedCapsuleCast;
    private Vector3 characterVelocity;

    public void ResetState()
    {
        jumpingTime = 0;
        doubleJumpingTime = 0;
        canDoubleJump = true;
        isAgentGroundedRayCast = true;
        isAgentGroundedCapsuleCast = true;
        characterVelocity = new Vector3(0.0f, 0.0f, 0.0f);
    }


    // cached components
    CharacterController m_CharacterController;
    Unity.MLAgents.DecisionRequester m_decisionRequester;

    public void OnEnable()
    {
        m_CharacterController = GetComponent<CharacterController>();
        m_decisionRequester = GetComponent<Unity.MLAgents.DecisionRequester>();
    }

#region Raycasts and helpers
     
    // pre-allocated hit buffer - could be static if only single threaded
    private RaycastHit[] hitGroundRayCastsHitBuffer = new RaycastHit[10];

    // Gets the center point of the bottom hemisphere of the character controller capsule    
    private Vector3 GetCapsuleBottomHemisphere()
    {
        // We get the point at the bottom of the character, and add the radius to find the center of the bottom sphere
        return transform.position + m_CharacterController.center + Vector3.down * (m_CharacterController.height / 2f - m_CharacterController.radius);
    }

    // Gets the center point of the top hemisphere of the character controller capsule    
    private Vector3 GetCapsuleTopHemisphere()
    {
        // We get the point at the top of the character, and add the radius to find the center of the top sphere
        return transform.position + m_CharacterController.center + Vector3.up * (m_CharacterController.height / 2f - m_CharacterController.radius);
    }

    public bool IsOnGroundCapsuleCast()
    {
        // NOTE: this CapsuleCast could possibly be replaced by something like
        // return m_CharacterController.collisionFlags != CollisionFlags.None;

        int numCollisions = Physics.CapsuleCastNonAlloc(
            GetCapsuleBottomHemisphere(),
            GetCapsuleTopHemisphere(),
            m_CharacterController.radius,
            Vector3.down,
            hitGroundRayCastsHitBuffer,
            0.05f + m_CharacterController.skinWidth,
            RayCastLayerMask,
            QueryTriggerInteraction.Ignore);

        return (numCollisions != 0);
    }

    public bool IsOnGroundRayCast()
    {
        RaycastHit hit;
        bool isHit = Physics.Raycast(m_CharacterController.bounds.center, Vector3.down, out hit, m_CharacterController.bounds.extents.y + 0.1f, RayCastLayerMask);
        return isHit;
    }

#endregion


    public bool IsOnGround()
    {
        return isAgentGroundedRayCast;
    }


    public void CollectObservations(VectorSensor sensor)
    {
        isAgentGroundedRayCast = IsOnGroundRayCast();
        isAgentGroundedCapsuleCast = IsOnGroundCapsuleCast();

        // Ground check (Bool, size 1)
        float groundCheck = isAgentGroundedRayCast ? 1.0f : 0.0f;
        sensor.AddObservation(groundCheck);

        // Can double jump (Bool, size 1)
        sensor.AddObservation(canDoubleJump);
    }


    private void Jump()
    {
        characterVelocity.y += agentJumpForce;
        jumpingTime = m_decisionRequester.DecisionPeriod;
    }

    public void OnJumpPadEnter(JumpPad jumpPad)
    {
        float jumpPadForce = Mathf.Sqrt(2.0f * fallingForce * (jumpPad.jumpPadProperties.Height + jumpPadOffset));
        characterVelocity.y = jumpPadForce; // equivalent to zeroing current y velocity and adding Vector3.up*jumpPadForce
        jumpingTime = m_decisionRequester.DecisionPeriod;
    }


    public void MoveAgent(float[] act)
    {
        bool jumpMotion = (float)act[0] > 0.0f ? true : false;
        float forwardAction = Mathf.Clamp((float)act[1], -0.6f, 1.0f) * 1.1f;  // We multiply by 1.1 to give him some margin to reach his max speed of 1 by going forward
        float rotateAction = (float)act[2];
        float strafeAction = act[3] * 0.6f;

        Vector3 dirToGo = (transform.forward * forwardAction + transform.right * strafeAction);
        dirToGo = dirToGo.magnitude > 1f ? dirToGo.normalized : dirToGo;

        // The velocity of the character this frame
        characterVelocity.x = dirToGo.x * agentRunAcceleration;
        characterVelocity.z = dirToGo.z * agentRunAcceleration;

        if (isAgentGroundedRayCast)
        {
            // If we're on the ground and didn't just start a jump, reset vertical speed
            if (jumpingTime <= 0)
                characterVelocity.y = 0;

            // Reset the double jump ability
            canDoubleJump = true;
        }
        else
        {
            // Apply gravity if in the air
            characterVelocity.y -= fallingForce * Time.deltaTime;

            // When the agent is on a slope that is too steep to climb, 
            // rayCast is false but capsuleCast is true
            // In these cases we deactivate the doubleJump so that the agent 
            // cannot climb by going backward and double jumping.
            if (isAgentGroundedCapsuleCast)
            {
                canDoubleJump = false;
            }
        }

        // Apply jump motion
        if (jumpMotion)
        {
            if ((jumpingTime <= 0) && isAgentGroundedRayCast)
            {
                // Jump start with zero vertical velocity
                characterVelocity.y = 0;
                Jump();
                doubleJumpingTime = m_decisionRequester.DecisionPeriod;
            }
            else
            {
                if ((canDoubleJump) && (!isAgentGroundedCapsuleCast) && (doubleJumpingTime <= 0))
                {
                    // Jump start with zero vertical velocity
                    characterVelocity.y = 0;
                    Jump();
                    canDoubleJump = false;
                }
            }
        }

        transform.Rotate(transform.up, rotateAction * 180f * Time.deltaTime);

        m_CharacterController.Move(characterVelocity * Time.deltaTime);
        jumpingTime -= 1;
        doubleJumpingTime -= 1;
    }
}
