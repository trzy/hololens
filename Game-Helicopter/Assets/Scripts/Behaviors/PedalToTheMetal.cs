using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PedalToTheMetal: MonoBehaviour
{
  public float acceleration = 1;
  private void FixedUpdate()
  {
    //GetComponent<Rigidbody>().AddRelativeForce(acceleration * Vector3.forward, ForceMode.VelocityChange);
    GetComponent<Rigidbody>().velocity = transform.forward * acceleration;
  }
}
