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

  [Tooltip("Error tolerance in degrees.")]
  public float maxErrorDegrees = 2;

  private float m_sinMaxVerticalAngle;
  private float m_sinMinVerticalAngle;
  private float m_deltaSinMaxErrorDegrees;
  private float m_sinMaxErrorDegrees;

  private void Update()
  {
    Vector3 targetPosition = target == null ? Camera.main.transform.position : target.position;

    if (azimuthalTrackingObject != null)
    {
      Vector3 targetLocalPosition = MathHelpers.Azimuthal(azimuthalTrackingObject.transform.InverseTransformPoint(targetPosition));
      float sinAngle = MathHelpers.CrossY(Vector3.forward, targetLocalPosition.normalized); // only the y component will be valid and we want it signed
      if (Mathf.Abs(sinAngle) > m_sinMaxErrorDegrees)
      {
        float direction = Mathf.Sign(sinAngle);
        azimuthalTrackingObject.Rotate(0, direction * Time.deltaTime * azimuthalSpeed, 0);
      }
    }

    if (verticalTrackingObject != null && azimuthalTrackingObject != null)
    {     
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
      if (Mathf.Abs(sinTargetAngle - sinObjectAngle) > m_deltaSinMaxErrorDegrees)
      {
        float direction = Mathf.Sign(sinObjectAngle - sinTargetAngle);
        verticalTrackingObject.Rotate(direction * Time.deltaTime * verticalSpeed, 0, 0);
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
  }

  private void Start()
  {
    m_sinMaxVerticalAngle = Mathf.Sin(maxVerticalAngle * Mathf.Deg2Rad);
    m_sinMinVerticalAngle = Mathf.Sin(minVerticalAngle * Mathf.Deg2Rad);
    m_sinMaxErrorDegrees = Mathf.Sin(Mathf.Abs(maxErrorDegrees) * Mathf.Deg2Rad);

    // Largest Sin(x) - Sin(x-MAX_ERRORD_DEGREES) occurs about x = 0, where the
    // derivative of Sin(x) is highest
    m_deltaSinMaxErrorDegrees = Mathf.Sin(0) - Mathf.Sin(-Mathf.Abs(maxErrorDegrees) * Mathf.Deg2Rad);
  }
}