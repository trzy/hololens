//TODO: error thresholds (ACCEPTABLE_DISTANCE and ACCEPTABLE_HEADING_ERROR) probably need to be made configurable
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Helicopter))]
public class HelicopterAutopilot: MonoBehaviour
{
  public float throttle
  {
    get { return m_throttle; }
    set { m_throttle = Mathf.Clamp(value, 0, 1); }
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
    m_controls.ClampAzimuthal(throttle);
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

  private IEnumerator FlyToPositionCoroutine(Vector3 targetPosition, float timeout, System.Action OnComplete)
  {
    float startTime = Time.time;
    while (GoTo(targetPosition))
    {
      UpdateControls();
      yield return null;
      if (Time.time - startTime >= timeout)
        break;
    }
    Halt();
    OnComplete(); 
  }

  private IEnumerator FollowCoroutine(Transform target, float distance, float timeout, System.Action OnComplete)
  {
    float startTime = Time.time;
    while (GoTo(target.position))
    {
      UpdateControls();
      yield return null;
      if (Time.time - startTime >= timeout)
        break;
    }
    Halt();
    OnComplete();
  }

  public delegate Vector3 UpdateVectorCallback(float deltaTime);
  public delegate float UpdateScalarCallback(float deltaTime);

  private IEnumerator OrbitPositionCoroutine(UpdateVectorCallback GetOrbitCenter, UpdateScalarCallback GetOrbitAltitude, float orbitRadius)
    //Vector3 orbitCenter, float orbitAltitude, float orbitRadius)
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

    while (true)
    {
      // Compute the next position along the circle
      float nextAngle = currentAngle + direction * step;
      float nextRadians = Mathf.Deg2Rad * nextAngle;

      // Move to that position
      bool flying = true;
      do
      {
        // In case orbit center is a moving target, continually update the next
        // position
        Vector3 nextPosition = GetOrbitCenter(Time.deltaTime) + currentRadius * new Vector3(Mathf.Cos(nextRadians), 0, Mathf.Sin(nextRadians));
        nextPosition.y = currentAltitude;
        flying = GoTo(nextPosition);
        UpdateControls();
        yield return null;
      } while (flying);

      // Advance the angle
      degreesElapsed += step;
      currentAngle = nextAngle;
      float revolutionCompleted = degreesElapsed / 360;

      // Adjust the radius so that it converges to the desired radius within
      // one revolution
      currentRadius = Mathf.Lerp(startRadius, orbitRadius, revolutionCompleted);
      Debug.Log(startRadius + " -> " + orbitRadius + " @ " + revolutionCompleted + " = " + currentRadius);

      // Altitude convergence
      currentAltitude = Mathf.Lerp(startAltitude, GetOrbitAltitude(Time.deltaTime), revolutionCompleted);
    }
  }

  public void FlyTo(Transform target, float timeout, System.Action OnComplete)
  {
    LaunchMovementCoroutine(FlyToPositionCoroutine(target.position, timeout, OnComplete));
    LaunchDirectionCoroutine(LookAtCoroutine(target));
  }

  public void FlyTo(Vector3 position, Transform lookAtTarget, float timeout, System.Action OnComplete)
  {
    LaunchMovementCoroutine(FlyToPositionCoroutine(position, timeout, OnComplete));
    LaunchDirectionCoroutine(LookAtCoroutine(lookAtTarget));
  }

  // Orbit a position while always facing straight ahead
  public void Orbit(Vector3 orbitCenter, float orbitAltitude, float orbitRadius = 1)
  {
    UpdateVectorCallback GetOrbitCenter = (float deltaTime) => { return orbitCenter; };
    UpdateScalarCallback GetOrbitAltitude = (float deltaTime) => { return orbitAltitude; };
    LaunchMovementCoroutine(OrbitPositionCoroutine(GetOrbitCenter, GetOrbitAltitude, orbitRadius));
    LaunchDirectionCoroutine(LookFlightDirectionCoroutine());
  }

  // Orbit a target while always facing it
  public void OrbitAndLookAt(Transform orbitCenter, float relativeOrbitAltitude, float orbitRadius = 1)
  {
    UpdateVectorCallback GetOrbitCenter = (float deltaTime) => { return orbitCenter.position; };
    UpdateScalarCallback GetOrbitAltitude = (float deltaTime) => { return orbitCenter.position.y + relativeOrbitAltitude; };
    LaunchMovementCoroutine(OrbitPositionCoroutine(GetOrbitCenter, GetOrbitAltitude, orbitRadius));
    LaunchDirectionCoroutine(LookAtCoroutine(orbitCenter));
  }

  public void Follow(Transform target, float distance, float timeout, System.Action OnComplete)
  {
    LaunchMovementCoroutine(FollowCoroutine(target, distance, timeout, OnComplete));
    LaunchDirectionCoroutine(LookAtCoroutine(target));
  }

  private void Start()
  {
    m_controls.Clear();
  }

  private void Awake()
  {
    m_helicopter = GetComponent<Helicopter>();
  }
}
