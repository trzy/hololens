using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HelicopterRotor: MonoBehaviour
{
  [Tooltip("Angular velocity in degrees/sec along each Euler axis.")]
  public Vector3 angularVelocity = 360 * Vector3.up;

  [Tooltip("Object to rotate about Y axis.")]
  public Transform rotor;

  private void Update()
  {
    rotor.Rotate(Time.deltaTime * angularVelocity);
  }
}