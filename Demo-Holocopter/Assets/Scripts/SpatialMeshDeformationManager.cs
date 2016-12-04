/*
 * Notes:
 * ------
 * - Spatial meshes are assumed to be at world scale (i.e., a scale of 1, 1, 1)
 *   but can otherwise have non-zero rotation and position.
 * - The SpatialMeshDeformationManager must have no transform applied.
 * 
 * TODO:
 * -----
 * - Assert that no transform is applied to SpatialMeshDeformationManager and
 *   that it is positioned at (0, 0, 0).
 * - Assert that spatial meshes have no scaling applied.
 */
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class SpatialMeshDeformationManager: HoloToolkit.Unity.Singleton<SpatialMeshDeformationManager>
{
  [Tooltip("Extra displacement (in meters) to push spatial mesh polygons beyond the back-plane of an object.")]
  public float extraDisplacement = 0.02f;

  [Tooltip("Whether to create patches containing original, un-displaced vertices. Needed to restore vertices.")]
  public bool createPatches = false;

  [Tooltip("Whether to render (and enable mesh colliders for) vertex patches.")]
  public bool renderPatches = false;

  [Tooltip("Render queue value to use for spatial meshes (existing value will be overwritten)")]
  public int spatialMeshRenderQueueValue = 1500;

  [Tooltip("Render queue value for spatial mesh patches (higher value than base spatial mesh and embedded objects)")]
  public int patchMeshRenderQueueValue = 1999;

  [Tooltip("Render queue value to use for drawing freshly-embeded objects before spatial mesh.")]
  public int highPriorityRenderQueueValue = 1000;

  // Restoring original materials:
  // http://answers.unity3d.com/questions/44108/changing-and-resetting-materials-dynamically.html
  // TODO: check instance IDs to see if they change back. We don't want to "restore" and just end
  //       up creating *another* instantiation of the shared materials.

  private class WorkUnit
  {
    public Renderer renderer = null;
    public Material[] originalMaterials = null;
    public WorkUnit(GameObject obj)
    {
      renderer = obj.GetComponent<Renderer>();
      originalMaterials = renderer ? renderer.materials : null;
    }
  }

  private int m_old_spatial_mesh_render_queue_value = 0;
  private List<MeshFilter> m_spatial_mesh_filters = null;
  //private Dictionary<int, SavedRenderOrder> m_freshly_inserted_by_id = null;  // freshly-inserted objects w/ modified render queue values are drawn before spatial mesh
  private Queue<WorkUnit> m_work_queue = null;  // TODO: add a way to queue up object removal
  private bool m_working = false;
  private int m_unique_id = 0;

  private static string AsString<T>(IList<T> list)
  {
    string output = "";
    foreach (T item in list)
    {
      output += item.ToString();
      output += " ";
    }
    return output;
  }

  private void ReassignSpatialMeshRenderOrder()
  {
    foreach (MeshFilter mesh_filter in m_spatial_mesh_filters)
    {
      foreach (Material material in mesh_filter.gameObject.GetComponent<Renderer>().materials)
      {
        if (0 == m_old_spatial_mesh_render_queue_value)
        {
          m_old_spatial_mesh_render_queue_value = material.renderQueue;
        }
        material.renderQueue = spatialMeshRenderQueueValue;
      }
    }
    Debug.Log("Old spatial mesh render queue=" + m_old_spatial_mesh_render_queue_value);
  }

  /*
   * Geometry is drawn with a render queue value of 2000. Surface meshes
   * produced by HoloToolkit are at a lower value (1999 at the time of this
   * writing), meaning they are drawn first. This means they will occlude any
   * objects positioned so as to appear embedded in the surface.
   * 
   * A solution that does not require altering the surface meshes is to draw
   * embedded objects *before* the surface meshes. And, within those objects,
   * the occluding material must be drawn first. HoloToolkit's 
   * WindowOcclusion shader draws with a render queue of 1999 (i.e., higher
   * priority than geometry). 
   * 
   * We assume here that an embeddable object is comprised of meshes that
   * render as ordinary geometry or as occlusion surfaces with a small render
   * priority offset. We re-assign render queue priorities beginning at 1000,
   * incrementing by one for each unique render queue priority found among
   * the meshes. This is done in sorted order, so that the lowest render
   * queue value (which will be the occlusion material) is mapped to 1000.
   */
  private void MakeHighPriorityRenderOrder(WorkUnit work)
  {
    List<Material> materials = work.originalMaterials.OrderBy(element => element.renderQueue).ToList();
    int new_priority = highPriorityRenderQueueValue - 1; // -1 because the loop will preincrement this
    int last_priority = -1;
    foreach (Material material in materials)
    {
      new_priority += (material.renderQueue != last_priority ? 1 : 0);
      last_priority = material.renderQueue;
      material.renderQueue = new_priority;
    }
  }

  /*
   * Compute a back-plane located directly behind the embedded object. 
   * Triangles that intersect with the embedded object's OBB will be
   * projected onto this back plane. The projection formula is:
   *
   *  v' = v - n * (Dot(n, v) + plane_to_origin_distance)
   *
   * Where n is the normal pointing toward the spatial mesh and
   * plane_to_origin_distance is solved for using the plane equation.
   *
   * We assume the OBB's position is located at the front-facing side of the
   * embedded object rather than in its center.
   */
  private void ComputeBackPlaneParameters(out Vector3 displacement_normal, out float plane_to_origin_distance, BoxCollider obb)
  {
    displacement_normal = -Vector3.Normalize(obb.transform.forward);
    Vector3 back_plane_point = obb.transform.position + displacement_normal * (obb.size.z * obb.transform.lossyScale.z + extraDisplacement);
    plane_to_origin_distance = 0 - Vector3.Dot(displacement_normal, back_plane_point); // Ax+By+Cz+D=0, solving for D here
  }

  private List<int> MakeSetOfVertexIndices(List<int> triangles)
  {
    List<int> vertex_set = new List<int>(triangles.Count);
    for (int i = 0; i < triangles.Count; i += 3)
    {
      int i0 = triangles[i + 0];
      int i1 = triangles[i + 1];
      int i2 = triangles[i + 2];
      if (!vertex_set.Contains(i0))
        vertex_set.Add(i0);
      if (!vertex_set.Contains(i1))
        vertex_set.Add(i1);
      if (!vertex_set.Contains(i2))
        vertex_set.Add(i2);
    }
    return vertex_set;
  }

  private T[] GetElements<T>(T[] original_elements, List<int> vert_indices)
  {
    T[] elements = new T[vert_indices.Count];
    for (int i = 0; i < vert_indices.Count; i++)
      elements[i] = original_elements[vert_indices[i]];
    return elements;
  }

  private void CreatePatchObject(Mesh mesh, MeshFilter mesh_filter, List<int> intersecting_triangles, Vector3[] verts, Vector3[] normals, List<int> vert_indices)
  {
    // Step 4
    Vector3[] patch_verts = GetElements(verts, vert_indices); // note: patch_verts[i] == mesh.vertices[vert_indices[i]]
    Vector3[] patch_normals = GetElements(normals, vert_indices);
    int[] patch_triangles = new int[intersecting_triangles.Count];
    for (int i = 0; i < patch_triangles.Length; i++)
    {
      int src_vert_idx = intersecting_triangles[i];
      int dest_vert_idx = vert_indices.IndexOf(src_vert_idx);
      patch_triangles[i] = dest_vert_idx;
    }

    /*
    Debug.Log("Intersecting Triangles = " + AsString(intersecting_triangles));
    Debug.Log("Unique Vertices        = " + AsString(vert_indices));
    Debug.Log("Saved Triangles        = " + AsString(patch_triangles));
    */

    // Step 5
    // TODO: write me

    // Step 6
    if (createPatches)
    {
      GameObject patch = new GameObject("patchMesh-" + mesh.name + "-" + (++m_unique_id).ToString());
      patch.layer = Layers.Instance.spatial_mesh_layer;
      patch.transform.parent = this.transform;                        // make child of SpatialMeshDeformationManager
      patch.transform.rotation = mesh_filter.transform.rotation;
      patch.transform.position = mesh_filter.transform.position;
      patch.transform.localScale = mesh_filter.transform.localScale;  // must be (1, 1, 1)
      Mesh new_mesh = patch.AddComponent<MeshFilter>().sharedMesh;
      new_mesh.vertices = patch_verts;
      new_mesh.triangles = patch_triangles;
      new_mesh.normals = patch_normals;
      MeshRenderer patch_renderer = patch.AddComponent<MeshRenderer>();
      patch_renderer.receiveShadows = false;
      patch_renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      patch_renderer.material = mesh_filter.gameObject.GetComponent<MeshRenderer>().material;
      if (renderPatches)
      {
        // Don't even bother creating the collider if we're not rendering
        MeshCollider patch_collider = patch.AddComponent<MeshCollider>();
        if (patch_collider != null)
        {
          patch_collider.sharedMesh = null;
          patch_collider.sharedMesh = new_mesh;
        }
      }
      patch_renderer.material.renderQueue = patchMeshRenderQueueValue;
      patch.SetActive(renderPatches == true);
    }
  }

  //private IEnumerator WorkRoutine()
  private void WorkRoutine()
  {
    while (m_work_queue.Count > 0)
    {
      // Dequeue and begin processing
      WorkUnit work = m_work_queue.Dequeue();
      BoxCollider obb = work.renderer.gameObject.GetComponent<BoxCollider>();
      if (obb == null)
      {
        Debug.Log("ERROR: Object " + work.renderer.gameObject.name + " lacks a box collider");
        return;
      }

      // Compute parameters for a projection plane behind embedded object 
      Vector3 displacement_normal;
      float plane_to_origin_distance;
      ComputeBackPlaneParameters(out displacement_normal, out plane_to_origin_distance, obb);

      // Check against each spatial mesh and deform those that intersect
      int num = 0;
      foreach (MeshFilter mesh_filter in m_spatial_mesh_filters)
      {
        Debug.Log("Iterating... " + (num++));
        if (mesh_filter == null)
          continue;

        //TODO: prefetch vertices/normals/uvs and indices because OBBMeshIntersection gets them, too, which is slow
        /*
         * Step 3: Detect all triangles that intersect with our object
         */
        Mesh mesh = mesh_filter.sharedMesh;
        if (mesh == null)
          continue;
        List<int> intersecting_triangles = OBBMeshIntersection.FindTriangles(obb, mesh, mesh_filter.transform);
        if (intersecting_triangles.Count == 0)
          continue;
        Vector3[] verts = mesh.vertices;  // spatial mesh has vertices and normals, no UVs
        Vector3[] normals = mesh.normals;
        List<int> vert_indices = MakeSetOfVertexIndices(intersecting_triangles);

        /*
         * Step 4: Create a new set of vertices and triangles out of the affected
         * ones to save their state before we alter them in the original mesh.
         * This "patch" mesh will be drawn after spatial meshes and embedded
         * objects.
         *
         * Step 5: Save the game object along with a mapping of the modified
         * vertices so we can undo changes later.
         *
         * Step 6: Create a new GameObject to hold a mesh with the saved 
         * vertices. Note that the vertices were in spatial mesh local
         * coordinates, so we copy the spatial mesh transform to position them
         * correctly in world space.
         *
         * Step 7: Reassign render order of saved triangles to a lower priority
         * than embedded object, so that they are rendered after
         */
        CreatePatchObject(mesh, mesh_filter, intersecting_triangles, verts, normals, vert_indices);

        /*
         * Step 8: Modify the spatial mesh's vertices by pushing them inward
         * along axis of normal
         */
        foreach (int i in vert_indices)
        {
          Vector3 world_vert = mesh_filter.transform.TransformPoint(verts[i]);
          float displacement = Vector3.Dot(displacement_normal, world_vert) + plane_to_origin_distance;
          world_vert = world_vert - displacement * displacement_normal;
          verts[i] = mesh_filter.transform.InverseTransformPoint(world_vert);
        }
        mesh.vertices = verts;  // apply changes
        mesh.RecalculateBounds();
        MeshCollider collider = mesh_filter.gameObject.GetComponent<MeshCollider>();
        if (collider != null)
        {
          collider.sharedMesh = null;
          collider.sharedMesh = mesh;
        }
      }

      /*
       * Step 9: Reassign render order of embedded object to be just after the
       * spatial mesh layer but before patches
       */
      foreach (Material material in work.renderer.materials)
      {
        material.renderQueue = material.renderQueue + (spatialMeshRenderQueueValue - highPriorityRenderQueueValue) + 1;
      }
    }
  }

  public void Embed(GameObject obj)
  {
    // Step 1: Modify render priority to draw before spatial mesh
    WorkUnit work = new WorkUnit(obj);
    MakeHighPriorityRenderOrder(work);

    // Step 2: Push to work queue for proper solution (spatial mesh adjustment)
    m_work_queue.Enqueue(work);
    //if (!m_working)
    {
      m_working = true;
      //StartCoroutine(WorkRoutine())
      WorkRoutine();  // TODO: make us a singleton and use a coroutine
    }
  }

  public void SetSpatialMeshFilters(List<MeshFilter> spatial_mesh_filters)
  {
    m_spatial_mesh_filters = spatial_mesh_filters;
    ReassignSpatialMeshRenderOrder();
  }

  private void Awake()
  {
    //m_freshly_inserted_by_id = new Dictionary<int, SavedRenderOrder>();
    m_work_queue = new Queue<WorkUnit>();
  }
}
