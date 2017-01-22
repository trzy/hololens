using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
/// <summary>
/// RemoveSurfaceVertices will remove any vertices from the Spatial Mapping Mesh that fall within the bounding volume.
/// This can be used to create holes in the environment, or to help reduce triangle count after finding planes.
/// </summary>
public class RemoveSurfaceVertices : HoloToolkit.Unity.Singleton<RemoveSurfaceVertices>
{
  class WorldSpaceBounds
  {
    private Transform m_xform;
    private Vector3 m_center;
    private Vector3 m_extents2;

    public bool ContainsPoint(Vector3 point)
    {
      // Transform point from world to local, get distance to bounds center
      Vector3 local_point = m_xform.InverseTransformPoint(point);
      Vector3 distance = local_point - m_center;

      // Square to get an absolute value and compare against limits
      distance.x *= distance.x;
      distance.y *= distance.y;
      distance.z *= distance.z;
      if (distance.x > m_extents2.x || distance.y > m_extents2.y || distance.z > m_extents2.z)
        return false;
      return true;
    }

    public WorldSpaceBounds(BoxCollider collider)
    {
      // Find the transform to use to translate from local to world space
      m_xform = collider.transform ? collider.transform : collider.gameObject.transform;

      // Do everything in squared units to serve as a quick absolute value (no
      // need to test negative extents in this case)
      m_extents2 = new Vector3(collider.size.x * 0.5f * collider.size.x * 0.5f, collider.size.y * 0.5f * collider.size.y * 0.5f, collider.size.z * 0.5f * collider.size.z * 0.5f);
    }
  }

  [Tooltip("The amount, if any, to expand each bounding volume by.")]
  public float BoundsExpansion = 0.0f;

  /// <summary>
  /// Delegate which is called when the RemoveVerticesComplete event is triggered.
  /// </summary>
  /// <param name="source"></param>
  /// <param name="args"></param>
  public delegate void EventHandler(object source, EventArgs args);

  /// <summary>
  /// EventHandler which is triggered when the RemoveSurfaceVertices is finished.
  /// </summary>
  public event EventHandler RemoveVerticesComplete;

  /// <summary>
  /// Indicates if RemoveSurfaceVertices is currently removing vertices from the Spatial Mapping Mesh.
  /// </summary>
  private bool removingVerts = false;

  /// <summary>
  /// Queue of bounding objects to remove surface vertices from.
  /// Bounding objects are queued so that RemoveSurfaceVerticesWithinBounds can be called even when the previous task has not finished.
  /// </summary>
  private Queue<WorldSpaceBounds> boundingObjectsQueue;

#if UNITY_EDITOR
  /// <summary>
  /// How much time (in sec), while running in the Unity Editor, to allow RemoveSurfaceVertices to consume before returning control to the main program.
  /// </summary>
  private static readonly float FrameTime = .016f;
#else
        /// <summary>
        /// How much time (in sec) to allow RemoveSurfaceVertices to consume before returning control to the main program.
        /// </summary>
        private static readonly float FrameTime = .008f;
#endif

  // GameObject initialization.
  private void Start()
  {
    boundingObjectsQueue = new Queue<WorldSpaceBounds>();
    removingVerts = false;
  }

  /// <summary>
  /// Removes portions of the surface mesh that exist within the bounds of the boundingObjects.
  /// </summary>
  /// <param name="boundingObjects">Collection of GameObjects that define the bounds where spatial mesh vertices should be removed.</param>
  public void RemoveSurfaceVerticesWithinBounds(IEnumerable<GameObject> boundingObjects)
  {
    if (boundingObjects == null)
    {
      return;
    }

    if (!removingVerts)
    {
      removingVerts = true;
      AddBoundingObjectsToQueue(boundingObjects);

      // We use Coroutine to split the work across multiple frames and avoid impacting the frame rate too much.
      StartCoroutine(RemoveSurfaceVerticesWithinBoundsRoutine());
    }
    else
    {
      // Add new boundingObjects to end of queue.
      AddBoundingObjectsToQueue(boundingObjects);
    }
  }

