//TODO: on any kind of collision, next waypoint should be chosen or current path aborted. Need to detect
//      collisions based on some sort of flag from helicopter base class
//TODO: make timeout a global property?
//TODO: helicopters should bounce off of any non-projectile they collide with forcefully. Need to determine
//      direction. Make it similar to Jungle Strike, with rotation.
//TODO: collision avoidance in the orbit code is whack. Sometimes, even if there is a path to the next
//      waypoint, the collider gets stuck. Simple solution proposed above. See if it works...
//TODO: raycasts should probably be emitted from the muzzle point or something, which should eventually be
//      set here.
//TODO: error thresholds (ACCEPTABLE_DISTANCE and ACCEPTABLE_HEADING_ERROR) probably need to be made configurable
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Helicopter))]
public class HelicopterAutopilot: MonoBehaviour
{
  [Tooltip("Layers to treat as obstacles. Autopilot will adjust its behavior in response to colliders in these layers.")]
  public LayerMask avoidanceLayers;

  [Tooltip("Amount of time (seconds) to spend attempting to move to an obstructed waypoint before giving up and selecting the next one.")]
  public float obstructionRetryTime = 1;

  [Tooltip("Timeout (seconds) used to abort flight patterns with a fixed destination or route. Persistent patterns, like orbiting, have their own timeout.")]
  public float timeout = 10;

  public float throttle
  {
    get { return m_throttle; }
    set { m_throttle = value; }
  }

  public bool flying
  {
    get { return m_movementCoroutine != null; }
  }

  private Helicopter m_helicopter;
  private IEnumerator m_movementCoroutine = null;
  private IEnumerator m_directionCoroutine = null;
  private Helicopter.Controls m_controls = new Helicopter.Controls();
  private float m_throttle = 1;
  
  private const float ACCEPTABLE_DISTANCE = .2f; //5 * .06f;
  private const float ACCEPTABLE_HEADING_ERROR = 10; // in degrees

  private void UpdateControls()
  {
    m_controls.ApplyThrottle(throttle);
    m_helicopter.controls = m_controls;
  }

  private void LaunchMovementCoroutine(IEnumerator coroutine)
  {
    if (m_movementCoroutine != null)
    {
      StopCoroutine(m_movementCoroutine);
      m_movementCoroutine = null;
      m_controls.longitudinal = 0;
      m_controls.lateral = 0;
      UpdateControls();
    }

    if (coroutine != null)
    {
      m_movementCoroutine = coroutine;
      StartCoroutine(m_movementCoroutine);
    }
  }

  private void LaunchDirectionCoroutine(IEnumerator coroutine)
  {
    if (m_directionCoroutine != null)
    {
      StopCoroutine(m_directionCoroutine);
      m_directionCoroutine = null;
      m_controls.rotational = 0;
      UpdateControls();
    }

    if (coroutine != null)
    {
      m_directionCoroutine = coroutine;
      StartCoroutine(m_directionCoroutine);
    }
  }

  public void Halt()
  {
    LaunchMovementCoroutine(null);
    LaunchDirectionCoroutine(null);
  }

  private bool PathObstructed(Vector3 targetPosition)
  {
    Vector3 toTarget = targetPosition - transform.position;
    Vector3 origin = transform.position + 0.25f * toTarget.normalized;  // TODO: replace with muzzle point or some characteristic radius?
    return Physics.Raycast(origin, toTarget, toTarget.magnitude);
  }

  private float HeadingErrorTo(Vector3 lookAtPoint)
  {
    Vector3 targetForward = lookAtPoint - transform.position;
    Vector3 target = MathHelpers.Azimuthal(targetForward);
    Vector3 forward = MathHelpers.Azimuthal(transform.forward);
    return Mathf.Sign(MathHelpers.CrossY(forward, target)) * Vector3.Angle(forward, target);
  }

  private bool LookAt(Vector3 lookAtPoint)
  {
    float headingError = HeadingErrorTo(lookAtPoint);
    float absHeadingError = Mathf.Abs(headingError);
    if (absHeadingError > ACCEPTABLE_HEADING_ERROR)
    {
      m_controls.rotational = -Mathf.Sign(headingError) * Mathf.Lerp(0.5F, 1.0F, Mathf.Abs(headingError) / 360.0F);
      return true;
    }
    else
    {
      m_controls.rotational = 0;
      return false;
    }
  }

  private IEnumerator LookAtCoroutine(Vector3 lookAtPoint)
  {
    while (true)
    {
      LookAt(lookAtPoint);
      UpdateControls();
      yield return null;
    }
  }

  private IEnumerator LookAtCoroutine(Transform lookAtTarget)
  {
    while (true)
    {
      LookAt(lookAtTarget.position);
      UpdateControls();
      yield return null;
    }
  }

  private IEnumerator LookFlightDirectionCoroutine()
  {
    Rigidbody rb = GetComponent<Rigidbody>();
    while (true)
    {
      Vector3 directionOfTravel = MathHelpers.Azimuthal(rb.velocity);
      LookAt(transform.position + directionOfTravel);
      UpdateControls();
      yield return null;
    }
  }

