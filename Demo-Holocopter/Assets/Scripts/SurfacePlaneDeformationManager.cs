/*
 * Singleton component for embedding small, non-collidable objects into 
 * SurfacePlane regions.
 * 
 * Requirements for embeddable objects
 * -----------------------------------
 * - Pivot point of the object is assumed to be flush with the surface in which
 *   it is being embedded. That is, when placing the object, its transform 
 *   position should be set to a point on the surface.
 * - Z (forward) axis must be oriented normal to the surface. It is assumed
 *   that this will have been determined by a collision with a surface plane
 *   and therefore will be consistent for all points on the surface plane.
 * - A BoxCollider must be present. Its z-axis length defines the thickness of
 *   the object, which is also the required spatial mesh margin (discussed
 *   further below) that will be created.
 * - No instanced materials. Objects should use only the shared materials they
 *   are instantiated with in the first place.
 * - SharedMaterialHelper must be attached to the object.
 * 
 * The problem with embedding small objects
 * ----------------------------------------
 * When used for occlusion, the spatial mesh is rendered to the depth buffer
 * and has a *higher* render priority (i.e., lower render queue value) than
 * ordinary geometry. As of this writing, the HoloToolkit sets the spatial mesh
 * render queue value to 1999, where the normal geometry render queue value is
 * 2000.
 * 
 * Therefore, embedding a small object in the wall is impossible because it
 * will be occluded by the spatial mesh. Creating large holes by modifying the
 * spatial mesh (as in Microsoft's examples) is a simpler problem: triangles
 * are simply removed from the mesh within some radius or volume. The natural
 * "noisiness" of the surface mesh triangles creates the desired jagged
 * boundary. Small objects, like bullet holes, on the other hand, may be
 * smaller than a single surface mesh triangle. Attempting to create "small"
 * holes in the spatial mesh using complex computational solid geometry
 * technique is overkill.
 * 
 * One simple solution is to modify the render priority of the embedded object
 * so that it is drawn *before* the spatial mesh. Unfortunately, this means the
 * embedded objects will never be occluded at all, not even by walls and
 * objects that legitimately obscure the embedded object. For example,
 * consider a small bullet hole in a wall and then a (real) pillar in front of
 * the wall. The spatial mesh corresponding to the pillar *should* obscure the
 * hole but with this scheme, will not because the occlusion shader does not
 * render to the color buffer and the spatial mesh is rendered *after* the
 * hole.
 * 
 * How SurfacePlaneDeformationManager works
 * ----------------------------------------
 * A simple, effective way to embed small objects into surfaces is to displace
 * the surface mesh behind the embedded object by a margin equal to the
 * thickness of the object. The collider mesh is *not* updated, so collisions
 * with the surface are unaffected.
 * 
 * The procedure used here is:
 * 
 *  1. Make embedded object render priority *higher* than spatial meshes by
 *     assigning lower render queue values to its instanced materials.
 *  2. Compute object thickness. This is the required margin necessary to
 *     "push back" spatial mesh triangles by, so that none overlap with the
 *     embedded object.
 *  3. Create a "margin volume" game object. The margin volume has a thickness
 *     equal to the object thickness (plus an extra displacement if desired),
 *     and width and height equal to the surface plane. This will cause the 
 *     entire surface plane volume to be adjusted at once allowing subsequent
 *     objects to be embedded readily. A BoxCollider is attached to the margin
 *     volume object because the OBB/mesh intersection testing routine requires
 *     it.
 *  4. For each spatial mesh, check whether it has already been deformed and
 *     whether the margin is sufficient to accomodate placing the new embedded
 *     object. If so, no need to process this mesh.
 *  5. Otherwise, find the triangles that intersect with the margin volume's
 *     OBB.
 *  6. Project each of the intersecting triangles' vertices onto the back plane
 *     of the margin volume ("push" them back) and then update the spatial mesh
 *     vertices without updating the collider mesh.
 *  7. Record the margin for this spatial mesh.
 *  8. Destroy the temporary margin volume game object.
 *  9. Restore the shared materials of the embedded object (which will also 
 *     restore its original render queue values).
 *     
 * Note that steps 4 through 9 are performed in a coroutine over multiple
 * frames.
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SurfacePlaneDeformationManager: HoloToolkit.Unity.Singleton<SurfacePlaneDeformationManager>
{
  [Tooltip("Extra displacement (in meters) to push spatial mesh polygons beyond the back-plane of an object.")]
  public float extraDisplacement = 0.02f;

  [Tooltip("Render queue value to use for drawing freshly-embeded objects before spatial mesh.")]
  public int highPriorityRenderQueueValue = 1000;

  [Tooltip("Maximum number of seconds per frame to spend processing.")]
  public float maxSecondsPerFrame = 4e-3f;

  //public Material flatMaterial;

  private class Task
  {
    public GameObject marginVolume;
    public Vector3 centerPointOnFrontPlane;
    public Vector3 centerPointOnBackPlane;
    public GameObject embedded;

    public Task(GameObject pMarginVolume, Vector3 pCenterPointOnFrontPlane, Vector3 pCenterPointOnBackPlane, GameObject pEmbedded)
    {
      marginVolume = pMarginVolume;
      centerPointOnFrontPlane = pCenterPointOnFrontPlane;
      centerPointOnBackPlane = pCenterPointOnBackPlane;
      embedded = pEmbedded;
    }
  }

  private List<MeshFilter> m_spatialMeshFilters = null;
  private Dictionary<MeshFilter, float> m_marginBySpatialMesh = null;
  private Queue<Task> m_taskQueue = null;
  private bool m_working = false;

  private List<Material> GetAllMaterials(GameObject obj)
  {
    List<Material> materials = new List<Material>();
    foreach (Renderer renderer in obj.GetComponentsInChildren<Renderer>())
    {
      foreach (Material material in renderer.materials)
      {
        if (!materials.Contains(material))
        {
          materials.Add(material);
        }
      }
    }
    return materials;
  }

  private void MakeHighPriorityRenderOrder(GameObject embedded)
  {
    /*
     * Geometry is drawn with a render queue value of 2000. Surface meshes
     * produced by HoloToolkit are at a lower value (1999 at the time of this
     * writing), meaning they are drawn first. They will therefore occlude any
     * object positioned directly on the surface.
     * 
     * One solution that does not require altering the surface meshes is to draw
     * embedded objects *before* the surface meshes. And, within those objects,
     * the occluding material must be drawn first. HoloToolkit's 
     * WindowOcclusion shader draws with a render queue of 1999 (i.e., higher
     * priority than geometry). We must preserve this relative ordering.
     * 
     * We assume here that an embeddable object is comprised of meshes that
     * render as ordinary geometry or as occlusion surfaces with a small render
     * priority offset. We re-assign render queue priorities beginning at 1000
     * (or some other user-selectable value), incrementing by one for each 
     * unique render queue priority found among the meshes. This is done in
     * sorted order, so that the lowest render queue value (which will be the
     * occlusion material) is mapped to 1000.
     */
    List<Material> materials = GetAllMaterials(embedded);
    materials.Sort((x, y) => x.renderQueue.CompareTo(y.renderQueue)); // sort ascending
    int newPriority = highPriorityRenderQueueValue - 1; // -1 because the loop will preincrement
    int lastPriority = -1;
    foreach (Material material in materials)
    {
      newPriority += (material.renderQueue != lastPriority ? 1 : 0);
      lastPriority = material.renderQueue;
      material.renderQueue = newPriority;
    }
  }

  private bool HasSufficientMargin(MeshFilter meshFilter, float requiredMargin)
  {
    float currentMargin;
    if (m_marginBySpatialMesh.TryGetValue(meshFilter, out currentMargin))
    {
      return currentMargin >= requiredMargin;
    }
    return false;
  }

  private List<int> MakeSetOfVertexIndices(List<int> triangles)
  {
    List<int> vertexSet = new List<int>(triangles.Count);
    for (int i = 0; i < triangles.Count; i += 3)
    {
      int i0 = triangles[i + 0];
      int i1 = triangles[i + 1];
      int i2 = triangles[i + 2];
      if (!vertexSet.Contains(i0))
      {
        vertexSet.Add(i0);
      }
      if (!vertexSet.Contains(i1))
      {
        vertexSet.Add(i1);
      }
      if (!vertexSet.Contains(i2))
      { 
        vertexSet.Add(i2);
      }
    }
    return vertexSet;
  }

  private IEnumerator DeformSurfaceCoroutine()
  {
    while (m_taskQueue.Count > 0)
    {
      // Dequeue
      Task task = m_taskQueue.Dequeue();
      GameObject marginVolume = task.marginVolume;
      Vector3 marginFrontPlanePoint = task.centerPointOnFrontPlane;
      Vector3 marginBackPlanePoint = task.centerPointOnBackPlane;
      GameObject embedded = task.embedded;

      // Get the OBB that describes the margin volume we want to create
      BoxCollider obb = marginVolume.GetComponent<BoxCollider>();

      /*
       * Compute a back-plane located directly behind the embedded object. 
       * Triangles that intersect with the embedded object's OBB will be
       * projected onto this back plane. The projection formula is:
       *
       *  v' = v - n * (Dot(n, v) + planeToOriginDistance)
       *
       * Where n is the normal pointing toward the spatial mesh and
       * planeToOriginDistance is solved for using the plane equation.
       *
       * We assume the OBB's position is located at the front-facing side of the
       * embedded object rather than in its center.
       */
      Vector3 intoSurface = marginBackPlanePoint - marginFrontPlanePoint;
      Vector3 intoSurfaceNormal = Vector3.Normalize(intoSurface);
      float planeToOriginDistance = 0 - Vector3.Dot(intoSurfaceNormal, marginBackPlanePoint);  // Ax+By+Cz+D=0, solving for D here

      // The thickness of the embedded object can easily be obtained from the
      // desired distance between the margin volume's front and back planes
      float requiredMargin = Vector3.Magnitude(intoSurface);

      // Check against each spatial mesh and deform those that intersect the
      // margin volume
      foreach (MeshFilter meshFilter in m_spatialMeshFilters)
      {
        if (meshFilter == null)
        {
          continue;
        }

        if (HasSufficientMargin(meshFilter, requiredMargin))
        {
          //Debug.Log("Skipping " + meshFilter.name + " due to sufficient margin");
          continue;
        }

        // Detect all triangles that intersect with the margin volume OBB
        Mesh mesh = meshFilter.sharedMesh;
        if (mesh == null)
        {
          continue;
        }
        Vector3[] verts = mesh.vertices;  // spatial mesh has vertices and normals, no UVs
        Vector3[] normals = mesh.normals;
        List<int> intersectingTriangles = new List<int>();
        OBBMeshIntersection.ResultsCallback results = delegate (List<int> result) { intersectingTriangles = result; };
        IEnumerator f = OBBMeshIntersection.FindTrianglesCoroutine(results, maxSecondsPerFrame, obb, verts, mesh.GetTriangles(0), meshFilter.transform);
        while (f.MoveNext())
        {
          yield return f.Current;
        }
        if (intersectingTriangles.Count == 0)
        {
          continue;
        }
        float t0 = Time.realtimeSinceStartup;
        List<int> vertIndices = MakeSetOfVertexIndices(intersectingTriangles);

        // Modify spatial mesh by pushing vertices inward onto the back plane of
        // the margin volume
        foreach (int i in vertIndices)
        {
          Vector3 worldVert = meshFilter.transform.TransformPoint(verts[i]);
          float displacement = Vector3.Dot(intoSurfaceNormal, worldVert) + planeToOriginDistance;
          worldVert = worldVert - displacement * intoSurfaceNormal;
          verts[i] = meshFilter.transform.InverseTransformPoint(worldVert);
        }
        mesh.vertices = verts;  // apply changes
        mesh.RecalculateBounds();
        if (Time.realtimeSinceStartup - t0 >= maxSecondsPerFrame)
        {
          yield return null;
          t0 = Time.realtimeSinceStartup;
        }

        // Update margin depth of this spatial mesh
        m_marginBySpatialMesh[meshFilter] = requiredMargin;
      }

      // Restore embedded object's shared materials (and its render order) now
      // that margin exists in the spatial mesh. Clean up temporary objects.
      // Destroy temporary object
      embedded.GetComponent<SharedMaterialHelper>().RestoreSharedMaterials();
      Object.Destroy(marginVolume.GetComponent<BoxCollider>());
      Object.Destroy(marginVolume);
    }

    m_working = false;
    yield break;
  }

  private bool CreateMarginVolumeObject(out GameObject marginVolume, out Vector3 centerPointOnFrontPlane, out Vector3 centerPointOnBackPlane, GameObject embedded, HoloToolkit.Unity.SurfacePlane plane)
  {
    BoxCollider embeddedOBB = embedded.GetComponent<BoxCollider>();
    if (null == embeddedOBB)
    {
      Debug.Log("ERROR: Embedded object " + embedded.name + " lacks a box collider");
      marginVolume = null;
      centerPointOnFrontPlane = Vector3.zero;
      centerPointOnBackPlane = Vector3.zero;
      return true;
    }
    HoloToolkit.Unity.OrientedBoundingBox surfaceBounds = plane.Plane.Bounds;

    /*
     * Compute margin volume front (i.e., the actual surface) and back (onto
     * which spatial mesh triangles will be projected) planes based on embedded
     * object thickness.
     *
     * Note that the embedded object position is not in the center of the
     * object but rather at a point flush with the surface it will be embedded
     * in.
     */
    Vector3 intoSurfaceNormal = -Vector3.Normalize(embeddedOBB.transform.forward);
    float embeddedThickness = embeddedOBB.size.z * embeddedOBB.transform.lossyScale.z + extraDisplacement;
    Vector3 embeddedPosOnPlane = plane.transform.InverseTransformPoint(embedded.transform.position);
    embeddedPosOnPlane.x = 0;
    embeddedPosOnPlane.y = 0;
    centerPointOnFrontPlane = plane.transform.TransformPoint(embeddedPosOnPlane);
    centerPointOnBackPlane = centerPointOnFrontPlane + intoSurfaceNormal * embeddedThickness;
    Vector3 centerPoint = 0.5f * (centerPointOnFrontPlane + centerPointOnBackPlane);

    /*
     * Create a game object to describe the size, position, and orientation of
     * the margin volume we want to create.
     * 
     * The box collider is something of a formality. The surface mesh
     * deformation procedure and OBB/mesh intersection tests use box colliders
     * to describe OBBs. Here, the box collider is made the same size as the
     * game object to which it is parented. The downstream intersection testing
     * code knows to properly scale the box based on its parent.
     */
    marginVolume = new GameObject("deformation-" + embedded.name);
    marginVolume.transform.position = centerPoint;
    marginVolume.transform.localScale = new Vector3(2 * surfaceBounds.Extents.x, 2 * surfaceBounds.Extents.y, embeddedThickness);
    marginVolume.transform.rotation = surfaceBounds.Rotation;  // need to use surface plane rotation because x, y differ from obj's
    BoxCollider obb = marginVolume.AddComponent<BoxCollider>();
    obb.center = Vector3.zero;
    obb.size = Vector3.one;
    obb.enabled = false;  // we do not actually want collision detection

    return false;
  }

  public void Embed(GameObject embedded, HoloToolkit.Unity.SurfacePlane plane)
  {
    if (null == plane)
    {
      return;
    }

    // Temporarily make the embedded object render in front of spatial mesh
    MakeHighPriorityRenderOrder(embedded);

    // Create an object describing the margin volume required for embedded
    // object placement
    GameObject marginVolume;
    Vector3 centerPointOnFrontPlane;
    Vector3 centerPointOnBackPlane;
    if (CreateMarginVolumeObject(out marginVolume, out centerPointOnFrontPlane, out centerPointOnBackPlane, embedded, plane))
    {
      return;
    }

    // Deform the spatial mesh to create a margin volume large enough for the
    // embedded object
    m_taskQueue.Enqueue(new Task(marginVolume, centerPointOnFrontPlane, centerPointOnBackPlane, embedded));
    if (!m_working)
    {
      m_working = true;
      StartCoroutine(DeformSurfaceCoroutine());
    }

    /*
    Debug.Log("pos=" + marginVolume.transform.position + ", obbpos = " + marginVolume.GetComponent<BoxCollider>().transform.position);
    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
    cube.transform.parent = null;
    cube.transform.localScale = marginVolume.transform.localScale;
    cube.transform.position = marginVolume.transform.position;
    cube.transform.transform.rotation = marginVolume.transform.rotation;
    cube.GetComponent<Renderer>().material = flatMaterial;
    cube.GetComponent<Renderer>().material.color = Color.green;
    cube.SetActive(true);
    */
  }

  public void SetSpatialMeshFilters(List<MeshFilter> spatialMeshFilters)
  {
    m_spatialMeshFilters = spatialMeshFilters;
    m_marginBySpatialMesh = new Dictionary<MeshFilter, float>(spatialMeshFilters.Count);
  }

  private void Awake()
  {
    m_taskQueue = new Queue<Task>();
    if (transform.position != Vector3.zero || transform.lossyScale != Vector3.one || transform.rotation != Quaternion.identity)
    {
      Debug.Log("ERROR: SurfacePlaneDeformationManager transform is not correct!");
    }
  }
}
