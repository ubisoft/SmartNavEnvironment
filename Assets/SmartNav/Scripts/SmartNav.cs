using Microsoft.VisualBasic;
//Put this script on your blue cube.

using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEngine.Events;
using UnityEngine.Assertions;
using LaForge.MapGenerator;

public class SmartNav : Agent
{
    // Shared parameters with the Python script
    [Header("Shared Parameters")]
    [Tooltip("Size of the vector observations excluding RAYCASTS.")]
    public int vectorObservationSize = 17;
    [Tooltip("Size of the unused vector observation at the end of the vector observation. This is only used to pass additional info to python. This will be filtered out and won't be fed to the network.")]
    public int unusedVectorEndObservation = 4;
    [Tooltip("Whether to add the jump pad position to the observation (size 4)")]
    public bool observeJumpPads = false;

    [Header("Raycast Rarameters")]
    [Tooltip("Physic layers checked against")]
    public LayerMask RayCastLayerMask = -1;
    [Tooltip("Length of raycasts")]
    public float rayCastLength = 17.0f;
    [Tooltip("Horizontal field of view")]
    public float horizontalFov = 180.0f;
    [Tooltip("Vertical field of view")]
    public float verticalFov = 180.0f;
    [Tooltip("Number of raycast channels, e.g., regular, lava, water...")]
    public int numRayCastChannels = 3;
    [Tooltip("Visualizes raycasts - set to false unless debugging")]
    public bool debugRayCasts = false;

    [Header("Square Diamond Raycast Parameters")]
    [Tooltip("Number of rays and columns in the rotated square n_rays = size*size")]
    public int squareDiamondSize = 8;

    [Header("Reward Parameters")]
    [Tooltip("Reward received for winning the episode.")]
    public float winReward = 1.0f;
    [Tooltip("Reward received for failing during the episode.")]
    public float loseReward = -1.0f;
    [Tooltip("The penalty received every step to motivate the agent to finish earlier.")]
    public float timeStepReward = -0.0005f;
    [Tooltip("The reward received if the agent gets closer to the goal w.r.t. its previous closest position.")]
    public float rewardGettingClosertoGoal = 1f;
    [Tooltip("Distance threshold for determining if the agent has reached its goal.")]
    public float goalWinThreshold = 1.0f;

    [Header("Scenario Parameters")]
    [Tooltip("Number of steps not progressing after which the episode is over.")]
    public int maxStepsNotProgressing = 1500;

    [Header("Prefabs")]
    [Tooltip("Goal object prefab")]
    public GameObject goalPrefab;

    [Header("Normalization constants")]
    public float playGroundSize = 40;
    public float maxVelocity = 50.0f;
    public float maxAcceleration = 500.0f;

    [Header("Inspector debug")]
    public int agentId = 0;
    public GameObject goal;
    BehaviorParameters m_behaviourParameters;
    CharacterController m_CharacterController;
    ISmartNavMotion m_motion;

    // State computation variables
    private bool firstTimeStep = true;
    private float[] previousAction;
    private Vector3 previousVelocity;

    // Reward computation variables
    private Vector3 startPosition;
    private int nbStepsNotProgressing = 0;
    private float prevDistanceToGoal = Mathf.Infinity;
    private float closestDistanceToGoal = Mathf.Infinity;


    // Debug variables
    private float success = 0.0f;
    private float m_previousSuccess;
    private int episodeCounter = 0;
    private static float successRate = 0.0f;
    private static float[] previousSuccesses = new float[200];
    private static int currentSuccessArrayIndex = 0;

    public delegate void EpisodeBeginCallback(SmartNav agent);
    public static EpisodeBeginCallback OnEpisodeBeginCallback;
    public MapSlot currentMapSlot = null;
    public Vector3 m_GoalPosition;
    public Vector3 m_SpawnPosition;

    // Raycast buffer and size
    float[] rayCastBuffer;
    int rayCastSize;

    (float h, float v)[] rayCastAngles; // precomputed raycast angles

    public Vector3 GoalPosition;
    public Vector3 SpawnPosition;
    private float goalDistance;
    private bool lineToggle = false;
    private LineRenderer m_lineRenderer;

    private bool shouldDie = false;
    public Vector3[] SpawnGoal;

