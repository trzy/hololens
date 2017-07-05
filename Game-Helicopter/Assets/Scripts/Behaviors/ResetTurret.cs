using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class ResetTurret: MonoBehaviour
{
  [Tooltip("Component of turret that moves azimuthally.")]
  public Transform azimuthalObject = null;

  [Tooltip("Component of turret that moves vertically.")]
  public Transform verticalObject = null;

  [Tooltip("Azimuthal rotation speed (degrees/sec).")]
  public float azimuthalSpeed = 180 / 2;

  [Tooltip("Vertical rotation speed (degrees/sec).")]
  public float verticalSpeed = 180 / 10;

  public Action OnComplete = null;

  private const float MAX_ERROR_DEGREES = 2;

  private Vector3 RandomOrientation()
  {
    float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
    return new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
  }

  private void Update()
  {
    bool azimuthalFinished = false;
    bool verticalFinished = false;

    // Rotate azimuthal component back to 0
    if (azimuthalObject != null)
    {
      float currentOrientation = azimuthalObject.localRotation.eulerAngles.y;
      float direction = currentOrientation > 180 ? 1 : -1;  // for shortest rotation
      if (Mathf.Abs(currentOrientation) > MAX_ERROR_DEGREES)
        azimuthalObject.Rotate(0, direction * Time.deltaTime * azimuthalSpeed, 0);
      else
        azimuthalFinished = true;
    }
    else
      azimuthalFinished = true;

    // Rotate vertical component back to 0
    if (verticalObject != null)
    {
      float currentOrientation = verticalObject.rotation.eulerAngles.x;
      if (Mathf.Abs(currentOrientation) > MAX_ERROR_DEGREES)
      {
        float direction = currentOrientation > 0 ? 1 : -1;
        verticalObject.Rotate(direction * Time.deltaTime * verticalSpeed, 0, 0);
      }
      else
        verticalFinished = true;
    }
    else
      verticalFinished = true;

    if (azimuthalFinished && verticalFinished)
    {
      if (OnComplete != null)
        OnComplete();
      OnComplete = null;
      enabled = false;
    }
  }
}