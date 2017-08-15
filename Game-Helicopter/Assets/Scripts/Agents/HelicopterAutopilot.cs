//TODO: separate coroutines for translational motion and rotation? 
//TODO: introduce the concept of a throttle by capping the magnitude of the control vector that
// can be produced. +1,+1 (lateral and longitudinal) would be 100%. Maybe also consider altitude.
// Should just be a public var we set.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Helicopter))]
public class HelicopterAutopilot: MonoBehaviour
{
  private Helicopter m_helicopter;
  private IEnumerator m_controlCoroutine = null;
  private Helicopter.Controls m_controls = new Helicopter.Controls();

  private const float ACCEPTABLE_DISTANCE = 5 * .06f;
  private const float ACCEPTABLE_HEADING_ERROR = 10; // in degrees

  private void LaunchCoroutine(IEnumerator coroutine)
  {
    if (m_controlCoroutine != null)
      StopCoroutine(m_controlCoroutine);
    m_controlCoroutine = coroutine;
    StartCoroutine(m_controlCoroutine);
  }

  private float HeadingErrorTo(Vector3 lookAtPoint)
  {
    Vector3 targetForward = lookAtPoint - transform.position;
    Vector3 target = MathHelpers.Azimuthal(targetForward);
    Vector3 forward = MathHelpers.Azimuthal(transform.forward);
    return Mathf.Sign(MathHelpers.CrossY(forward, target)) * Vector3.Angle(forward, target);
  }

  private bool GoTo(Vector3 targetPosition, Vector3 lookAtPoint)
  {
    Vector3 toTarget = targetPosition - transform.position;
    float distance = Vector3.Magnitude(toTarget);
    float headingError = HeadingErrorTo(lookAtPoint);
    float absHeadingError = Mathf.Abs(headingError);
    if (absHeadingError > ACCEPTABLE_HEADING_ERROR)
      m_controls.rotational = -Mathf.Sign(headingError) * Mathf.Lerp(0.5F, 1.0F, Mathf.Abs(headingError) / 360.0F);
    else
      m_controls.rotational = 0;
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

  private IEnumerator FlyToPositionCoroutine(Vector3 targetPosition, Vector3 lookAtPoint)
  {
    while (GoTo(targetPosition, lookAtPoint))
    {
      m_helicopter.controls = m_controls;
      yield return null;
    }
    m_controls.Clear();
    m_controlCoroutine = null;
  }

  private IEnumerator OrbitPositionCoroutine(Vector3 orbitCenter, float orbitAltitude, float orbitRadius)
  {
    float step = 20;
    float direction = MathHelpers.RandomSign();
    Vector3 toHelicopter = MathHelpers.Azimuthal(transform.position - orbitCenter);
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
      toHelicopter = MathHelpers.Azimuthal(toHelicopter);
      float nextAngle = currentAngle + direction * step;
      float nextRadians = Mathf.Deg2Rad * nextAngle;
      Vector3 nextPosition = currentRadius * new Vector3(Mathf.Cos(nextRadians), currentAltitude, Mathf.Sin(nextRadians));

      // Move to that position
      while (GoTo(nextPosition, orbitCenter))
      {
        m_helicopter.controls = m_controls;
        yield return null;
      }

      // Advance the angle
      degreesElapsed += step;
      currentAngle = nextAngle;
      float revolutionCompleted = degreesElapsed / 360;

      // Adjust the radius so that it converges to the desired radius within
      // one revolution
      currentRadius = Mathf.Lerp(startRadius, orbitRadius, revolutionCompleted);

      // Altitude convergence
      currentAltitude = Mathf.Lerp(startAltitude, orbitAltitude, revolutionCompleted);
    }
  }

  public void FlyTo(Transform target)
  {
    LaunchCoroutine(FlyToPositionCoroutine(target.position, target.position));
  }

  //TODO: make this take a Tranform
  public void Orbit(Vector3 orbitCenter, float orbitAltitude, float orbitRadius = 1)
  {
    LaunchCoroutine(OrbitPositionCoroutine(orbitCenter, orbitAltitude, orbitRadius));
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
