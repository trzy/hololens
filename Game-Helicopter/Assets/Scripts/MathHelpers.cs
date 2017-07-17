using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MathHelpers
{
  public static bool IsNaN(Vector3 v)
  {
    return float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z);
  }

  // Project onto azimuthal plane (xz-plane; the ground plane)
  public static Vector3 Azimuthal(Vector3 v)
  {
    return new Vector3(v.x, 0, v.z);
  }

  // Project onto the vertical plane (yz-plane)
  public static Vector3 Vertical(Vector3 v)
  {
    return new Vector3(0, v.y, v.z);
  }

  // X-component of cross product
  public static float CrossX(Vector3 a, Vector3 b)
  {
    return a.y * b.z - a.z * b.y;
  }

  // Y-component of cross product
  public static float CrossY(Vector3 a, Vector3 b)
  {
    return -(a.x * b.z - a.z * b.x);
  }
}