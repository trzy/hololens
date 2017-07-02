//TODO: Pre-compute AABB and pass it to OBB intersection test if obb collider is disabled.
/*
 * Singleton component for embedding small, non-collidable objects into 
 * SurfacePlane regions.
 * 
 * Requirements for embeddable objects
 * -----------------------------------
 * - Pivot point of the object is assumed to be flush with the surface in which
 *   it is being embedded. That is, when placing the object, its transform 
 *   position should be set to a point on the surface.
 * - There are no requirements on the orientation of the object itself. Rather,
 *   an OrientedBoundingBox must be supplied in world space units with the z
 *   (forward) axis oriented normal to the surface, even if the object's z axis
 *   is *not* oriented this way. The z axis length defines the thickness of the
 *   object, which is also the required spatial mesh margin (discussed further
 *   below) that will be created.
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
 *  3. Encode the margin volume as points on the front and back sides (along
 *     the axis pointing into the surface). The difference between these two
 *     points is the object "thickness"  plus an optional extra displacement.
 *     The width and height of the deformation volume are equal to the surface
 *     plane into which the object is being embedded. This will cause the
 *     entire surface plane volume to be adjusted at once allowing subsequent
 *     objects to be embedded readily.
 *  4. For each spatial mesh, check whether it has already been deformed and
 *     whether the margin is sufficient to accomodate placing the new embedded
 *     object. If so, no need to process this mesh.
 *  5. Otherwise, find the triangles that intersect with the margin volume's
 *     OBB.
 *  6. Project each of the intersecting triangles' vertices onto the back plane
 *     of the margin volume ("push" them back) and then update the spatial mesh
 *     vertices without updating the collider mesh.
 *  7. Record the margin for this spatial mesh. Note that both the spatial mesh
 *     *and* the SurfacePlane are required because a single spatial mesh may 
 *     contain multiple SurfacePlanes. The margin created through deformation
 *     only exists in the vicinity of a given plane.
 *  8. Destroy the temporary margin volume game object.
 *  9. Restore the shared materials of the embedded object (which will also 
 *     restore its original render queue values).
 *     
 * Note that steps 4 through 9 are performed in a coroutine over multiple
 * frames.
 * 
 * Arbitrary spatial mesh
 * ----------------------
 * An alternative form of Embed() is provided that does not require a 
 * SurfacePlane. This version works virtually identically except that it
 * deforms only the portion of the spatial meshes that is intersected by
 * the embedded object. Consequently, the meshes have to be searched and
 * deformed each time.
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity.SpatialMapping;

public class SurfacePlaneDeformationManager: HoloToolkit.Unity.Singleton<SurfacePlaneDeformationManager>
{
  [Tooltip("Extra displacement (in meters) to push spatial mesh polygons beyond the back-plane of an object.")]
  public float extraDisplacement = 0.02f;

  [Tooltip("Render queue value to use for drawing freshly-embeded objects before spatial mesh.")]
  public int highPriorityRenderQueueValue = 1000;

  [Tooltip("Maximum number of seconds per frame to spend processing.")]
  public float maxSecondsPerFrame = 4e-3f;

  [Tooltip("Draw margin volumes for debugging purposes")]
  public bool debugMarginVolumes = false;

  [Tooltip("Debug material to use to render margin volumes if debugging is enabled")]
  public Material debugMaterial = null;

  private class EmbedRequest
  {
    public OrientedBoundingBox obb;
    public Vector3 centerPointOnFrontPlane;
    public Vector3 centerPointOnBackPlane;
    public GameObject embedded;

    public virtual bool HasSufficientMargin(MeshFilter meshFilter, float requiredMargin)
    {
      // Without SurfacePlane, we cannot pre-deform large areas of the spatial
      // mesh. Therefore, each insertion will deform a new part of the mesh.
      return false;
    }

    public virtual void RecordMargin(MeshFilter meshFilter, float margin)
    {
    }

    public EmbedRequest(OrientedBoundingBox pObb, Vector3 pCenterPointOnFrontPlane, Vector3 pCenterPointOnBackPlane, GameObject pEmbedded)
    {
      obb = pObb;
      centerPointOnFrontPlane = pCenterPointOnFrontPlane;
      centerPointOnBackPlane = pCenterPointOnBackPlane;
      embedded = pEmbedded;
    }
  }

  private class SurfacePlaneEmbedRequest: EmbedRequest
  {
    public SurfacePlane plane;
    private Dictionary<Tuple<SurfacePlane, MeshFilter>, float> m_marginByPlaneAndSpatialMesh;

    public override bool HasSufficientMargin(MeshFilter meshFilter, float requiredMargin)
    {
      Tuple<SurfacePlane, MeshFilter> key = new Tuple<SurfacePlane, MeshFilter>(plane, meshFilter);
      //Debug.Log("hash key " + plane.GetInstanceID() + "," + meshFilter.GetInstanceID() + " = " + key.GetHashCode());
      float currentMargin;
      if (m_marginByPlaneAndSpatialMesh.TryGetValue(key, out currentMargin))
      {
        return currentMargin >= requiredMargin;
      }
      return false;
    }

    public override void RecordMargin(MeshFilter meshFilter, float margin)
    {
      m_marginByPlaneAndSpatialMesh[new Tuple<SurfacePlane, MeshFilter>(plane, meshFilter)] = margin;
    }

    public SurfacePlaneEmbedRequest(
      SurfacePlane pPlane,
      Dictionary<Tuple<SurfacePlane, MeshFilter>, float> marginByPlaneAndSpatialMesh,
      OrientedBoundingBox pObb,
      Vector3 pCenterPointOnFrontPlane,
      Vector3 pCenterPointOnBackPlane,
      GameObject pEmbedded)
      : base(pObb, pCenterPointOnFrontPlane, pCenterPointOnBackPlane, pEmbedded)
    {
      plane = pPlane;
      m_marginByPlaneAndSpatialMesh = marginByPlaneAndSpatialMesh;
    }
  }

  private List<MeshFilter> m_spatialMeshFilters = null;
  private Dictionary<Tuple<SurfacePlane, MeshFilter>, float> m_marginByPlaneAndSpatialMesh = null;
  private Queue<EmbedRequest> m_requestQueue = new Queue<EmbedRequest>();
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

  private bool HasSufficientMargin(SurfacePlane plane, MeshFilter meshFilter, float requiredMargin)
  {
    Tuple<SurfacePlane, MeshFilter> key = new Tuple<SurfacePlane, MeshFilter>(plane, meshFilter);
    //Debug.Log("hash key " + plane.GetInstanceID() + "," + meshFilter.GetInstanceID() + " = " + key.GetHashCode());
    float currentMargin;
    if (m_marginByPlaneAndSpatialMesh.TryGetValue(key, out currentMargin))
    {
      return currentMargin >= requiredMargin;
    }
    return false;
  }

  private IEnumerator DeformSurfaceCoroutine()
  {
    while (m_requestQueue.Count > 0)
    {
      // Dequeue
      EmbedRequest request = m_requestQueue.Dequeue();
      OrientedBoundingBox obb = request.obb;
      Vector3 marginFrontPlanePoint = request.centerPointOnFrontPlane;
      Vector3 marginBackPlanePoint = request.centerPointOnBackPlane;
      GameObject embedded = request.embedded;

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

        if (request.HasSufficientMargin(meshFilter, requiredMargin))
        {
          //Debug.Log("Skipping [" + task.plane.GetInstanceID() + "," + meshFilter.GetInstanceID() + "] due to sufficient margin");
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
        List<int> intersectingTriangles = null;
        IEnumerator f = OBBMeshIntersection.FindTrianglesCoroutine((List<int> result) => { intersectingTriangles = result; }, maxSecondsPerFrame, obb, verts, mesh.GetTriangles(0), meshFilter.transform);
        while (f.MoveNext())
        {
          yield return f.Current;
        }
        if (intersectingTriangles.Count == 0)
        {
          //Debug.Log("No intersecting triangles found in: " + meshFilter.gameObject.name);
          continue;
        }

        // TODO: Use a bit set to keep track of which vertex indices to modify?

        float t0 = Time.realtimeSinceStartup;
        List<int> vertIndices = intersectingTriangles;

        // Modify spatial mesh by pushing vertices inward onto the back plane of
        // the margin volume
        foreach (int i in vertIndices)
        {
          Vector3 worldVert = meshFilter.transform.TransformPoint(verts[i]);
          float displacement = Vector3.Dot(intoSurfaceNormal, worldVert) + planeToOriginDistance;
          worldVert = worldVert - displacement * intoSurfaceNormal;
          verts[i] = meshFilter.transform.InverseTransformPoint(worldVert);
          if (Time.realtimeSinceStartup - t0 >= maxSecondsPerFrame)
          {
            yield return null;
            t0 = Time.realtimeSinceStartup;
          }
        }
        mesh.vertices = verts;      // apply changes
        mesh.RecalculateBounds();   // not needed because deformation should be minor
        if (Time.realtimeSinceStartup - t0 >= maxSecondsPerFrame)
        {
          yield return null;
          t0 = Time.realtimeSinceStartup;
        }

        // Update margin depth of this plane/spatial mesh pair
        request.RecordMargin(meshFilter, requiredMargin);
        //Debug.Log("Finished deforming [" + task.plane.GetInstanceID() + "," + meshFilter.GetInstanceID() + "]");
      }

      // Restore embedded object's shared materials (and its render order) now
      // that margin exists in the spatial mesh. Clean up temporary objects.
      // Destroy temporary object
      embedded.GetComponent<SharedMaterialHelper>().RestoreSharedMaterials();
    }

    m_working = false;
    yield break;
  }

  private void ComputeDepthBounds(out Vector3 centerPointOnFrontPlane, out Vector3 centerPointOnBackPlane, OrientedBoundingBox obb, Vector3 position, SurfacePlane plane)
  {
    OrientedBoundingBox surfaceBounds = plane.Plane.Bounds;

    /*
     * Compute margin volume front (i.e., the actual surface) and back (onto
     * which spatial mesh triangles will be projected) planes based on embedded
     * object thickness.
     *
     * Note that the embedded object position is not in the center of the
     * object but rather at a point flush with the surface it will be embedded
     * in.
     */
    Vector3 forward = obb.Rotation * Vector3.forward;
    Vector3 size = 2 * obb.Extents;
    Vector3 intoSurfaceNormal = -Vector3.Normalize(forward);
    float embeddedThickness = size.z + extraDisplacement;
    Vector3 embeddedPosOnPlane = plane.transform.InverseTransformPoint(position);
    embeddedPosOnPlane.x = 0;
    embeddedPosOnPlane.y = 0;
    centerPointOnFrontPlane = plane.transform.TransformPoint(embeddedPosOnPlane);
    centerPointOnBackPlane = centerPointOnFrontPlane + intoSurfaceNormal * embeddedThickness;
  }

  private void ComputeDepthBounds(out Vector3 centerPointOnFrontPlane, out Vector3 centerPointOnBackPlane, OrientedBoundingBox obb, Vector3 position)
  {
    /*
     * Compute margin volume front (i.e., the actual surface) and back (onto
     * which spatial mesh triangles will be projected) planes based on embedded
     * object thickness.
     *
     * Note that the embedded object position is not in the center of the
     * object but rather at a point flush with the surface it will be embedded
     * in.
     */
    Vector3 forward = obb.Rotation * Vector3.forward;
    Vector3 size = 2 * obb.Extents;
    Vector3 intoSurfaceNormal = -Vector3.Normalize(forward);
    float embeddedThickness = size.z + extraDisplacement;
    centerPointOnFrontPlane = position;
    centerPointOnBackPlane = centerPointOnFrontPlane + intoSurfaceNormal * embeddedThickness;    
  }

  private void DebugVisualization(OrientedBoundingBox obb)
  {
    if (debugMarginVolumes)
    {
      GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
      cube.transform.parent = null;
      cube.transform.localScale = 2 * obb.Extents;
      cube.transform.position = obb.Center;
      cube.transform.transform.rotation = obb.Rotation;
      cube.GetComponent<Renderer>().material = debugMaterial;
      cube.GetComponent<Renderer>().material.color = Color.green;
      cube.SetActive(true);
    }
  }

  public void Embed(GameObject embedded, OrientedBoundingBox obb, SurfacePlane plane)
  {
    if (null == plane)
    {
      return;
    }

    // Temporarily make the embedded object render in front of spatial mesh
    MakeHighPriorityRenderOrder(embedded);

    // Compute the front (pivot point of embedded object flush with surface)
    // and back planes of the object to be embedded
    Vector3 centerPointOnFrontPlane;
    Vector3 centerPointOnBackPlane;
    ComputeDepthBounds(out centerPointOnFrontPlane, out centerPointOnBackPlane, obb, embedded.transform.position, plane);

    // Deform the spatial mesh to create a margin volume large enough for the
    // embedded object
    m_requestQueue.Enqueue(new SurfacePlaneEmbedRequest(plane, m_marginByPlaneAndSpatialMesh, obb, centerPointOnFrontPlane, centerPointOnBackPlane, embedded));
    if (!m_working)
    {
      m_working = true;
      StartCoroutine(DeformSurfaceCoroutine());
    }

    DebugVisualization(obb);
  }

  public void Embed(GameObject embedded, OrientedBoundingBox obb, Vector3 position)
  {
    // Temporarily make the embedded object render in front of spatial mesh
    MakeHighPriorityRenderOrder(embedded);

    // Compute the front (pivot point of embedded object flush with surface)
    // and back planes of the object to be embedded
    Vector3 centerPointOnFrontPlane;
    Vector3 centerPointOnBackPlane;
    ComputeDepthBounds(out centerPointOnFrontPlane, out centerPointOnBackPlane, obb, position);
    
    // Deform the spatial mesh to create a margin volume large enough for the
    // embedded object
    m_requestQueue.Enqueue(new EmbedRequest(obb, centerPointOnFrontPlane, centerPointOnBackPlane, embedded));
    if (!m_working)
    {
      m_working = true;
      StartCoroutine(DeformSurfaceCoroutine());
    }

    DebugVisualization(obb);
  }

  public void SetSpatialMeshFilters(List<MeshFilter> spatialMeshFilters)
  {
    m_spatialMeshFilters = spatialMeshFilters;
    m_marginByPlaneAndSpatialMesh = new Dictionary<Tuple<SurfacePlane, MeshFilter>, float>(spatialMeshFilters.Count);
  }

  private new void Awake()
  {
    base.Awake();
    if (transform.position != Vector3.zero || transform.lossyScale != Vector3.one || transform.rotation != Quaternion.identity)
    {
      Debug.Log("ERROR: SurfacePlaneDeformationManager transform is not correct!");
    }
  }
}
