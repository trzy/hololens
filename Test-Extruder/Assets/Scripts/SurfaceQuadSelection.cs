using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity.SpatialMapping;

public class SurfaceQuadSelection //TODO: rename to PlanarQuadSelection? or PlanarTileSelection?
{
  private const int QUAD_WIDTH_CM = 10; // width of each quad in centimeters
  private int m_maxQuads;

  // Selection plane definition in world units
  private Vector3 m_xAxis = Vector3.zero;
  private Vector3 m_yAxis = Vector3.zero;
  private Vector3 m_normal = Vector3.zero;  // local z axis
  private Vector3 m_origin = Vector3.zero;  // along with the normal defines the current selection plane
  private Quaternion m_toWorld;
  private Quaternion m_toLocal;
  private Plane m_plane;                    // current selection plane defined from the above

  // Tiles in local, centimeter units
  private struct Tile
  {
    public enum Edge
    {
      Top = 0x1,
      Right = 0x2,
      Bottom = 0x4,
      Left = 0x8
    }
    public IVector3 center;
    public byte neighbors;

    public Tile(IVector3 centerPosition)
    {
      center = centerPosition;
      neighbors = 0;
    }
  }

  private List<Tile> m_tiles;

  // Selection pattern
  private IVector3[] m_pattern;

  public Quaternion rotation
  {
    get { return m_toWorld; }
  }

  public Vector3 position
  {
    get { return m_origin; }
  }

  public Vector3 scale
  {
    get { return 1e-2f * Vector3.one; } // mesh data is in centimeters
  }

  public bool Empty()
  {
    return m_tiles.Count == 0;
  }

  private Vector3 PointToLocal(Vector3 worldPoint)
  {
    return m_toLocal * (worldPoint - m_origin);
  }

  private Vector3 PointToWorld(Vector3 localPoint)
  {
    return m_toWorld * localPoint + m_origin;
  }

  private Vector3 VectorToLocal(Vector3 worldVector)
  {
    return m_toLocal * worldVector;
  }

  private IVector3 ToCentimeters(Vector3 meters)
  {
    return new IVector3(100 * meters);
  }

  private Vector3 ToVector3(IVector3 v)
  {
    return new Vector3(v.x, v.y, v.z);
  }

  private int SnapTo(int x, int granularity)
  {
    float f = x;
    f /= granularity;
    int snapped = granularity * Mathf.RoundToInt(f);
    return snapped;
  }

  private IVector3 SnapXY(IVector3 v, int granularity)
  {
    v.x = SnapTo(v.x, granularity);
    v.y = SnapTo(v.y, granularity);
    return v;
  }

  private bool TrySingleRayCast(out Vector3 position, out Vector3 normal, Vector3 from, Vector3 direction, float distance = .01f)
  {
    RaycastHit hit;
    int layerMask = 1 << SpatialMappingManager.Instance.PhysicsLayer;
    if (Physics.Raycast(from, direction, out hit, distance, layerMask))
    {
      position = hit.point;
      normal = hit.normal;
      return true;
    }
    position = Vector3.zero;
    normal = Vector3.zero;
    return false;
  }

  private bool TryGetBasisVectors(out Vector3 x, out Vector3 y, out Vector3 z)
  {
    x = Vector3.zero;
    y = Vector3.zero;
    z = Vector3.zero;
    RaycastHit hit;
    int layerMask = 1 << SpatialMappingManager.Instance.PhysicsLayer;
    float distance = 5f;
    if (!Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, distance, layerMask))
      return false;
    MeshCollider collider = hit.collider as MeshCollider;
    if (collider == null)
      return false;

