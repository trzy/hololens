using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MathHelpers
{
  // Project onto ground plane (xz-plane)
  public static Vector3 GroundVector(Vector3 v)
  {
    return new Vector3(v.x, 0, v.z);
  }

  // Y-component of cross product
  public static float CrossY(Vector3 a, Vector3 b)
  {
    return -(a.x * b.z - a.z * b.x);
  }
}