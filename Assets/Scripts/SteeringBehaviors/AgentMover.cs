using System.Numerics;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

/// <summary>
/// This scripts moves its prefab following steering behaviours components.
/// </summary>
public class AgentMover : MonoBehaviour
{
    [Header("WIRING:")]
    [Tooltip("Steering behaviour component that will return movement vectors.")]
    public SteeringBehavior steeringBehavior;
    [Tooltip("This prefab's RigidBody to apply movement vectors over it.")]
    [SerializeField] private Rigidbody2D rigidBody;

    [Header("CONFIGURATION:")]
    [Tooltip("Movement will now surpass this maximum speed.")]
    [SerializeField] private float maximumSpeed;
    [Tooltip("Speed at which we consider agent should stop.")]
    [SerializeField] private float stopSpeed;
    [Tooltip("Movement rotation will not surpass this maximum rotational speed (degress).")]
    [SerializeField] private float maximumRotationalSpeed;
    [Tooltip("Maximum acceleration for this agent.")]
    [SerializeField] private float maximumAcceleration;
    [Tooltip("Maximum deceleration for this agent.")]
    [SerializeField] private float maximumDeceleration;
    
    /// <summary>
    /// This agent current speed
    /// </summary>
    public float CurrentSpeed { get; private set;}
    
    /// <summary>
    /// This agent maximum speed.
    /// </summary>
    public float MaximumSpeed => maximumSpeed;
    
    private SteeringBehaviorArgs _behaviorArgs;

    private SteeringBehaviorArgs GetSteeringBehaviorArgs()
    {
        return new SteeringBehaviorArgs(
            gameObject, 
            rigidBody.velocity, 
            maximumSpeed, 
            stopSpeed,
            maximumRotationalSpeed,
            maximumAcceleration,
            maximumDeceleration,
            0);
    }

    private void Awake()
    {
        _behaviorArgs = GetSteeringBehaviorArgs();
    }

    private void FixedUpdate()
    {
        _behaviorArgs.CurrentVelocity = rigidBody.velocity;
        _behaviorArgs.DeltaTime = Time.fixedDeltaTime;
        SteeringOutput steeringOutput = steeringBehavior.GetSteering(_behaviorArgs);
        rigidBody.velocity = steeringOutput.Linear;
        if (steeringOutput.Angular == 0 && rigidBody.velocity != Vector2.zero)
        {
            transform.up = rigidBody.velocity;
        }
        else
        {
            transform.eulerAngles = new Vector3(transform.eulerAngles.x, 
                transform.eulerAngles.y, 
                transform.eulerAngles.z + steeringOutput.Angular);
        }
        CurrentSpeed = rigidBody.velocity.magnitude;
        // Debug.Log(CurrentSpeed);
    }

    /// <summary>
    /// Get velocity vector updated with current steering and clamped by maximum speed.
    /// </summary>
    /// <returns>New velocity vector (older plus steering).</returns>
    private Vector2 GetNewVelocity(SteeringOutput newSteeringOutput, float delta)
    {
        Vector2 newVelocity = rigidBody.velocity + newSteeringOutput.Linear * delta;
        newVelocity = ClampVector2(newVelocity, 0, maximumSpeed);
        return newVelocity;
    }

    /// <summary>
    /// <p>Clamp linear acceleration of an steering output.</p>
    /// <p><b>Angular acceleration</b> is not clamped yet.</p>
    /// </summary>
    /// <param name="steeringOutput">Steering output to clamp.</param>
    /// <returns>Steering output clamped.</returns>
    private SteeringOutput ClampSteeringOutput(SteeringOutput steeringOutput)
    {
        if (isAccelerationSteering(steeringOutput))
        {
            return new SteeringOutput(
                ClampVector2(steeringOutput.Linear, 0, maximumAcceleration),
                steeringOutput.Angular
            );
        }
        else
        {
            return new SteeringOutput(
                ClampVector2(steeringOutput.Linear, 0, maximumDeceleration),
                steeringOutput.Angular
            );
        }
    }

    /// <summary>
    /// Whether this steering output is going to increase speed or not.
    /// </summary>
    /// <param name="steeringOutput">Current steering.</param>
    /// <returns>True if this steering is going to accelerate the agent. False otherwise.</returns>
    private bool isAccelerationSteering(SteeringOutput steeringOutput)
    {
        return Vector2.Dot(rigidBody.velocity, steeringOutput.Linear) >= 0;
    }
    
    /// <summary>
    /// Clamp vector magnitude between a minimum and a maximum length.
    /// </summary>
    /// <param name="vectorToClamp">Vector to check.</param>
    /// <param name="minimumMagnitude">Minimum length for the clamped vector.</param>
    /// <param name="maximumMagnitude">Maximin length for the clamped vector.</param>
    /// <returns>Vector clamped.</returns>
    private Vector2 ClampVector2(Vector2 vectorToClamp, float minimumMagnitude, float maximumMagnitude)
    {
        if (vectorToClamp.magnitude > maximumMagnitude)
        {
            vectorToClamp = vectorToClamp.normalized * maximumMagnitude;
        } 
        else if (vectorToClamp.magnitude < minimumMagnitude)
        {   
            vectorToClamp = Mathf.Approximately(minimumMagnitude, 0.0f)? 
                Vector2.zero:  
                vectorToClamp.normalized * minimumMagnitude;
        }
        return vectorToClamp;
    }
    //
    // /// <summary>
    // /// Get rotation scalar updated with current steering and clamped by maximum rotational speed.
    // /// </summary>
    // /// <returns>New rotational scalar (older plus steering).</returns>
    // private float GetNewRotation(float delta)
    // {
    //     float newRotation = Mathf.Clamp(rigidBody.rotation + steeringBehavior.Angular * delta, 
    //         0, 
    //         maximumRotationalSpeed);
    //     return newRotation;
    // }
}
