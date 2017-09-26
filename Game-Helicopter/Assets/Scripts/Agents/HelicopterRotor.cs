using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HelicopterRotor: MonoBehaviour
{
  [Tooltip("Angular velocity in degrees/sec along each Euler axis.")]
  public float angularVelocity = 360;

  [Tooltip("Axis of rotation.")]
  public Vector3 rotationAxis = Vector3.up;

  [Tooltip("Object to rotate.")]
  public Transform rotor;

  private void Update()
  {
    rotor.Rotate(Time.deltaTime * angularVelocity * rotationAxis);
  }
}