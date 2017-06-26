using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

class Track: MonoBehaviour
{
  [Tooltip("Object to track.")]
  public Transform target = null;

  [Tooltip("Component of agent that tracks along azimuthal direction.")]
  public Transform azimuthalTrackingObject = null;

  [Tooltip("Component of agent that tracks altitude.")]
  public Transform altitudeTrackingObject = null;

  [Tooltip("Rate of turret rotation in degrees/sec during tracking state.")]
  public float azimuthalTrackingSpeed = 180 / 10;

  [Tooltip("Error tolerance in degrees.")]
  public float maxErrorDegrees = 2;

  private float m_sinMaxErrorDegrees;

  // Project onto ground plane (xz-plane)
  private Vector3 GroundVector(Vector3 v)
  {
    return new Vector3(v.x, 0, v.z);
  }

  private void Update()
  {
    Vector3 targetPosition = target == null ? Camera.main.transform.position : target.position;
    Vector3 targetLocalPosition = GroundVector(azimuthalTrackingObject.transform.InverseTransformPoint(targetPosition));
    float sinAngle = Vector3.Cross(Vector3.forward, targetLocalPosition.normalized).y;  // only the y component will be valid and we want it signed
    if (Mathf.Abs(sinAngle) > m_sinMaxErrorDegrees)
    {
      float direction = Mathf.Sign(sinAngle);
      azimuthalTrackingObject.Rotate(0, direction * Time.deltaTime * azimuthalTrackingSpeed, 0);
    }
  }

  private void Start()
  {
    m_sinMaxErrorDegrees = Mathf.Sin(Mathf.Abs(maxErrorDegrees) * Mathf.Deg2Rad);
  }
}