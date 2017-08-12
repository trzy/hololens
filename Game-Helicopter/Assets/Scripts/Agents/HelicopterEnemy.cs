using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Helicopter))]
public class HelicopterEnemy: MonoBehaviour
{
  private Helicopter m_helicopter;
  private IEnumerator m_controlCoroutine = null;
  private Helicopter.Controls m_controls = new Helicopter.Controls();

  private const float ACCEPTABLE_DISTANCE = 5 * .06f;
  private const float ACCEPTABLE_HEADING_ERROR = 5; // in degrees

  private float HeadingError(Vector3 targetForward)
  {
    Vector3 target = MathHelpers.Azimuthal(targetForward);
    Vector3 forward = MathHelpers.Azimuthal(transform.forward);
    // Minus sign because error defined as how much we have overshot and need
    // to subtract, assuming positive rotation is clockwise.
    return Mathf.Sign(MathHelpers.CrossY(forward, target)) * Vector3.Angle(forward, target);
  }

  private float HeadingErrorTo(Vector3 targetPoint)
  {
    return HeadingError(targetPoint - transform.position);
  }

  private bool GoTo(Vector3 targetPosition)
  {
    Vector3 to_target = targetPosition - transform.position;
    float distance = Vector3.Magnitude(to_target);
    float headingError = HeadingErrorTo(targetPosition);
    float absHeadingError = Mathf.Abs(headingError);
    if (absHeadingError > ACCEPTABLE_HEADING_ERROR)
      m_controls.rotational = -Mathf.Sign(headingError) * Mathf.Lerp(0.5F, 1.0F, Mathf.Abs(headingError) / 360.0F);
    else
      m_controls.rotational = 0;
    if (distance > ACCEPTABLE_DISTANCE)
    {
      //TODO: reduce intensity once closer? Gradual roll-off within some event horizon.
      Vector3 toTargetNorm = to_target / distance;
      m_controls.longitudinal = Vector3.Dot(toTargetNorm, transform.forward);
      m_controls.lateral = Vector3.Dot(toTargetNorm, transform.right);
      m_controls.altitude = toTargetNorm.y;
    }
    else
      return false;
    return true;
  }

  private IEnumerator FlyToPositionCoroutine(Vector3 targetPosition)
  {
    while (GoTo(targetPosition))
    {
      m_helicopter.controls = m_controls;
      yield return null;
    }
    m_controls.Clear();
    m_controlCoroutine = null;
  }

  private Vector3 RandomOrientation()
  {
    float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
    return new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
  }

  private void FixedUpdate()
  {
    if (m_controlCoroutine == null)
    {
      Vector3 targetPosition = transform.position + 1f * RandomOrientation();
      m_controlCoroutine = FlyToPositionCoroutine(targetPosition);
      StartCoroutine(m_controlCoroutine);
    }
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
