using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Track: MonoBehaviour
{
  [Tooltip("Object to track.")]
  public Transform target = null;

  [Tooltip("Component of agent that tracks along azimuthal direction.")]
  public Transform azimuthalTrackingObject = null;

  [Tooltip("Component of agent that tracks vertically.")]
  public Transform verticalTrackingObject = null;

  [Tooltip("Azimuthal rotation speed (degrees/sec).")]
  public float azimuthalSpeed = 180 / 10;

  [Tooltip("Vertical rotation speed (degrees/sec).")]
  public float verticalSpeed = 180 / 10;

  [Tooltip("Maximum vertical angle (degrees).")]
  [Range(-90, 90)]  // Sine is monotonic in this range
  public float maxVerticalAngle = 60;

  [Tooltip("Minimum vertical angle (degrees).")]
  [Range(-90, 90)]
  public float minVerticalAngle = 30;

  [Tooltip("Error tolerance in degrees that tracking aims for.")]
  public float maxErrorDegrees = 2;

  [Tooltip("Approximate degrees (in azimuthal and vertical directions) within which lock callback is called.")]
  public float lockDegrees = 10;

  public Action OnLockObtained = null;
  public Action OnLockLost = null;
  public bool perfectLock = false;
  public bool lockedOn = false;

  private float m_sinMaxVerticalAngle;
  private float m_sinMinVerticalAngle;
  private float m_deltaSinMaxErrorDegrees;
  private float m_sinMaxErrorDegrees;
  private float m_sinLockDegrees;

  private void Update()
  {
    if (target == null)
      return;

    bool hasVerticalObject = false;
    bool azimuthalLock = false;
    bool azimuthalPerfectLock = false;
    bool verticalLock = false;
    bool verticalPerfectLock = false;

    Vector3 targetPosition = target.position;

    if (azimuthalTrackingObject != null)
    {
      Vector3 targetLocalPosition = MathHelpers.Azimuthal(azimuthalTrackingObject.transform.InverseTransformPoint(targetPosition));
      float sinAngle = MathHelpers.CrossY(Vector3.forward, targetLocalPosition.normalized); // only the y component will be valid and we want it signed
      float absSinAngle = Mathf.Abs(sinAngle);
      if (absSinAngle > m_sinMaxErrorDegrees)
      {
        float direction = Mathf.Sign(sinAngle);
        azimuthalTrackingObject.Rotate(0, direction * Time.deltaTime * azimuthalSpeed, 0);
        azimuthalLock = absSinAngle <= m_sinLockDegrees;
      }
      else
      {
        azimuthalLock = true;
        azimuthalPerfectLock = true;
      }
    }

    if (verticalTrackingObject != null && azimuthalTrackingObject != null)
    {
      hasVerticalObject = true;

      // Transform both the target and the vertical rotating object into the
      // local coordinate system of the azimuthal object, whose xz-plane will
      // be our ground plane from which to measure vertical angle.
      Vector3 targetLocalVector = azimuthalTrackingObject.transform.InverseTransformPoint(targetPosition);
      Vector3 objectLocalVector = azimuthalTrackingObject.transform.InverseTransformVector(verticalTrackingObject.forward);

      // Compute the angles from the ground (or rather, the sine of the angles)
      float sinTargetAngle = targetLocalVector.y / targetLocalVector.magnitude;
      float sinObjectAngle = objectLocalVector.y / objectLocalVector.magnitude;

      // Clamp the target angle to allowable range
      sinTargetAngle = Mathf.Clamp(sinTargetAngle, m_sinMinVerticalAngle, m_sinMaxVerticalAngle);

      // Rotate appropriately to minimize error
      float deltaSinAngle = sinObjectAngle - sinTargetAngle;
      float absDeltaSinAngle = Mathf.Abs(deltaSinAngle);
      if (absDeltaSinAngle > m_deltaSinMaxErrorDegrees)
      {
        float direction = Mathf.Sign(deltaSinAngle);
        verticalTrackingObject.Rotate(direction * Time.deltaTime * verticalSpeed, 0, 0);
        verticalLock = absDeltaSinAngle <= m_sinLockDegrees;
      }
      else
      {
        verticalLock = true;
        verticalPerfectLock = true;
      }

      /*
      // This code is a reference implementation that computes everything in
      // angles but requires the use of slow arcsin functions
      Vector3 targetLocalPosition = azimuthalTrackingObject.transform.InverseTransformPoint(targetPosition);
      Vector3 objectLocalPosition = azimuthalTrackingObject.transform.InverseTransformVector(verticalTrackingObject.forward);
      float deltaAngle = Mathf.Rad2Deg * (Mathf.Asin(targetLocalPosition.y / targetLocalPosition.magnitude) - Mathf.Asin(objectLocalPosition.y / objectLocalPosition.magnitude));
      if (Mathf.Abs(deltaAngle ) > maxErrorDegrees)
      {
        float direction = -Mathf.Sign(deltaAngle);
        verticalTrackingObject.Rotate(direction * Time.deltaTime * verticalSpeed, 0, 0);
      }
      */
    }

    // Callbacks
    bool oldLockState = lockedOn;
    lockedOn = false;
    if (!hasVerticalObject && azimuthalLock)
    {
      lockedOn = true;
      perfectLock = azimuthalPerfectLock;
      if (OnLockObtained != null && oldLockState == false)
        OnLockObtained();
    }
    else if (verticalLock && azimuthalLock)
    {
      lockedOn = true;
      perfectLock = azimuthalPerfectLock && verticalPerfectLock;
      if (OnLockObtained != null && oldLockState == false)
        OnLockObtained();
    }
    if (OnLockLost != null && oldLockState == true && lockedOn == false)
      OnLockLost();
  }

  private void Start()
  {
    m_sinMaxVerticalAngle = Mathf.Sin(maxVerticalAngle * Mathf.Deg2Rad);
    m_sinMinVerticalAngle = Mathf.Sin(minVerticalAngle * Mathf.Deg2Rad);
    m_sinMaxErrorDegrees = Mathf.Sin(Mathf.Abs(maxErrorDegrees) * Mathf.Deg2Rad);
    m_sinLockDegrees = Mathf.Sin(Mathf.Max(lockDegrees) * Mathf.Deg2Rad);

    // Largest Sin(x) - Sin(x-MAX_ERRORD_DEGREES) occurs about x = 0, where the
    // derivative of Sin(x) is highest
    m_deltaSinMaxErrorDegrees = Mathf.Sin(0) - Mathf.Sin(-Mathf.Abs(maxErrorDegrees) * Mathf.Deg2Rad);
  }
}