    private Animator m_animator;
    private int m_AnimSpeedHash = Animator.StringToHash("Speed");
    private int m_AnimJumpHash = Animator.StringToHash("Jump");

    public override void Initialize()
    {
        base.Initialize();
        m_CharacterController = GetComponent<CharacterController>();
        m_behaviourParameters = GetComponent<BehaviorParameters>();
        m_lineRenderer = GetComponent<LineRenderer>();
        m_animator = GetComponentInChildren<Animator>();
        m_lineRenderer.enabled = lineToggle;

        m_motion = GetComponent<ISmartNavMotion>();
        if (m_motion == null)
        {
            Debug.LogWarning("No ISmartNavMotion component found! Adding default SmartNavMotion. Please update your prefab!", this);
            m_motion = gameObject.AddComponent<SmartNavMotion>() as SmartNavMotion;
        }

        goal = Instantiate(goalPrefab, GoalPosition, Quaternion.identity);

        rayCastSize = squareDiamondSize * squareDiamondSize;
        rayCastBuffer = new float[rayCastSize * numRayCastChannels];
        rayCastAngles = new (float h, float v)[rayCastSize];
        CacheSquareDiamondRayCastAngles();
    }

    public override void OnEpisodeBegin()
    {
        OnEpisodeBeginCallback?.Invoke(this);
        updateAvgSuccessRate();
        ResetInternalVariables();

        Vector3[] SpawnGoal = currentMapSlot.SmartNavMap.GetNextSpawnGoal();
        SpawnPosition = SpawnGoal[0] + new Vector3(0.0f, m_CharacterController.height * transform.localScale.y / 2, 0.0f);
        transform.localPosition = SpawnPosition;
        transform.localRotation = GetStartOrientationFromPosition(SpawnPosition);

        GoalPosition = SpawnGoal[1] + new Vector3(0.0f, m_CharacterController.height * transform.localScale.y / 2, 0.0f);
        goal.transform.localPosition = GoalPosition;

        goalDistance = Vector3.Distance(SpawnPosition, GoalPosition);
        // WARNING: The characterController isn't aware of the teleportation if no physics update is made, which causes the agent to de-teleport. 
        // Calling Physics.SyncTransforms to force the synching.
        Physics.SyncTransforms();

        m_lineRenderer.SetPosition(1, goal.transform.position);

    }

    public Quaternion GetStartOrientationFromPosition(Vector3 position)
    {
        return Quaternion.AngleAxis(position.x, Vector3.up);
    }

    private void Reset()
    {
        episodeCounter += 1;
        EndEpisode();
    }