  /// <summary>
  /// Adds new bounding objects to the end of the Queue.
  /// </summary>
  /// <param name="boundingObjects">Collection of GameObjects which define the bounds where spatial mesh vertices should be removed.</param>
  private void AddBoundingObjectsToQueue(IEnumerable<GameObject> boundingObjects)
  {
    foreach (GameObject item in boundingObjects)
    {
      BoxCollider boxCollider = item.GetComponent<BoxCollider>();
      if (boxCollider != null)
      {
        // Expand the bounds, if requested.
        if (BoundsExpansion > 0.0f)
        {
          Debug.Log("Bounds expansion in world coordinates not yet supported!");
        }
        WorldSpaceBounds bounds = new WorldSpaceBounds(boxCollider);
        boundingObjectsQueue.Enqueue(bounds);
      }
    }
  }

  /// <summary>
  /// Iterator block, analyzes surface meshes to find vertices existing within the bounds of any boundingObject and removes them.
  /// </summary>
  /// <returns>Yield result.</returns>
  private IEnumerator RemoveSurfaceVerticesWithinBoundsRoutine()
  {
    List<MeshFilter> meshFilters = HoloToolkit.Unity.SpatialMappingManager.Instance.GetMeshFilters();
    float start = Time.realtimeSinceStartup;

    while (boundingObjectsQueue.Count > 0)
    {
      // Get the current boundingObject.
      WorldSpaceBounds worldSpaceBounds = boundingObjectsQueue.Dequeue();

      foreach (MeshFilter filter in meshFilters)
      {
        // Since this is amortized across frames, the filter can be destroyed by the time
        // we get here.
        if (filter == null)
        {
          continue;
        }

        Mesh mesh = filter.sharedMesh;

        if (mesh != null && !worldSpaceBounds.AABBIntersects(mesh.bounds))
        {
          // We don't need to do anything to this mesh, move to the next one.
          continue;
        }

        // Remove vertices from any mesh that intersects with the bounds.
        Vector3[] verts = mesh.vertices;
        List<int> vertsToRemove = new List<int>();

        // Find which mesh vertices are within the bounds.
        for (int i = 0; i < verts.Length; ++i)
        {
          if (worldSpaceBounds.ContainsPoint(verts[i]))
          {
            // These vertices are within bounds, so mark them for removal.
            vertsToRemove.Add(i);
          }

          // If too much time has passed, we need to return control to the main game loop.
          if ((Time.realtimeSinceStartup - start) > FrameTime)
          {
            // Pause our work here, and continue finding vertices to remove on the next frame.
            yield return null;
            start = Time.realtimeSinceStartup;
          }
        }

        if (vertsToRemove.Count == 0)
        {
          // We did not find any vertices to remove, so move to the next mesh.
          continue;
        }

        // We found vertices to remove, so now we need to remove any triangles that reference these vertices.
        int[] indices = mesh.GetTriangles(0);
        List<int> updatedIndices = new List<int>();

        for (int index = 0; index < indices.Length; index += 3)
        {
          // Each triangle utilizes three slots in the index buffer, check to see if any of the
          // triangle indices contain a vertex that should be removed.
          if (vertsToRemove.Contains(indices[index]) ||
              vertsToRemove.Contains(indices[index + 1]) ||
              vertsToRemove.Contains(indices[index + 2]))
          {
            // Do nothing, we don't want to save this triangle...
          }
          else
          {
            // Every vertex in this triangle is good, so let's save it.
            updatedIndices.Add(indices[index]);
            updatedIndices.Add(indices[index + 1]);
            updatedIndices.Add(indices[index + 2]);
          }

          // If too much time has passed, we need to return control to the main game loop.
          if ((Time.realtimeSinceStartup - start) > FrameTime)
          {
            // Pause our work, and continue making additional planes on the next frame.
            yield return null;
            start = Time.realtimeSinceStartup;
          }
        }

        if (indices.Length == updatedIndices.Count)
        {
          // None of the verts to remove were being referenced in the triangle list.
          continue;
        }

        // Update mesh to use the new triangles.
        mesh.SetTriangles(updatedIndices.ToArray(), 0);
        mesh.RecalculateBounds();
        yield return null;
        start = Time.realtimeSinceStartup;

        // Reset the mesh collider to fit the new mesh.
        MeshCollider collider = filter.gameObject.GetComponent<MeshCollider>();
        if (collider != null)
        {
          collider.sharedMesh = null;
          collider.sharedMesh = mesh;
        }
      }
    }

    Debug.Log("Finished removing vertices.");

    // We are done removing vertices, trigger an event.
    EventHandler handler = RemoveVerticesComplete;
    if (handler != null)
    {
      handler(this, EventArgs.Empty);
    }

    removingVerts = false;
  }
}
*/

