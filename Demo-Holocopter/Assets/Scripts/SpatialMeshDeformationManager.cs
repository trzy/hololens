/*
 * Notes:
 * ------
 * - Spatial meshes are assumed to be in world coordinates (i.e., no transform
 *   applied to their parent objects). 
 * - The SpatialMeshDeformationManager also must have no transform applied.
 * 
 * TODO:
 * -----
 * - Assert that no transform is applied to SpatialMeshDeformationManager and
 *   that it is positioned at (0, 0, 0).
 */
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class SpatialMeshDeformationManager //: HoloToolkit.Unity.Singleton<SpatialMeshDeformationManager>
{
  [Tooltip("Extra displacement (in meters) to push spatial mesh polygons beyond the back-plane of an object.")]
  public float extraDisplacement = 0.02f;

  [Tooltip("Render queue value to use for spatial meshes (existing value will be overwritten)")]
  public int spatialMeshRenderQueueValue = 1500;

  [Tooltip("Render queue value for spatial mesh patches (higher value than base spatial mesh and embedded objects)")]
  public int patchMeshRenderQueueValue = 1999;
  
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

  private int highPriorityRenderQueueValue = 1000;  //TODO: make public
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

  private void MakeHighPriorityRenderOrder(WorkUnit work)
  {
    /*
    * Geometry is drawn with a render queue queue of 2000. Surface meshes
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

  private Vector3[] GetVertices(Vector3[] all_verts, List<int> vert_indices)
  {
    Vector3[] verts = new Vector3[vert_indices.Count];
    for (int i = 0; i < vert_indices.Count; i++)
      verts[i] = all_verts[vert_indices[i]];
    return verts;
  }

  private T[] GetElements<T>(T[] original_elements, List<int> vert_indices)
  {
    T[] elements = new T[vert_indices.Count];
    for (int i = 0; i < vert_indices.Count; i++)
      elements[i] = original_elements[vert_indices[i]];
    return elements;
  }

  private void AdjustSpatialMesh(WorkUnit work)
  {
    /*
     * Compute vector to "push" surface polygons in by (the negative of the
     * object's forward direction and its box collider length along the same
     * axis in world units)
     */
    BoxCollider obb = work.renderer.gameObject.GetComponent<BoxCollider>();
    if (obb == null)
    {
      Debug.Log("ERROR: Object " + work.renderer.gameObject.name + " lacks a box collider");
      return;
    }

    /*
     * Compute a back-plane located directly behind the embedded object. 
     * Triangles that intersect with the embedded object's OBB will be
     * projected onto this back plane. The projection formula is:
     *
     *  v' = v - (Dot(n, v) + plane_to_origin_distance)
     *
     * Where n is the normal pointing toward the spatial mesh and
     * plane_to_origin_distance is solved for using the plane equation.
     *
     * We assume the OBB's position is located at the front-facing side of the
     * embedded object rather than in its center.
     */
    Vector3 displacement_normal = -Vector3.Normalize(obb.transform.forward);
    Vector3 back_plane_point = obb.transform.position + displacement_normal * (obb.size.z * obb.transform.lossyScale.z + extraDisplacement);
    float plane_to_origin_distance = 0 - Vector3.Dot(displacement_normal, back_plane_point); // Ax+By+Cz+D=0, solving for D here

    //Vector3 displacement = -Vector3.Normalize(obb.transform.forward) * obb.size.z * obb.transform.lossyScale.z;
    foreach (MeshFilter mesh_filter in m_spatial_mesh_filters)
    {
      //TODO: prefetch vertices/normals/uvs and indices because OBBMeshIntersection gets them, too, which is slow

      // Step 3: Detect all triangles that intersection with our object
      Mesh mesh = mesh_filter.mesh;
      List<int> intersecting_triangles = OBBMeshIntersection.FindTriangles(obb, mesh, mesh_filter.transform);
      if (intersecting_triangles.Count == 0)
        continue;

      /*
       * Step 4: Create a new set of vertices and triangles out of the affected
       * ones to save their state before we alter them in the original mesh.
       * This "patch" mesh will be drawn after spatial meshes and embedded
       * objects.
       */
      Vector3[] verts = mesh.vertices;  // spatial mesh has vertices and normals, no UVs
      Vector3[] normals = mesh.normals;
      List<int> vert_indices = MakeSetOfVertexIndices(intersecting_triangles);
      Vector3[] patch_verts = GetElements(verts, vert_indices); // note: patch_verts[i] == mesh.vertices[vert_indices[i]]
      Vector3[] patch_normals = GetElements(normals, vert_indices);
      int[] patch_triangles = new int[intersecting_triangles.Count];
      for (int i = 0; i < patch_triangles.Length; i++)
      {
        int src_vert_idx = intersecting_triangles[i];
        int dest_vert_idx = vert_indices.IndexOf(src_vert_idx);
        patch_triangles[i] = dest_vert_idx;
      }
      Debug.Log("Intersecting Triangles = " + AsString(intersecting_triangles));
      Debug.Log("Unique Vertices        = " + AsString(vert_indices));
      Debug.Log("Saved Triangles        = " + AsString(patch_triangles));

      /*     
       * Step 5: Push the saved vertices on a stack so that when all
       * embedded objects are ultimately removed, we can unwind changes
       */
      // TODO: write me

      /*
       * Step 6: Create a new GameObject to hold a mesh with the saved vertices
       */
      // TODO: set parent to SpatialMeshDeformationManager
      GameObject patch = new GameObject("patchMesh-" + mesh.name + "-" + (++m_unique_id).ToString());
      patch.layer = Layers.Instance.spatial_mesh_layer;
      Mesh new_mesh = patch.AddComponent<MeshFilter>().mesh;
      new_mesh.vertices = patch_verts;
      new_mesh.triangles = patch_triangles;
      new_mesh.normals = patch_normals;
      MeshRenderer patch_renderer = patch.AddComponent<MeshRenderer>();
      patch_renderer.receiveShadows = false;
      patch_renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      patch_renderer.material = mesh_filter.gameObject.GetComponent<MeshRenderer>().material;
      patch.SetActive(false);

      /*
       * Step 7: Reassign render order of saved triangles to a higher priority
       * than embedded object
       */
      patch_renderer.material.renderQueue = patchMeshRenderQueueValue;

      /*
       * Step 8: Modify the spatial mesh's vertices by pushing them inward
       * along axis of normal
       */
      foreach (int i in vert_indices)
      {
        float displacement = Vector3.Dot(displacement_normal, verts[i]) + plane_to_origin_distance;
        Debug.Log("displacement=" + displacement);
        verts[i] -= displacement * displacement_normal;
      }
      mesh.vertices = verts;  // apply changes
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

  //private IEnumerator WorkRoutine()
  private void WorkRoutine()
  {
    while (m_work_queue.Count > 0)
    {
      WorkUnit work = m_work_queue.Dequeue();
      AdjustSpatialMesh(work);
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

  public SpatialMeshDeformationManager(int hi_pri_render_queue_value, List<MeshFilter> spatial_mesh_filters)
  {
    highPriorityRenderQueueValue = hi_pri_render_queue_value;
    m_spatial_mesh_filters = spatial_mesh_filters;
    //m_freshly_inserted_by_id = new Dictionary<int, SavedRenderOrder>();
    m_work_queue = new Queue<WorkUnit>();
    ReassignSpatialMeshRenderOrder();
  }
}