    public void Kill()
    {
        shouldDie = true;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        firstTimeStep = false;

        // ******************************** WARNING **************************************
        // The following parts of the state are accessed in python. 
        // Make sure that the indexes to access them are correct in SetSharedProperties.
        // *******************************************************************************

        // Raycasts
        CollectSquareDiamondRayCasts();
        sensor.AddObservation(rayCastBuffer);
        var goalRelPos = goal.transform.position - transform.position;
        var localGoalRelPos = transform.InverseTransformDirection(goalRelPos);
        var agentVelocityOwnRef = transform.InverseTransformDirection(m_CharacterController.velocity);
        var agentAccelerationOwnRef = transform.InverseTransformDirection(computeAcceleration());

        // Local position of the goal, given as radius and direction, size 4
        sensor.AddObservation(localGoalRelPos.magnitude / playGroundSize);
        sensor.AddObservation(localGoalRelPos.normalized);

        // Local velocity of the agent (vector, size 3)
        sensor.AddObservation(agentVelocityOwnRef / maxVelocity);

        // Local acceleration of the agent (vector, size 3)
        sensor.AddObservation(agentAccelerationOwnRef / maxAcceleration);

        // Previous action of the agent (vector, size 3 or 4 depending on action size)
        sensor.AddObservation(previousAction);

        // Motion specific observations (check concrete implementation of ISmartNavMotion for size)
        m_motion.CollectObservations(sensor);

        // Number of steps the agent has been stuck
        sensor.AddObservation((float)nbStepsNotProgressing / maxStepsNotProgressing);

        // Jump Pad position (vector size 4)
        if (observeJumpPads)
        {
            var jumpPadPosition = currentMapSlot.SmartNavMap.FindNearestJumpPad(transform.localPosition);
            var jumpPadRelPos = jumpPadPosition - transform.localPosition;
            var localJumpPadRelPos = transform.InverseTransformDirection(jumpPadRelPos);
            sensor.AddObservation(localJumpPadRelPos.magnitude / playGroundSize);
            sensor.AddObservation(localJumpPadRelPos.normalized);
        }
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        AddReward(timeStepReward);

        float[] vectorAction = actionBuffers.ContinuousActions.Array;

        if (firstTimeStep)
        {
            vectorAction[0] = 0.0f;
        }

        m_motion.MoveAgent(vectorAction);
        previousAction = actionBuffers.ContinuousActions.Array;

        float distanceToGoal = (transform.position - goal.transform.position).magnitude;

        if ((!firstTimeStep) && (prevDistanceToGoal != Mathf.Infinity))
        {
            float denseReward = 0;
            if (distanceToGoal < closestDistanceToGoal)
            {
                // Did the agent get closer to the goal than it ever did before
                float rewardDistanceGoal = rewardGettingClosertoGoal * Mathf.Max(closestDistanceToGoal - distanceToGoal, 0f);
                rewardDistanceGoal /= playGroundSize;
                denseReward += rewardDistanceGoal;

                closestDistanceToGoal = distanceToGoal;
            }
            else
            {
                nbStepsNotProgressing += 1;
                if (nbStepsNotProgressing > maxStepsNotProgressing)
                {
                    success = 0.0f;
                    Reset();
                }
            }
            AddReward(denseReward);
        }
        if ((distanceToGoal < goalWinThreshold) && (!firstTimeStep))
        {
            success = 1.0f;
            SetReward(winReward);
            Reset();
        }
        else if ((shouldDie || (transform.position.y < currentMapSlot.Position[1] - 25.0f)) && (!firstTimeStep))
        {
            shouldDie = false;
            success = 0.0f;
            SetReward(loseReward);
            Reset();
        }

        if ((!firstTimeStep))
        {
            if (startPosition.magnitude == 0f)
            {
                startPosition = transform.position;
                closestDistanceToGoal = (transform.position - goal.transform.position).magnitude;
            }
            prevDistanceToGoal = distanceToGoal;
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var action = actionsOut.ContinuousActions;
        float horizontalMove = Input.GetAxis("Horizontal");
        float verticalMove = Input.GetAxis("Vertical");
        action[2] = horizontalMove;
        action[1] = verticalMove;
        if (Input.GetKey(KeyCode.Q))
        {
            action[3] = -1f;
        }
        else if (Input.GetKey(KeyCode.E))
        {
            action[3] = 1f;
        }
        else
        {
            action[3] = 0f;
        }
        action[0] = Input.GetAxis("Jump") > 0 ? 1.0f : 0.0f;
        return;
    }

    private void ResetInternalVariables()
    {
        // State computation variables
        m_motion.ResetState();
        previousVelocity = new Vector3(0.0f, 0.0f, 0.0f);
        firstTimeStep = true;
        previousAction = new float[m_behaviourParameters.BrainParameters.ActionSpec.NumContinuousActions];

        // Reward computation variables
        startPosition = new Vector3(0.0f, 0.0f, 0.0f);
        nbStepsNotProgressing = 0;
        prevDistanceToGoal = Mathf.Infinity;
        closestDistanceToGoal = Mathf.Infinity;

        // Debug variables
        success = 0.0f;

        // Reset rayCast buffer
        rayCastBuffer = new float[rayCastSize * numRayCastChannels];
    }

    private void updateAvgSuccessRate()
    {
        float toRemove = previousSuccesses[currentSuccessArrayIndex];
        successRate += (success - toRemove) / previousSuccesses.Length;
        previousSuccesses[currentSuccessArrayIndex] = success;
        currentSuccessArrayIndex = (currentSuccessArrayIndex + 1) % previousSuccesses.Length;
    }

    private Vector3 computeAcceleration()
    {
        Vector3 acceleration;
        acceleration = (m_CharacterController.velocity - previousVelocity) / Time.fixedDeltaTime;
        previousVelocity = m_CharacterController.velocity;
        return acceleration;
    }

    float CalulateRaycastDistance(RaycastHit hit, bool isHit)
    {
        if (isHit)
        {
            return 1.0f - hit.distance / rayCastLength;
        }
        return 0.0f;
    }

    void StoreRayCastInBuffer(RaycastHit hit, bool isHit, float distance, int ray)
    {
        int hitChannel = 0;
        if (isHit && numRayCastChannels > 1)
        {
            if (TryGetComponent<Ground>(out var curGround))
            {
                hitChannel = (int)curGround.GroundProperties.Type + 1;
            }
        }
        for (int rayChannel = 0; rayChannel < numRayCastChannels; rayChannel++)
        {
            int curChannelOffset = rayCastSize * rayChannel;
            if (rayChannel != hitChannel)
            {
                rayCastBuffer[ray + curChannelOffset] = 0.0f;
            }
            else
            {
                rayCastBuffer[ray + curChannelOffset] = distance;
            }
        }
    }

    private void CollectSquareDiamondRayCasts()
    {
        for (var ray = 0; ray < rayCastSize; ray++)
        {
            var horizontalAngle = rayCastAngles[ray].h;
            var verticalAngle = rayCastAngles[ray].v;
            Vector3 rayDirection = Quaternion.AngleAxis(horizontalAngle, this.transform.up) *
                Quaternion.AngleAxis(verticalAngle, this.transform.right) * this.transform.forward;
            RaycastHit hit;
            var isHit = Physics.Raycast(this.transform.position, rayDirection, out hit, rayCastLength, RayCastLayerMask);
            float distance = CalulateRaycastDistance(hit, isHit);
            StoreRayCastInBuffer(hit, isHit, distance, ray);
            
            if (debugRayCasts)
            {
                //var colors = new List<Color> { Color.black, Color.cyan, Color.green, Color.blue, Color.gray, Color.white, Color.red, Color.yellow };
                Debug.DrawRay(this.transform.position, rayCastLength * rayDirection.normalized);
            }
        }
    }

    private void CacheSquareDiamondRayCastAngles()
    {
        var totalLayers = 2 * squareDiamondSize - 1;
        float verticalIncrement = verticalFov / ((float)totalLayers + 1.0f);

        for (var ray = 0; ray < rayCastSize; ray++)
        {
            var row = ray % squareDiamondSize + ray / squareDiamondSize;
            var col = 0;
            for (int i = 0; i < ray; i++)
            {
                // count the number of columns for this row, this is a really bad way to do this but I could not think of a better one...
                if (i % squareDiamondSize + i / squareDiamondSize == row)
                {
                    col++;
                }
            }
            var n_rays = row + 1;
            if (row + 1 > squareDiamondSize)
            {
                n_rays = 2 * squareDiamondSize - row - 1;
            }
            float horizontalIncrement = horizontalFov / ((float)n_rays + 1.0f);
            float horizontalAngle = -horizontalFov / 2.0f + ((float)col + 1.0f) * horizontalIncrement;
            float verticalAngle = -verticalFov / 2.0f + ((float)row + 1.0f) * verticalIncrement;

            rayCastAngles[ray] = (horizontalAngle, verticalAngle);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            lineToggle = !lineToggle;
            m_lineRenderer.enabled = lineToggle;

            Debug.Log("L pressed");
        }

        if (lineToggle)
        {
            // should this stay in OnActionRecieved?
            m_lineRenderer.SetPosition(0, transform.position);
        }

        Vector3 relativeVelocity = transform.InverseTransformDirection(m_CharacterController.velocity); 
        Vector2 horizontalVelocity = new Vector2(relativeVelocity.x, relativeVelocity.z);
        float horizontalSpeed = horizontalVelocity.sqrMagnitude * relativeVelocity.normalized.z;
        m_animator?.SetFloat(m_AnimSpeedHash, horizontalSpeed / 121f);
        m_animator?.SetBool(m_AnimJumpHash, !m_motion.IsOnGround());

        // NOTE: m_motion.IsOnGround is only updated at the frequency of observations
        // if higher resolution animation changes are desired, compute them here
    }
}
