/*
 * Creates a planar selection mesh in a local coordinate system with centimer
 * units. A transform can be obtained for conversion to world space. The local
 * coordinate system has +z pointing out of the selection plane. Due to Unity's
 * left-handed coordinate system, this means that +x is to the *left* when
 * viewed with +z facing the camera.
 * 
 * TODO:
 * -----
 * - Optimize vertex sharing.
 * - Optimize neighbor detection.
 * - Figure out how to use a right-handed system and perform the conversion to
 *   left-handed with the transform.
 */

using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity.SpatialMapping;

public class PlanarTileSelection
{
  public readonly IVector3[] defaultPattern =
  {
                                                new IVector3(+0, +2),
                          new IVector3(-1, +1), new IVector3(+0, +1), new IVector3(+1, +1),
    new IVector3(-2, +0), new IVector3(-1, +0), new IVector3(+0, +0), new IVector3(+1, +0), new IVector3(+2, +0),
                          new IVector3(-1, -1), new IVector3(+0, -1), new IVector3(+1, -1),
                                                new IVector3(+0, -2)
  };

  private int m_maxTiles;
  private Vector2[] m_tileUV;

  // Selection plane definition in world units
  private Vector3 m_xAxis = Vector3.zero;
  private Vector3 m_yAxis = Vector3.zero;
  private Vector3 m_normal = Vector3.zero;  // local z axis
  private Vector3 m_origin = Vector3.zero;  // along with the normal defines the current selection plane
  private Quaternion m_toWorld;
  private Quaternion m_toLocal;
  private Vector3 m_lastTrackedPosition;
  private Plane m_plane;                    // current selection plane defined from the above

  // Tiles in local, centimeter units
  private List<SelectionTile> m_tiles;

  // Selection pattern
  private IVector3[] m_pattern;

  public List<SelectionTile> tiles
  {
    get { return m_tiles; }
  }

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