  private bool GoTo(Vector3 targetPosition)
  {
    Vector3 toTarget = targetPosition - transform.position;
    float distance = Vector3.Magnitude(toTarget);
    if (distance > ACCEPTABLE_DISTANCE)
    {
      //TODO: reduce intensity once closer? Gradual roll-off within some event horizon.
      Vector3 toTargetNorm = toTarget / distance;
      m_controls.longitudinal = Vector3.Dot(toTargetNorm, transform.forward);
      m_controls.lateral = Vector3.Dot(toTargetNorm, transform.right);
      m_controls.altitude = toTargetNorm.y;
    }
    else
      return false;
    return true;
  }

  private IEnumerator FlyToPositionCoroutine(Vector3 targetPosition, System.Action OnComplete)
  {
    float startTime = Time.time;
    while (GoTo(targetPosition))
    {
      UpdateControls();
      yield return null;
      if (Time.time - startTime >= timeout)
        break;
    }

    // Crudely slow down to a halt by reversing the controls until the velocity
    // is almost zero, then clear all inputs
    Rigidbody rb = GetComponent<Rigidbody>();
    while (rb.velocity.magnitude > 0.01f)
    {
      m_controls.longitudinal = -Vector3.Dot(rb.velocity.normalized, transform.forward);
      m_controls.lateral = -Vector3.Dot(rb.velocity.normalized, transform.right);
      m_controls.altitude = -Mathf.Sign(rb.velocity.y);
      yield return null;
      if (Time.time - startTime >= timeout)
        break;
    }
    m_controls.Clear();

    Halt();
    if (OnComplete != null)
      OnComplete(); 
  }

  private IEnumerator FollowCoroutine(Transform target, float distance, System.Action OnComplete)
  {
    float startTime = Time.time;
    while (GoTo(target.position))
    {
      UpdateControls();
      yield return null;
      if (Time.time - startTime >= timeout)
        break;
    }

    // Crudely slow down to a halt by reversing the controls until the velocity
    // is almost zero, then clear all inputs
    Rigidbody rb = GetComponent<Rigidbody>();
    while (rb.velocity.magnitude > 0.01f)
    {
      m_controls.longitudinal = -Vector3.Dot(rb.velocity.normalized, transform.forward);
      m_controls.lateral = -Vector3.Dot(rb.velocity.normalized, transform.right);
      m_controls.altitude = -Mathf.Sign(rb.velocity.y);
      yield return null;
      if (Time.time - startTime >= timeout)
        break;
    }
    m_controls.Clear();

    Halt();
    if (OnComplete != null)
      OnComplete();
  }

  private IEnumerator FollowPathCoroutine(Vector3[] waypoints, System.Action OnComplete)
  {
    float startTime = Time.time;
    foreach (Vector3 waypoint in waypoints)
    {
      while (GoTo(waypoint))
      {
        UpdateControls();
        yield return null;
        if (Time.time - startTime >= timeout)
          break;
      }
    }

    // Crudely slow down to a halt by reversing the controls until the velocity
    // is almost zero, then clear all inputs
    Rigidbody rb = GetComponent<Rigidbody>();
    while (rb.velocity.magnitude > 0.01f)
    {
      m_controls.longitudinal = -Vector3.Dot(rb.velocity.normalized, transform.forward);
      m_controls.lateral = -Vector3.Dot(rb.velocity.normalized, transform.right);
      m_controls.altitude = -Mathf.Sign(rb.velocity.y);
      yield return null;
      if (Time.time - startTime >= timeout)
        break;
    }
    m_controls.Clear();

    Halt();    
    if (OnComplete != null)
      OnComplete();
  }

  public delegate Vector3 UpdateVectorCallback(float deltaTime);
  public delegate float UpdateScalarCallback(float deltaTime);