public class TriangleRemoval: HoloToolkit.Unity.Singleton<TriangleRemoval>
{
  public GameObject testBoundsObject;
  public GameObject testObject;

  private System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();

  class WorldSpaceBounds
  {
    private BoxCollider m_collider;
    private Vector3 m_local_center; // micro-optimization: caching this here instead of accessing m_collider.center actually helps
    private Transform m_xform;
    private Vector3 m_extents2;
    

    /*
    public Plane[] planes = new Plane[6]; // planes that define the volume of the box

    public bool ContainsPoint(Vector3 point)
    {
      foreach (Plane plane in planes)
      {
        if (plane.GetSide(point))
          return false;
      }
      return true;
    }
    */

    public bool ContainsPoint(Vector3 point)
    {
      // Transform point from world to local
      Vector3 local_point = m_xform.InverseTransformPoint(point);
      Vector3 distance = local_point - m_local_center;
      distance.x *= distance.x;
      distance.y *= distance.y;
      distance.z *= distance.z;
      if (distance.x > m_extents2.x || distance.y > m_extents2.y || distance.z > m_extents2.z)
        return false;
      return true;
    }

    public bool AABBIntersects(Bounds bounds)
    {
      return m_collider.bounds.Intersects(bounds);
    }

    public WorldSpaceBounds(BoxCollider collider)
    {
      // Find the transform to use to translate from local to world space
      m_collider = collider;
      m_local_center = collider.center;
      m_xform = collider.transform ? collider.transform : collider.gameObject.transform;
      Vector3 extents = 0.5f * (collider.size - collider.center); // local axis-aligned extents (distances from center to each side of box)
      m_extents2 = new Vector3(extents.x * extents.x, extents.y * extents.y, extents.z * extents.z);

      // Plane method is the slowest
      /*
      // Compute vectors in local space from center to sides of box
      Vector3 local_cx = Vector3.right * 0.5f * collider.size.x;
      Vector3 local_cy = Vector3.up * 0.5f * collider.size.y;
      Vector3 local_cz = Vector3.forward * 0.5f * collider.size.z;

      // Transform the vectors as well as the center point to world space
      Vector3 center = xform.TransformPoint(collider.center);
      Vector3 cx = xform.TransformVector(local_cx);
      Vector3 cy = xform.TransformVector(local_cy);
      Vector3 cz = xform.TransformVector(local_cz);

      // Compute normals
      Vector3 nx = Vector3.Normalize(cx);
      Vector3 ny = Vector3.Normalize(cy);
      Vector3 nz = Vector3.Normalize(cz);

      // Define the 6 planes
      planes[0] = new Plane(nx, center + cx);
      planes[1] = new Plane(ny, center + cy);
      planes[2] = new Plane(nz, center + cz);
      planes[3] = new Plane(-nx, center - cx);
      planes[4] = new Plane(-ny, center - cy);
      planes[5] = new Plane(-nz, center - cz);

      // Position small cubes to visualize
      Vector3 size = new Vector3(1, 1, 1);

      GameObject cube1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
      cube1.transform.parent = null;
      cube1.transform.localScale = size;
      cube1.transform.position = center + cx;
      cube1.transform.transform.rotation = Quaternion.LookRotation(nx);

      GameObject cube2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
      cube2.transform.parent = null;
      cube2.transform.localScale = size;
      cube2.transform.position = center + cy;
      cube2.transform.transform.rotation = Quaternion.LookRotation(ny);

      GameObject cube3 = GameObject.CreatePrimitive(PrimitiveType.Cube);
      cube3.transform.parent = null;
      cube3.transform.localScale = size;
      cube3.transform.position = center + cz;
      cube3.transform.transform.rotation = Quaternion.LookRotation(nz);
      */
    }
  }

  [Tooltip("The amount, if any, to expand each bounding volume by.")]
  public float BoundsExpansion = 0.0f;

  /// <summary>
  /// Delegate which is called when the RemoveVerticesComplete event is triggered.
  /// </summary>
  /// <param name="source"></param>
  /// <param name="args"></param>
  public delegate void EventHandler(object source, EventArgs args);

  /// <summary>
  /// EventHandler which is triggered when the RemoveSurfaceVertices is finished.
  /// </summary>
  public event EventHandler RemoveVerticesComplete;