  private bool TryGetBasisVectors(out Vector3 x, out Vector3 y, out Vector3 z, Vector3 from, Vector3 direction, float length)
  {
    x = Vector3.zero;
    y = Vector3.zero;
    z = Vector3.zero;
    RaycastHit hit;
    int layerMask = 1 << SpatialMappingManager.Instance.PhysicsLayer;
    if (!Physics.Raycast(from, direction, out hit, length, layerMask))
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

  private void AddTile(IVector3 position)
  {
    SelectionTile tile = new SelectionTile(position);
    for (int i = 0; i < m_tiles.Count; i++)
    {
      if (tile.center == m_tiles[i].center)
        return;
      SelectionTile tmp = m_tiles[i]; // annoyingly, [] is a function call that returns a copy
      tmp.AddNeighbor(ref tile);
      m_tiles[i] = tmp;               // write back to list
    }
    m_tiles.Add(tile);
  }

  //TODO: this is really pretty terrible
  private void RemoveExcessTiles()
  {
    while (m_tiles.Count > m_maxTiles)
    {
      IVector3 position = m_tiles[0].center;
      // Remove tile 0 and unlink it from neighbors
      for (int i = 1; i < m_tiles.Count; i++)
      {
        SelectionTile tmp = m_tiles[i];
        tmp.RemoveNeighbor(position);
        m_tiles[i] = tmp;
      }
      m_tiles.RemoveAt(0);
    }
  }

  private int FindFirstIndex(Vector3[] verts, Vector3 vert)
  {
    for (int i = 0; i < verts.Length; i++)
    {
      if (verts[i] == vert)
        return i;
    }
    return 0; // should never happen
  }

  public void GenerateMeshData(out Vector3[] verts, out int[] tris, out Vector2[] uv)
  {
    float halfDim = 0.5f * SelectionTile.SIDE_CM;
    verts = new Vector3[m_tiles.Count * 4];
    tris = new int[m_tiles.Count * 6];
    uv = (m_tileUV != null) ? new Vector2[m_tiles.Count * 4] : null;
    int vi = 0;
    int ti = 0;
    for (int i = 0; i < m_tiles.Count; i++)
    {
      /*
       * Unity uses left-handed coordinates. The selection plane local system
       * is defined with +z going out from the visible side of the surface, 
       * which means +x is to the *left*, and +y is up. Hence for clockwise
       * winding, the top-left corner has x = +1 and the top-right is x = -1.
       */
      Vector3 center = ToVector3(m_tiles[i].center);
      Vector3 topLeft = center + halfDim * ToVector3(new IVector3(+1, +1));
      Vector3 topRight = center + halfDim * ToVector3(new IVector3(-1, +1));
      Vector3 bottomRight = center + halfDim * ToVector3(new IVector3(-1, -1));
      Vector3 bottomLeft = center + halfDim * ToVector3(new IVector3(+1, -1));
       
      verts[vi + 0] = topLeft;
      verts[vi + 1] = topRight;
      verts[vi + 2] = bottomRight;
      verts[vi + 3] = bottomLeft;

      if (m_tileUV == null)
      {
        // No textures, can use shared vertices

        //TODO: vertex sharing could be done *a lot* more efficiently by
        //      generating vertices inside the tile structure and then linking
        //      tiles to their neighbors

        // First triangle
        tris[ti + 0] = FindFirstIndex(verts, topLeft);
        tris[ti + 1] = FindFirstIndex(verts, topRight);
        tris[ti + 2] = FindFirstIndex(verts, bottomRight);
        // Second
        tris[ti + 3] = FindFirstIndex(verts, bottomRight);
        tris[ti + 4] = FindFirstIndex(verts, bottomLeft);
        tris[ti + 5] = FindFirstIndex(verts, topLeft);
      }
      else
      {
        // Texture mapping cannot coexist with independent vertices
        // First triangle
        tris[ti + 0] = vi + 0;
        tris[ti + 1] = vi + 1;
        tris[ti + 2] = vi + 2;
        // Second
        tris[ti + 3] = vi + 2;
        tris[ti + 4] = vi + 3;
        tris[ti + 5] = vi + 0;
        // UV coordinates
        for (int j = 0; j < 4; j++)
        {
          uv[vi + j] = m_tileUV[j];
        }
      }

      vi += 4;
      ti += 6;
    }
  }

  public void Raycast(Vector3 from, Vector3 direction, float length)
  {
    Vector3 position;
    Vector3 normal;
    if (!TrySingleRayCast(out position, out normal, from, direction, length))
    {
      Debug.Log("Initial raycast failed");
      return;
    }

    // If first ever raycast, determine the selection plane and the origin
    // point for the local selection plane coordinate system
    if (Empty())
    {
      if (!TryGetBasisVectors(out m_xAxis, out m_yAxis, out m_normal, from, direction, length))
      {
        Debug.Log("Unable to generate basis vectors");
        return;
      }
      m_origin = position;
      m_plane = new Plane(m_normal, m_origin);
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
    IVector3 patternCenter = SnapXY(ToCentimeters(patternCenterLocal), SelectionTile.SIDE_CM);

    // Generate all other selection points in local coordinate system and throw
    // away any dupes. These are the center points of our tiles.
    int numTilesCreated = 0;
    foreach (IVector3 offset in m_pattern)
    {
      IVector3 tilePosition = patternCenter + SelectionTile.SIDE_CM * offset;
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
          {
            AddTile(tilePosition);
            numTilesCreated++;
          }
          //TODO: normal test?
        }
      }
    }

    // If we haven't been able to create any tiles, and have strayed too far,
    // clear everything out and start over
    if (numTilesCreated == 0)
    {
      float distanceStrayed = Vector3.Magnitude(patternCenterWorld - m_lastTrackedPosition);
      Debug.Log("strayed " + distanceStrayed + " m");
      if (distanceStrayed > 0.2f)
      {
        m_tiles.Clear();
        return;
      }
    }
    else
      m_lastTrackedPosition = patternCenterWorld;

    RemoveExcessTiles();
  }

  public void Reset()
  {
    m_tiles.Clear();
  }

  public void SetPattern(int maxTiles, IVector3[] pattern)
  {
    m_maxTiles = maxTiles;
    m_pattern = pattern == null ? defaultPattern : pattern;
  }

  // Selection pattern Z component should always be 0
  public PlanarTileSelection(int maxTiles, Vector2[] tileUV = null, IVector3[] pattern = null)
  {
    m_tileUV = tileUV;
    m_tiles = new List<SelectionTile>(m_maxTiles);
    SetPattern(maxTiles, pattern);
  }
}