  private IEnumerator OrbitPositionCoroutine(UpdateVectorCallback GetOrbitCenter, UpdateScalarCallback GetOrbitAltitude, float orbitRadius, float timeout)
  {
    float step = 20;
    float direction = MathHelpers.RandomSign();
    Vector3 toHelicopter = MathHelpers.Azimuthal(transform.position - GetOrbitCenter(Time.deltaTime));
    float startRadius = toHelicopter.magnitude;
    float currentRadius = startRadius;
    float startAngle = currentRadius == 0 ? 0 : Mathf.Rad2Deg * Mathf.Acos(toHelicopter.x / currentRadius);
    if (startAngle < 0)
      startAngle += 180;
    float currentAngle = startAngle;
    float degreesElapsed = 0;
    float startAltitude = transform.position.y;
    float currentAltitude = startAltitude;

    int maxObstructionRetries = (int) (360 / step);
    int numObstructionRetries = maxObstructionRetries;
    float nextObstructionCheckTime = 0;
    float timeoutTime = Time.time + timeout;
    while (true)
    {
      // Compute the next position along the circle
      float nextAngle = currentAngle + direction * step;
      float nextRadians = Mathf.Deg2Rad * nextAngle;

      // Move to that position
      bool flying = true;
      do
      {
        float now = Time.time;

        if (now >= timeoutTime)
        {
          Halt();
          yield break;
        }

        // In case orbit center is a moving target, continually update the next
        // position
        Vector3 nextPosition = GetOrbitCenter(Time.deltaTime) + currentRadius * new Vector3(Mathf.Cos(nextRadians), 0, Mathf.Sin(nextRadians));
        nextPosition.y = currentAltitude;

        // This doesn't work very well but the idea is to check for 
        // obstructions and move on to subsequent waypoints. If no point seems
        // reachable, we simply abort. For each attempt, we allow some time to
        // pass in case we were stuck.
        if (now > nextObstructionCheckTime && PathObstructed(nextPosition))
        {
          if (numObstructionRetries > 0)
          {
            nextObstructionCheckTime = now + obstructionRetryTime;
            numObstructionRetries--;
            break;
          }

          // Need to abort
          Halt();
          //TODO: callback?
          yield break;
        }

        flying = GoTo(nextPosition);
        UpdateControls();
        yield return null;
      } while (flying);

      // Restore number of avoidance attempts if we finally reached a waypoint
      if (flying == false)
        numObstructionRetries = maxObstructionRetries;

      // Advance the angle
      degreesElapsed += step;
      currentAngle = nextAngle;
      float revolutionCompleted = degreesElapsed / 360;

      // Adjust the radius so that it converges to the desired radius within
      // one revolution
      currentRadius = Mathf.Lerp(startRadius, orbitRadius, revolutionCompleted);

      // Altitude convergence
      currentAltitude = Mathf.Lerp(startAltitude, GetOrbitAltitude(Time.deltaTime), revolutionCompleted);
    }
  }

  public void FlyTo(Transform target, System.Action OnComplete = null)
  {
    LaunchMovementCoroutine(FlyToPositionCoroutine(target.position, OnComplete));
    LaunchDirectionCoroutine(LookAtCoroutine(target));
  }

  public void FlyTo(Vector3 position, Transform lookAtTarget, System.Action OnComplete = null)
  {
    LaunchMovementCoroutine(FlyToPositionCoroutine(position, OnComplete));
    LaunchDirectionCoroutine(LookAtCoroutine(lookAtTarget));
  }

  // Orbit a position while always facing straight ahead
  public void Orbit(Vector3 orbitCenter, float orbitAltitude, float orbitRadius = 1, float timeout = float.PositiveInfinity)
  {
    UpdateVectorCallback GetOrbitCenter = (float deltaTime) => { return orbitCenter; };
    UpdateScalarCallback GetOrbitAltitude = (float deltaTime) => { return orbitAltitude; };
    LaunchMovementCoroutine(OrbitPositionCoroutine(GetOrbitCenter, GetOrbitAltitude, orbitRadius, timeout));
    LaunchDirectionCoroutine(LookFlightDirectionCoroutine());
  }

  // Orbit a target while always facing it
  public void OrbitAndLookAt(Transform orbitCenter, float relativeOrbitAltitude, float orbitRadius = 1, float timeout = float.PositiveInfinity)
  {
    UpdateVectorCallback GetOrbitCenter = (float deltaTime) => { return orbitCenter.position; };
    UpdateScalarCallback GetOrbitAltitude = (float deltaTime) => { return orbitCenter.position.y + relativeOrbitAltitude; };
    LaunchMovementCoroutine(OrbitPositionCoroutine(GetOrbitCenter, GetOrbitAltitude, orbitRadius, timeout));
    LaunchDirectionCoroutine(LookAtCoroutine(orbitCenter));
  }

  public void Follow(Transform target, float distance, System.Action OnComplete)
  {
    LaunchMovementCoroutine(FollowCoroutine(target, distance, OnComplete));
    LaunchDirectionCoroutine(LookAtCoroutine(target));
  }

  public void FollowPathAndLookAt(Vector3[] waypoints, Transform lookAtTarget, float timeout = float.PositiveInfinity, System.Action OnComplete = null)
  {
    LaunchMovementCoroutine(FollowPathCoroutine(waypoints, OnComplete));
    LaunchDirectionCoroutine(LookAtCoroutine(lookAtTarget));
  }

  /*
  private void OnCollisionEnter(Collision collision)
  {
    if ((avoidanceLayers.value & (1 << collision.collider.gameObject.layer)) != 0)
      m_colliding = true;
  }

  private void OnCollisionExit(Collision collision)
  {
    if ((avoidanceLayers.value & (1 << collision.collider.gameObject.layer)) != 0)
      m_colliding = false;
  }
  */

  private void Start()
  {
    m_controls.Clear();
  }

  private void Awake()
  {
    m_helicopter = GetComponent<Helicopter>();
  }
}