  /// <summary>
  /// Indicates if RemoveSurfaceVertices is currently removing vertices from the Spatial Mapping Mesh.
  /// </summary>
  private bool removingVerts = false;

  /// <summary>
  /// Queue of bounding objects to remove surface vertices from.
  /// Bounding objects are queued so that RemoveSurfaceVerticesWithinBounds can be called even when the previous task has not finished.
  /// </summary>
  private Queue<WorldSpaceBounds> boundingObjectsQueue;

  /// Cumulative number of triangles removed
  private int m_triangles_removed = 0;
  private int m_total_triangles = 0;

#if UNITY_EDITOR
  /// <summary>
  /// How much time (in sec), while running in the Unity Editor, to allow RemoveSurfaceVertices to consume before returning control to the main program.
  /// </summary>
  private static readonly float FrameTime = .016f;
#else
        /// <summary>
        /// How much time (in sec) to allow RemoveSurfaceVertices to consume before returning control to the main program.
        /// </summary>
        private static readonly float FrameTime = .008f;
#endif

  // GameObject initialization.
  private void Start()
  {
    boundingObjectsQueue = new Queue<WorldSpaceBounds>();
    removingVerts = false;
  }

  /// <summary>
  /// Removes portions of the surface mesh that exist within the bounds of the boundingObjects.
  /// </summary>
  /// <param name="boundingObjects">Collection of GameObjects that define the bounds where spatial mesh vertices should be removed.</param>
  public void RemoveSurfaceVerticesWithinBounds(IEnumerable<GameObject> boundingObjects)
  {
    if (boundingObjects == null)
    {
      return;
    }

    if (!removingVerts)
    {
      removingVerts = true;
      AddBoundingObjectsToQueue(boundingObjects);

      // We use Coroutine to split the work across multiple frames and avoid impacting the frame rate too much.
      StartCoroutine(RemoveSurfaceVerticesWithinBoundsRoutine());
    }
    else
    {
      // Add new boundingObjects to end of queue.
      AddBoundingObjectsToQueue(boundingObjects);
    }
  }

  /// <summary>
  /// Adds new bounding objects to the end of the Queue.
  /// </summary>
  /// <param name="boundingObjects">Collection of GameObjects which define the bounds where spatial mesh vertices should be removed.</param>
  private void AddBoundingObjectsToQueue(IEnumerable<GameObject> boundingObjects)
  {
    foreach (GameObject item in boundingObjects)
    {
      BoxCollider boxCollider = item.GetComponent<BoxCollider>();
      if (boxCollider != null)
      {
        // Expand the bounds, if requested.
        if (BoundsExpansion > 0.0f)
        {
          Debug.Log("Bounds expansion in world coordinates not yet supported!");
        }
        WorldSpaceBounds bounds = new WorldSpaceBounds(boxCollider);
        boundingObjectsQueue.Enqueue(bounds);
      }
    }
  }

