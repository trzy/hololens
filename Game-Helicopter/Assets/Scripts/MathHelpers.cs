﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MathHelpers
{
  // Angles must be normalized to [0,359] already. Result may be outside this
  // range.
  public static float ShortestAngleLerp(float fromDegrees, float toDegrees, float t)
  {
    float delta = toDegrees - fromDegrees;
    if (Mathf.Abs(delta) > 180)
      toDegrees -= 360 * Mathf.Sign(delta);
    return Mathf.Lerp(fromDegrees, toDegrees, t);
  }

  public static float CircularEaseOut(float from, float to, float t)
  {
    float t1 = Mathf.Clamp(t, 0, 1) - 1;  // shift curve right by 1
    return from + (to - from) * Mathf.Sqrt(1 - t1 * t1);
  }

  public static float Sigmoid01(float x)
  {
    // f(x) = x / (1 + |x|), f(x): [-0.5, 0.5] for x: [-1, 1]
    // To get [0, 1] for [0, 1]: f'(x) = 0.5 + f(2 * (x - 0.5))
    float y = 2 * (x - 0.5f);
    float f = 0.5f + y / (1 + Mathf.Abs(y));
    return f;
  }

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