/*
 * Based on the method described in "Fast 3D Triangle-Box Overlap Testing" by
 * Tomas Akenine-Moller (2001). The code here is a direct C -> C# conversion of
 * Moller's implementation, which can be found along with the paper here:
 * http://fileadmin.cs.lth.se/cs/personal/tomas_akenine-moller/code/
 * 
 * Performance optimization TO-DO:
 * -------------------------------
 * - Copy all surface mesh vertices and indices over at the beginning so we don't
 *   have to perform this operation frequently.
 * - Add code to do this in background and yield.
 */

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public static class OBBMeshIntersection
{
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

  public static List<int> FindTriangles(BoxCollider obb, Vector3[] mesh_verts, int[] triangle_indices, Transform mesh_transform)
  {
    System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
    stopwatch.Reset();
    stopwatch.Start();

    int[] indices = triangle_indices;
    int expected_num_intersecting = Math.Max(1, indices.Length / 10); // assume 10% will intersect
    List<int> intersecting_triangles = new List<int>(expected_num_intersecting);
    Vector3 boxcenter = obb.center;
    Vector3 boxhalfsize = obb.size * 0.5f;

    // Gross test using AABB
    //obb.enabled = true; // TODO: still need to solve weird OBB issue
    if (!obb.bounds.Intersects(mesh_transform.gameObject.GetComponent<Renderer>().bounds))
      return intersecting_triangles;

    // Rotate the mesh into OBB-local space so we can perform axis-aligned
    // testing (because the OBB then becomes an AABB in its local space)
    Vector3[] verts = new Vector3[mesh_verts.Length];
    for (int i = 0; i < mesh_verts.Length; i++)
    {
      // Transform mesh 1) mesh local -> world, 2) world -> OBB local
      Vector3 world_vertex = mesh_transform.TransformPoint(mesh_verts[i]);
      verts[i] = obb.transform.InverseTransformPoint(world_vertex);
    }
    
    // Test each triangle in the mesh
    for (int i = 0; i < indices.Length; i += 3)
    {
      int i0 = indices[i + 0];
      int i1 = indices[i + 1];
      int i2 = indices[i + 2];
      if (TriangleBoxTest(boxcenter, boxhalfsize, verts[i0], verts[i1], verts[i2]))
      {
        intersecting_triangles.Add(i0);
        intersecting_triangles.Add(i1);
        intersecting_triangles.Add(i2);
      }
    }

    stopwatch.Stop();
    Debug.Log("Elapsed time=" + stopwatch.ElapsedTicks + " ticks (" + (double)(stopwatch.ElapsedTicks) / (double)(System.Diagnostics.Stopwatch.Frequency) + " sec)");
    return intersecting_triangles;
  }

  public delegate void ResultsCallback(List<int> intersecting_triangles_found);

  public static IEnumerator FindTrianglesCoroutine(ResultsCallback callback, float maxSecondsPerFrame, BoxCollider obb, Vector3[] mesh_verts, int[] triangle_indices, Transform mesh_transform)
  {
    float t0 = Time.realtimeSinceStartup;

    int[] indices = triangle_indices;
    int expected_num_intersecting = Math.Max(1, indices.Length / 10); // assume 10% will intersect
    List<int> intersecting_triangles = new List<int>(expected_num_intersecting);
    Vector3 boxcenter = obb.center;
    Vector3 boxhalfsize = obb.size * 0.5f;

    // Gross test using AABB
    //obb.enabled = true; // TODO: still need to solve weird OBB issue
    if (!obb.bounds.Intersects(mesh_transform.gameObject.GetComponent<Renderer>().bounds))
    {
      callback(intersecting_triangles);
      yield break;
    }

    // Rotate the mesh into OBB-local space so we can perform axis-aligned
    // testing (because the OBB then becomes an AABB in its local space).
    Vector3[] verts = new Vector3[mesh_verts.Length];
    for (int i = 0; i < mesh_verts.Length; i++)
    {
      // Transform mesh: 1) mesh local -> world, 2) world -> OBB local
      Vector3 world_vertex = mesh_transform.TransformPoint(mesh_verts[i]);
      verts[i] = obb.transform.InverseTransformPoint(world_vertex);
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
        intersecting_triangles.Add(i0);
        intersecting_triangles.Add(i1);
        intersecting_triangles.Add(i2);
      }
      if (Time.realtimeSinceStartup - t0 >= maxSecondsPerFrame)
      {
        yield return null;
        t0 = Time.realtimeSinceStartup;
      }
    }

    // Pass result to caller
    callback(intersecting_triangles);
  }
}