  /// <summary>
  /// Iterator block, analyzes surface meshes to find vertices existing within the bounds of any boundingObject and removes them.
  /// </summary>
  /// <returns>Yield result.</returns>
  private IEnumerator RemoveSurfaceVerticesWithinBoundsRoutine()
  {
    List<MeshFilter> meshFilters = HoloToolkit.Unity.SpatialMapping.SpatialMappingManager.Instance.GetMeshFilters();
    float start = Time.realtimeSinceStartup;

    while (boundingObjectsQueue.Count > 0)
    {
      // Get the current boundingObject.
      WorldSpaceBounds worldSpaceBounds = boundingObjectsQueue.Dequeue();

      foreach (MeshFilter filter in meshFilters)
      {
        // Since this is amortized across frames, the filter can be destroyed by the time
        // we get here.
        if (filter == null)
        {
          continue;
        }

        Mesh mesh = filter.sharedMesh;

        if (mesh != null && !worldSpaceBounds.AABBIntersects(mesh.bounds))
        {
          // We don't need to do anything to this mesh, move to the next one.
          continue;
        }

        // Remove vertices from any mesh that intersects with the bounds.
        Vector3[] verts = mesh.vertices;
        List<int> vertsToRemove = new List<int>();

        // Find which mesh vertices are within the bounds.
        for (int i = 0; i < verts.Length; ++i)
        {
          if (worldSpaceBounds.ContainsPoint(verts[i]))
          {
            // These vertices are within bounds, so mark them for removal.
            vertsToRemove.Add(i);
          }

          // If too much time has passed, we need to return control to the main game loop.
          if ((Time.realtimeSinceStartup - start) > FrameTime)
          {
            // Pause our work here, and continue finding vertices to remove on the next frame.
            yield return null;
            start = Time.realtimeSinceStartup;
          }
        }

        if (vertsToRemove.Count == 0)
        {
          // We did not find any vertices to remove, so move to the next mesh.
          continue;
        }

        // We found vertices to remove, so now we need to remove any triangles that reference these vertices.
        int[] indices = mesh.GetTriangles(0);
        List<int> updatedIndices = new List<int>();

        for (int index = 0; index < indices.Length; index += 3)
        {
          // Each triangle utilizes three slots in the index buffer, check to see if any of the
          // triangle indices contain a vertex that should be removed.
          if (vertsToRemove.Contains(indices[index]) ||
              vertsToRemove.Contains(indices[index + 1]) ||
              vertsToRemove.Contains(indices[index + 2]))
          {
            // Do nothing, we don't want to save this triangle...
          }
          else
          {
            // Every vertex in this triangle is good, so let's save it.
            updatedIndices.Add(indices[index]);
            updatedIndices.Add(indices[index + 1]);
            updatedIndices.Add(indices[index + 2]);
          }

          // If too much time has passed, we need to return control to the main game loop.
          if ((Time.realtimeSinceStartup - start) > FrameTime)
          {
            // Pause our work, and continue making additional planes on the next frame.
            yield return null;
            start = Time.realtimeSinceStartup;
          }
        }

        if (indices.Length == updatedIndices.Count)
        {
          // None of the verts to remove were being referenced in the triangle list.
          continue;
        }

        m_total_triangles += indices.Length / 3;
        m_triangles_removed += (indices.Length - updatedIndices.Count) / 3;

        // Update mesh to use the new triangles.
        mesh.SetTriangles(updatedIndices.ToArray(), 0);
        mesh.RecalculateBounds();
        yield return null;
        start = Time.realtimeSinceStartup;

        // Reset the mesh collider to fit the new mesh.
        MeshCollider collider = filter.gameObject.GetComponent<MeshCollider>();
        if (collider != null)
        {
          collider.sharedMesh = null;
          collider.sharedMesh = mesh;
        }
      }
    }

    float pct_removed = 100f * (float) m_triangles_removed / (float) m_total_triangles;
    Debug.Log("Finished removing " + m_triangles_removed + " triangles (" + pct_removed + "%).");

    // We are done removing vertices, trigger an event.
    EventHandler handler = RemoveVerticesComplete;
    if (handler != null)
    {
      handler(this, EventArgs.Empty);
    }

    removingVerts = false;
  }

  /*
  void Start ()
  {
    Debug.Log("High resolution stopwatch" + (System.Diagnostics.Stopwatch.IsHighResolution ? " " : " *not* ") + "available");
    long nanos_per_tick = (1000L * 1000L * 1000L) / System.Diagnostics.Stopwatch.Frequency;
    Debug.Log("Resolution: " + nanos_per_tick + " ns");

    WorldSpaceBounds box = new WorldSpaceBounds(testBoundsObject.GetComponent<BoxCollider>());
    Debug.Log(box.ContainsPoint(testObject.transform.position) ? "INSIDE" : "OUTSIDE");
    Debug.Log("Alt test: " + (testBoundsObject.GetComponent<BoxCollider>().bounds.Contains(testObject.transform.position) ? "INSIDE" : "OUTSIDE"));
    Plane p = new Plane(Vector3.up, Vector3.zero);
    Vector3 pt = new Vector3(1, -10, 1);
    Debug.Log(p.GetSide(pt) ? "Above" : "Below");

    stopwatch.Reset();
    stopwatch.Start();
    bool result = false;
    for (int i = 0; i < 10000; i++)
      result |= box.ContainsPoint(testObject.transform.position);
    stopwatch.Stop();
    Debug.Log(result);
    Debug.Log("Elapsed time=" + stopwatch.ElapsedTicks + " ticks (" + (double) (stopwatch.ElapsedTicks) / (double) (System.Diagnostics.Stopwatch.Frequency) + " sec)");
  }
  */
}