    // Find x and y basis vectors using a triangle edge and the hit normal
    Mesh mesh = collider.sharedMesh;
    Vector3[] verts = mesh.vertices;
    int[] tris = mesh.GetTriangles(0);
    int t0 = tris[hit.triangleIndex * 3 + 0];
    int t1 = tris[hit.triangleIndex * 3 + 1];
    x = Vector3.Normalize(verts[t1] - verts[t0]);
    z = hit.normal;
    y = Vector3.Cross(x, z);
    return true;
  }

  private bool TileExists(IVector3 position)
  {
    for (int i = 0; i < m_tiles.Count; i++)
    {
      if (position == m_tiles[i].center)
        return true;
    }
    return false;
  }

  public Tuple<Vector3[], int[]> GenerateMeshData()
  {
    float halfDim = 0.5f * QUAD_WIDTH_CM;
    Vector3[] verts = new Vector3[m_tiles.Count * 4];
    int[] tris = new int[m_tiles.Count * 6];
    int vi = 0;
    int ti = 0;
    for (int i = 0; i < m_tiles.Count; i++)
    {
      /*
       * There is a subtlety here re: left-handed vs. right-handed coordinate
       * systems. The selection plane local system is defined in a right-handed
       * space, where the plane normal points out from the visible side of the
       * surface (our +z), +x is to the right, and +y is up.
       * 
       * Unity renders in a left-handed system, where +z would be *into* the
       * visible surface. When +z points out, as in our case, +x is to the 
       * *left*. This is why there is a -1 factor on the local x coordinates of
       * each quad vertex -- to convert from the right-handed system that is
       * natural for me to Unity's left-handed system.
       * 
       * Without this, the polygon winding would be backwards to what Unity 
       * expects.
       */
      Vector3 center = ToVector3(m_tiles[i].center);
      Vector3 topLeft = center + halfDim * ToVector3(new IVector3(-1 * -1, +1));
      Vector3 topRight = center + halfDim * ToVector3(new IVector3(-1 * +1, +1));
      Vector3 bottomRight = center + halfDim * ToVector3(new IVector3(-1 * +1, -1));
      Vector3 bottomLeft = center + halfDim * ToVector3(new IVector3(-1 * -1, -1));
       
      verts[vi + 0] = topLeft;
      verts[vi + 1] = topRight;
      verts[vi + 2] = bottomRight;
      verts[vi + 3] = bottomLeft;

      // First triangle
      tris[ti + 0] = vi + 0;
      tris[ti + 1] = vi + 1;
      tris[ti + 2] = vi + 2;
      // Second
      tris[ti + 3] = vi + 2;
      tris[ti + 4] = vi + 3;
      tris[ti + 5] = vi + 0;

      vi += 4;
      ti += 6;
    }
    return new Tuple<Vector3[], int[]>(verts, tris);
  }

  public void Raycast(Vector3 from, Vector3 direction)
  {
    Vector3 position;
    Vector3 normal;
    if (!TrySingleRayCast(out position, out normal, Camera.main.transform.position, Camera.main.transform.forward, 5f))
    {
      Debug.Log("Initial raycast failed");
      return;
    }

    // If first ever raycast, determine the selection plane and the origin
    // point for the local selection plane coordinate system
    if (Empty())
    {
      if (!TryGetBasisVectors(out m_xAxis, out m_yAxis, out m_normal))
      {
        Debug.Log("Unable to generate basis vectors");
        return;
      }
      m_origin = position;
      m_plane = new Plane(m_normal, m_origin);
      // The - is for right-handed -> left-handed coordinate conversion of
      // quads in pattern, which are specified using right-handed system
      m_toWorld = Quaternion.LookRotation(m_normal, m_yAxis);
      m_toLocal = Quaternion.Inverse(m_toWorld);
    }

    // Perform raycast against plane and then transform result to local
    // coordinate system to get center point of current selection pattern.
    // Convert this to integral units and snap to the tile spacing distance.
    Ray ray = new Ray(from, direction);
    float distance;
    if (!m_plane.Raycast(ray, out distance))
    {
      Debug.Log("Plane raycast failed");
      return;
    }
    Vector3 patternCenterWorld = ray.GetPoint(distance);
    Vector3 patternCenterLocal = PointToLocal(patternCenterWorld);
    IVector3 patternCenter = SnapXY(ToCentimeters(patternCenterLocal), QUAD_WIDTH_CM);

    // Generate all other selection points in local coordinate system and throw
    // away any dupes. These are the center points of our tiles.
    foreach (IVector3 offset in m_pattern)
    {
      IVector3 tilePosition = patternCenter + QUAD_WIDTH_CM * offset;
      if (!TileExists(tilePosition))
      {
        // Perform actual raycasts on each tile against the spatial mesh from a
        // minimum clearance distance above the tile. If an intersection happens
        // above the tile, we have insufficient clearance. If an intersection
        // happens too far below the tile, the selection plane no longer conforms
        // to the surface here and we reject the tile. Lastly, the normal of the
        // underlying surface must not be too different.
        //
        //  * <-- raycast origin
        //  |
        //  | <-- min clearance above
        // ---------- <-- plane
        //  | <-- max clearance allowed below
        //  * <-- hit point on spatial mesh
        Vector3 tilePositionWorld = PointToWorld(1e-2f * ToVector3(tilePosition));
        float epsilon = 1e-2f;
        float minClearanceAbove = 0.3f;
        float maxClearanceBelow = 0.02f;
        Vector3 rayOrigin = tilePositionWorld + m_normal * (minClearanceAbove + epsilon);
        Vector3 rayDirection = -m_normal;
        Vector3 hitPoint;
        Vector3 hitNormal;
        if (TrySingleRayCast(out hitPoint, out hitNormal, rayOrigin, rayDirection, 2*(minClearanceAbove + maxClearanceBelow)))
        {
          float d = Vector3.Distance(hitPoint, rayOrigin);
          if (d >= minClearanceAbove && d < (minClearanceAbove + maxClearanceBelow))
            m_tiles.Add(new Tile(tilePosition));
          //TODO: normal test?
        }
      }
    }
  }

  public SurfaceQuadSelection(int maxQuads)
	{
    m_maxQuads = maxQuads;
    m_tiles = new List<Tile>(m_maxQuads);
    // Z component should always be 0
    IVector3[] pattern =
    {
                                                  new IVector3(+0, +2),
                            new IVector3(-1, +1), new IVector3(+0, +1), new IVector3(+1, +1),
      new IVector3(-2, +0), new IVector3(-1, +0),                       new IVector3(+1, +0), new IVector3(+2, +0),
                            new IVector3(-1, -1), new IVector3(+0, -1), new IVector3(+1, -1),
                                                  new IVector3(+0, -2)
    };
    m_pattern = pattern;
  }
}
