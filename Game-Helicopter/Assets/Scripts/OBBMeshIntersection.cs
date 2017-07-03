/*
 * Based on the method described in "Fast 3D Triangle-Box Overlap Testing" by
 * Tomas Akenine-Moller (2001). The code here is a direct C -> C# conversion of
 * Moller's implementation, which can be found along with the paper here:
 * http://fileadmin.cs.lth.se/cs/personal/tomas_akenine-moller/code/
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity.SpatialMapping;

public static class OBBMeshIntersection
{
  // Creates an oriented bounding box from the box collider in *world* coordinates
  public static OrientedBoundingBox CreateWorldSpaceOBB(BoxCollider collider)
  {
    OrientedBoundingBox obb = new OrientedBoundingBox()
    {
      Center = collider.transform.TransformPoint(collider.center),
      Rotation = collider.transform.rotation,
      Extents = 0.5f * new Vector3(collider.transform.lossyScale.x * collider.size.x, collider.transform.lossyScale.y * collider.size.y, collider.transform.lossyScale.z * collider.size.z)
    };
    return obb;
  }

  private static Vector3 TransformWorldToOBB(Vector3 point, OrientedBoundingBox obb)
  {
    // OBB is in world units (no scale applied) but with an arbitrary center
    // point and rotation. This function converts from absolute world space to
    // OBB-local space.
    return Quaternion.Inverse(obb.Rotation) * (point - obb.Center);
  }

  private static bool AxisTestX01Failed(Vector3[] v, Vector3 boxhalfsize, float a, float b, float fa, float fb)
  {
    float p0 = a * v[0].y - b * v[0].z;
    float p2 = a * v[2].y - b * v[2].z;
    float min;
    float max;
    if (p0 < p2)
    {
      min = p0;
      max = p2;
    }
    else
    {
      min = p2;
      max = p0;
    }
    float rad = fa * boxhalfsize.y + fb * boxhalfsize.z;
    return (min > rad || max < -rad) ? true : false;
  }

  private static bool AxisTestX2Failed(Vector3[] v, Vector3 boxhalfsize, float a, float b, float fa, float fb)
  {
    float p0 = a * v[0].y - b * v[0].z;
    float p1 = a * v[1].y - b * v[1].z;
    float min;
    float max;
    if (p0 < p1)
    {
      min = p0;
      max = p1;
    }
    else
    {
      min = p1;
      max = p0;
    }
    float rad = fa * boxhalfsize.y + fb * boxhalfsize.z;
    return (min > rad || max < -rad) ? true : false;
  }

  private static bool AxisTestY02Failed(Vector3[] v, Vector3 boxhalfsize, float a, float b, float fa, float fb)
  {
    float p0 = -a * v[0].x + b * v[0].z;
    float p2 = -a * v[2].x + b * v[2].z;
    float min;
    float max;
    if (p0 < p2)
    {
      min = p0;
      max = p2;
    }
    else
    {
      min = p2;
      max = p0;
    }
    float rad = fa * boxhalfsize.x + fb * boxhalfsize.z;
    return (min > rad || max < -rad) ? true : false;
  }

  private static bool AxisTestY1Failed(Vector3[] v, Vector3 boxhalfsize, float a, float b, float fa, float fb)
  {
    float p0 = -a * v[0].x + b * v[0].z;
    float p1 = -a * v[1].x + b * v[1].z;
    float min;
    float max;
    if (p0 < p1)
    {
      min = p0;
      max = p1;
    }
    else
    {
      min = p1;
      max = p0;
    }
    float rad = fa * boxhalfsize.x + fb * boxhalfsize.z;
    return (min > rad || max < -rad) ? true : false;
  }

  private static bool AxisTestZ12Failed(Vector3[] v, Vector3 boxhalfsize, float a, float b, float fa, float fb)
  {
    float p1 = a * v[1].x - b * v[1].y;
    float p2 = a * v[2].x - b * v[2].y;
    float min;
    float max;
    if (p2 < p1)
    {
      min = p2;
      max = p1;
    }
    else
    {
      min = p1;
      max = p2;
    }
    float rad = fa * boxhalfsize.x + fb * boxhalfsize.y;
    return (min > rad || max < -rad) ? true : false;
  }

  private static bool AxisTestZ0Failed(Vector3[] v, Vector3 boxhalfsize, float a, float b, float fa, float fb)
  {
    float p0 = a * v[0].x - b * v[0].y;
    float p1 = a * v[1].x - b * v[1].y;
    float min;
    float max;
    if (p0 < p1)
    {
      min = p0;
      max = p1;
    }
    else
    {
      min = p1;
      max = p0;
    }
    float rad = fa * boxhalfsize.x + fb * boxhalfsize.y;
    return (min > rad || max < -rad) ? true : false;
  }

  private static bool AxialOverlapTestFailed(float x0, float x1, float x2, float halfsize)
  {
    float min = x0;
    float max = x0;
    if (x1 < min)
      min = x1;
    if (x1 > max)
      max = x1;
    if (x2 < min)
      min = x2;
    if (x2 > max)
      max = x2;
    return (min > halfsize || max < -halfsize) ? true : false;
  }

  private static bool PlaneBoxOverlap(Vector3 normal, Vector3 vert, Vector3 boxhalfsize)
  {
    Vector3 vmin = Vector3.zero;
    Vector3 vmax = Vector3.zero;
    for (int q = 0; q < 3; q++)
    {
      float v = vert[q];
      if (normal[q] > 0.0f)
      {
        vmin[q] = -boxhalfsize[q] - v;
        vmax[q] = boxhalfsize[q] - v;
      }
      else
      {
        vmin[q] = boxhalfsize[q] - v;
        vmax[q] = -boxhalfsize[q] - v;
      }
    }
    if (Vector3.Dot(normal, vmin) > 0.0f)
      return false;
    if (Vector3.Dot(normal, vmax) >= 0.0f)
      return true;
    return false;
  }

  private static bool TriangleBoxTest(Vector3 boxcenter, Vector3 boxhalfsize, Vector3 v0, Vector3 v1, Vector3 v2)
  {
    // Center the vertices about the box origin
    v0 -= boxcenter;
    v1 -= boxcenter;
    v2 -= boxcenter;

    Vector3[] v = { v0, v1, v2 };

    // Compute triangle edges
    Vector3 e0 = v1 - v0;
    Vector3 e1 = v2 - v1;
    Vector3 e2 = v0 - v2;

    // Bullet 3: 9 tests
    float fex = Mathf.Abs(e0.x);
    float fey = Mathf.Abs(e0.y);
    float fez = Mathf.Abs(e0.z);
    if (AxisTestX01Failed(v, boxhalfsize, e0.z, e0.y, fez, fey))
      return false;
    if (AxisTestY02Failed(v, boxhalfsize, e0.z, e0.x, fez, fex))
      return false;
    if (AxisTestZ12Failed(v, boxhalfsize, e0.y, e0.x, fey, fex))
      return false;
    fex = Mathf.Abs(e1.x);
    fey = Mathf.Abs(e1.y);
    fez = Mathf.Abs(e1.z);
    if (AxisTestX01Failed(v, boxhalfsize, e1.z, e1.y, fez, fey))
      return false;
    if (AxisTestY02Failed(v, boxhalfsize, e1.z, e1.x, fez, fex))
      return false;
    if (AxisTestZ0Failed(v, boxhalfsize, e1.y, e1.x, fey, fex))
      return false;
    fex = Mathf.Abs(e2.x);
    fey = Mathf.Abs(e2.y);
    fez = Mathf.Abs(e2.z);
    if (AxisTestX2Failed(v, boxhalfsize, e2.z, e2.y, fez, fey))
      return false;
    if (AxisTestY1Failed(v, boxhalfsize, e2.z, e2.x, fez, fex))
      return false;
    if (AxisTestZ12Failed(v, boxhalfsize, e2.y, e2.x, fey, fex))
      return false;

    // Bullet 1: Test overlap in {x,y,z}-directions. Find min/max of the
    // triangles in each direction, and test for overlap in that direction.
    // This is equivalent to testing a minimal AABB around the triangle against
    // the AABB.
    if (AxialOverlapTestFailed(v0.x, v1.x, v2.x, boxhalfsize.x)) // test x direction
      return false;
    if (AxialOverlapTestFailed(v0.y, v1.y, v2.y, boxhalfsize.y)) // test y direction
      return false;
    if (AxialOverlapTestFailed(v0.z, v1.z, v2.z, boxhalfsize.z)) // test z direction
      return false;

    // Bullet 2: Test if the box intersects the plane of the triangle.
    Vector3 normal = Vector3.Cross(e0, e1);
    return PlaneBoxOverlap(normal, v0, boxhalfsize);
  }

  private static Bounds ComputeAABB(OrientedBoundingBox obb)
  {
    Vector3[] points = new Vector3[8];
    Vector3 center = obb.Center;  // already in world coordinates
    points[0] = center + obb.Rotation * new Vector3(obb.Extents.x, obb.Extents.y, obb.Extents.z);
    points[1] = center + obb.Rotation * new Vector3(obb.Extents.x, obb.Extents.y, -obb.Extents.z);
    points[2] = center + obb.Rotation * new Vector3(obb.Extents.x, -obb.Extents.y, obb.Extents.z);
    points[3] = center + obb.Rotation * new Vector3(obb.Extents.x, -obb.Extents.y, -obb.Extents.z);
    points[4] = center + obb.Rotation * new Vector3(-obb.Extents.x, obb.Extents.y, obb.Extents.z);
    points[5] = center + obb.Rotation * new Vector3(-obb.Extents.x, obb.Extents.y, -obb.Extents.z);
    points[6] = center + obb.Rotation * new Vector3(-obb.Extents.x, -obb.Extents.y, obb.Extents.z);
    points[7] = center + obb.Rotation * new Vector3(-obb.Extents.x, -obb.Extents.y, -obb.Extents.z);
    float minX = float.PositiveInfinity;
    float minY = float.PositiveInfinity;
    float minZ = float.PositiveInfinity;
    float maxX = float.NegativeInfinity;
    float maxY = float.NegativeInfinity;
    float maxZ = float.NegativeInfinity;
    foreach (Vector3 point in points)
    {
      minX = Mathf.Min(minX, point.x);
      maxX = Mathf.Max(maxX, point.x);
      minY = Mathf.Min(minY, point.y);
      maxY = Mathf.Max(maxY, point.y);
      minZ = Mathf.Min(minZ, point.z);
      maxZ = Mathf.Max(maxZ, point.z);
    }
    return new Bounds(center, new Vector3(maxX - minX, maxY - minY, maxZ - minZ));
  }

  private static Bounds ComputeAABB(BoxCollider obb)
  {
    // Naive method using 8 points from OBB (could be improved!)
    Transform xform = obb.transform;
    Vector3[] points = new Vector3[8];
    Vector3 center = xform.TransformPoint(obb.center);
    points[0] = center + xform.TransformVector(0.5f * new Vector3(obb.size.x, obb.size.y, obb.size.z));
    points[1] = center + xform.TransformVector(0.5f * new Vector3(obb.size.x, obb.size.y, -obb.size.z));
    points[2] = center + xform.TransformVector(0.5f * new Vector3(obb.size.x, -obb.size.y, obb.size.z));
    points[3] = center + xform.TransformVector(0.5f * new Vector3(obb.size.x, -obb.size.y, -obb.size.z));
    points[4] = center + xform.TransformVector(0.5f * new Vector3(-obb.size.x, obb.size.y, obb.size.z));
    points[5] = center + xform.TransformVector(0.5f * new Vector3(-obb.size.x, obb.size.y, -obb.size.z));
    points[6] = center + xform.TransformVector(0.5f * new Vector3(-obb.size.x, -obb.size.y, obb.size.z));
    points[7] = center + xform.TransformVector(0.5f * new Vector3(-obb.size.x, -obb.size.y, -obb.size.z));
    float minX = float.PositiveInfinity;
    float minY = float.PositiveInfinity;
    float minZ = float.PositiveInfinity;
    float maxX = float.NegativeInfinity;
    float maxY = float.NegativeInfinity;
    float maxZ = float.NegativeInfinity;
    foreach (Vector3 point in points)
    {
      minX = Mathf.Min(minX, point.x);
      maxX = Mathf.Max(maxX, point.x);
      minY = Mathf.Min(minY, point.y);
      maxY = Mathf.Max(maxY, point.y);
      minZ = Mathf.Min(minZ, point.z);
      maxZ = Mathf.Max(maxZ, point.z);
    }
    return new Bounds(center, new Vector3(maxX - minX, maxY - minY, maxZ - minZ));
  }

  public static List<int> FindTriangles(OrientedBoundingBox obb, Vector3[] meshVerts, int[] triangleIndices, Transform meshTransform)
  {
    //System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
    //stopwatch.Reset();
    //stopwatch.Start();

    int[] indices = triangleIndices;
    int expectedNumIntersecting = Math.Max(1, indices.Length / 10); // assume 10% will intersect
    List<int> intersectingTriangles = new List<int>(expectedNumIntersecting);
    Vector3 boxcenter = Vector3.zero;
    Vector3 boxhalfsize = obb.Extents;

    // Gross test using AABB
    Bounds aabb = ComputeAABB(obb);
    if (!aabb.Intersects(meshTransform.gameObject.GetComponent<Renderer>().bounds))
      return intersectingTriangles;

    // Rotate the mesh into OBB-local space so we can perform axis-aligned
    // testing (because the OBB then becomes an AABB in its local space)
    Vector3[] verts = new Vector3[meshVerts.Length];
    for (int i = 0; i < meshVerts.Length; i++)
    {
      // Transform mesh 1) mesh local -> world, 2) world -> OBB local
      Vector3 worldVertex = meshTransform.TransformPoint(meshVerts[i]);
      verts[i] = TransformWorldToOBB(worldVertex, obb);
    }
    
    // Test each triangle in the mesh
    for (int i = 0; i < indices.Length; i += 3)
    {
      int i0 = indices[i + 0];
      int i1 = indices[i + 1];
      int i2 = indices[i + 2];
      if (TriangleBoxTest(boxcenter, boxhalfsize, verts[i0], verts[i1], verts[i2]))
      {
        intersectingTriangles.Add(i0);
        intersectingTriangles.Add(i1);
        intersectingTriangles.Add(i2);
      }
    }

    //stopwatch.Stop();
    //Debug.Log("Elapsed time=" + stopwatch.ElapsedTicks + " ticks (" + (double)(stopwatch.ElapsedTicks) / (double)(System.Diagnostics.Stopwatch.Frequency) + " sec)");
    return intersectingTriangles;
  }

  public delegate void ResultsCallback(List<int> intersectingTriangles_found);

  public static IEnumerator FindTrianglesCoroutine(ResultsCallback callback, float maxSecondsPerFrame, OrientedBoundingBox obb, Vector3[] meshVerts, int[] triangleIndices, Transform meshTransform)
  {
    float t0 = Time.realtimeSinceStartup;

    int[] indices = triangleIndices;
    int expectedNumIntersecting = Math.Max(1, indices.Length / 10); // assume 10% will intersect
    List<int> intersectingTriangles = new List<int>(expectedNumIntersecting);

    Vector3 boxcenter = Vector3.zero; // OBB is symmetric about its center point
    Vector3 boxhalfsize = obb.Extents;

    // Gross test using AABB
    Bounds aabb = ComputeAABB(obb);
    if (!aabb.Intersects(meshTransform.gameObject.GetComponent<Renderer>().bounds))
    {
      callback(intersectingTriangles);
      yield break;
    }

    // Rotate the mesh into OBB-local space so we can perform axis-aligned
    // testing (because the OBB then becomes an AABB in its local space).
    Vector3[] verts = new Vector3[meshVerts.Length];
    for (int i = 0; i < meshVerts.Length; i++)
    {
      // Transform mesh: 1) mesh local -> world, 2) world -> OBB local
      Vector3 worldVertex = meshTransform.TransformPoint(meshVerts[i]);
      verts[i] = TransformWorldToOBB(worldVertex, obb);// obb.transform.InverseTransformPoint(worldVertex);
      if (Time.realtimeSinceStartup - t0 >= maxSecondsPerFrame)
      {
        yield return null;
        t0 = Time.realtimeSinceStartup;
      }
    }

    // Test each triangle in the mesh
    for (int i = 0; i < indices.Length; i += 3)
    {
      int i0 = indices[i + 0];
      int i1 = indices[i + 1];
      int i2 = indices[i + 2];
      if (TriangleBoxTest(boxcenter, boxhalfsize, verts[i0], verts[i1], verts[i2]))
      {
        intersectingTriangles.Add(i0);
        intersectingTriangles.Add(i1);
        intersectingTriangles.Add(i2);
      }
      if (Time.realtimeSinceStartup - t0 >= maxSecondsPerFrame)
      {
        yield return null;
        t0 = Time.realtimeSinceStartup;
      }
    }

    // Pass result to caller
    callback(intersectingTriangles);
  }
}
