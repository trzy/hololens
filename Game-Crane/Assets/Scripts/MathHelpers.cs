using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MathHelpers
{
  public static float CrossY(Vector3 a, Vector3 b)
  {
    return -(a.x * b.z - a.z * b.x);
  }
}