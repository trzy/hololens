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

  [Tooltip("Error tolerance in degrees.")]
  public float maxErrorDegrees = 2;

  private float m_sinMaxErrorDegrees;

  private void Update()
  {
    Vector3 targetPosition = target == null ? Camera.main.transform.position : target.position;
    Vector3 targetLocalPosition = MathHelpers.Azimuthal(azimuthalTrackingObject.transform.InverseTransformPoint(targetPosition));
    float sinAngle = MathHelpers.CrossY(Vector3.forward, targetLocalPosition.normalized); // only the y component will be valid and we want it signed
    if (Mathf.Abs(sinAngle) > m_sinMaxErrorDegrees)
    {
      float direction = Mathf.Sign(sinAngle);
      azimuthalTrackingObject.Rotate(0, direction * Time.deltaTime * azimuthalSpeed, 0);
    }
  }

  private void Start()
  {
    m_sinMaxErrorDegrees = Mathf.Sin(Mathf.Abs(maxErrorDegrees) * Mathf.Deg2Rad);
  }
}