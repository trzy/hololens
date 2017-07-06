using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Scan: MonoBehaviour
{
  public enum ScanningMode
  {
    Continuous,
    Random
  }

  [Tooltip("Component of agent that scans along azimuthal direction.")]
  public Transform scanningObject = null;

  [Tooltip("Scanning mode.")]
  public ScanningMode mode;

  [Tooltip("Rate of rotation in degrees/sec during scanning.")]
  public float scanningSpeed = 180 / 10;

  [Tooltip("Continuous mode only: clockwise or counterclockwise rotation.")]
  public bool clockwise = true;

  [Tooltip("Random mode only: time (in seconds) to linger at each orientation.")]
  public float lingerTime = 1;

  private Vector3 m_targetOrientation = Vector3.zero;
  private float m_lingerStartTime;
  private bool m_lingering = false;

  private const float MAX_ERROR_DEGREES = 2;
  private float m_sinMaxErrorDegrees;

  private Vector3 RandomOrientation()
  {
    float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
    return new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
  }

  private void Update()
  {
    if (mode == ScanningMode.Continuous)
    {
      float direction = clockwise ? 1 : -1;
      scanningObject.Rotate(0, direction * Time.deltaTime * scanningSpeed, 0);
    }
    else
    {
      Vector3 currentOrientation = MathHelpers.Azimuthal(scanningObject.transform.forward).normalized;
      float sinAngle = MathHelpers.CrossY(currentOrientation, m_targetOrientation);
      if (Mathf.Abs(sinAngle) > m_sinMaxErrorDegrees)
      {
        // Move toward target orientation
        float direction = Mathf.Sign(sinAngle);
        scanningObject.Rotate(0, direction * Time.deltaTime * scanningSpeed, 0);
      }
      else if (!m_lingering)
      {
        // Target orientation reached; linger here a while
        m_lingering = true;
        m_lingerStartTime = Time.time;
      }
      else if (m_lingering && (Time.time - m_lingerStartTime) >= lingerTime)
      {
        // Select new target orientation
        m_lingering = false;
        m_targetOrientation = RandomOrientation();
      }
    }
  }

  private void Awake()
  {
    m_sinMaxErrorDegrees = Mathf.Sin(Mathf.Abs(MAX_ERROR_DEGREES) * Mathf.Deg2Rad);
    m_targetOrientation = RandomOrientation();
  }
}