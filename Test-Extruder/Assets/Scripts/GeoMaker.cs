using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity.SpatialMapping;
using HoloLensXboxController;

public class GeoMaker: MonoBehaviour
{
  [Tooltip("Material to render selected patches with")]
  public Material selectedMaterial = null;

  private ControllerInput m_xboxController = null;

  private class ExtrudableMesh
  {
    public enum State
    {
      Select,
      Volume,
      Finished
    };
    private State m_state = State.Select;
    private static int m_instanceNumber = 0;
    private int m_maxNumQuads = 0;
    private GameObject m_gameObject = null;
    private Mesh m_mesh = null;
    private MeshRenderer m_meshRenderer = null;
    private Dictionary<MeshCollider, Tuple<Vector3[], int[]>> m_meshDataByCollider = new Dictionary<MeshCollider, Tuple<Vector3[], int[]>>();
    private List<Vector3> m_vertices = new List<Vector3>();
    private List<int> m_triangles = new List<int>();
    private List<Vector3> m_centroids = new List<Vector3>();
    private List<Vector3> m_faceNormals = new List<Vector3>();
    private List<Tuple<Vector3, int>> m_edges = new List<Tuple<Vector3, int>>();
    private float m_offset = 2e-2f; // start off slightly offset so as not to be occluded

    public State state
    {
      get { return m_state; }
    }

    public float offset
    {
      get { return m_offset; }
      set { m_offset = value; UpdateMesh(); }
    }
    
    public bool Empty()
    {
      return m_triangles.Count == 0;
    }

    public void GenerateVolumeMesh()
    {
      if (m_state == State.Finished)
      {
        return;
      }
      m_state = State.Volume;
    }

    public void FinalizeMesh()
    {
      m_state = State.Finished;
    }

    private void UpdateSelectionMesh()
    {
      Vector3[] vertices = m_vertices.ToArray();
      for (int i = 0; i < vertices.Length; i++)
      {
        vertices[i] += m_faceNormals[i / 4] * offset;
      }
      m_mesh.vertices = vertices; // always assign vertices first
      m_mesh.triangles = m_triangles.ToArray();
      m_mesh.RecalculateBounds();
    }

    private List<int> MakeSharedTriangles(List<int> triangles, Vector3[] vertices)
    {
      List<int> outTriangles = new List<int>(triangles.Count);
      float tolerance = .5e-2f;
      float tolerance2 = tolerance * tolerance;
      foreach (int t in triangles)
      {
        int triIdx = t;
        Vector3 vertex = vertices[t];
        for (int j = 0; j < outTriangles.Count; j++)
        {
          Vector3 existingVertex = vertices[outTriangles[j]];
          float sqrDist = Vector3.SqrMagnitude(vertex - existingVertex);
          if (sqrDist <= tolerance2)
          {
            triIdx = outTriangles[j];
            break;
          }
        }
        outTriangles.Add(triIdx);
      }
      return outTriangles;
    }

    private void UpdateVolumeMesh()
    { 
      int numSurfaceVertices = m_vertices.Count;
      int numSurfaceTriangles = m_triangles.Count;

      // For each triangle, create 3 perpendicular faces (one for each edge)
      // consisting of two triangles each (total of 6 new triangles). Reuse the
      // vertices from the existing triangle faces (the surface triangles).
      int numTriangles = numSurfaceTriangles + (numSurfaceTriangles / 3) * 6 * 3;
      int numVertices = numSurfaceVertices * 2;
      Vector3[] vertices = new Vector3[numVertices];
      int[] triangles = new int[numTriangles];
      m_vertices.CopyTo(0, vertices, 0, numSurfaceVertices);
      System.Array.Copy(vertices, 0, vertices, numSurfaceVertices, numSurfaceVertices);
      m_triangles.CopyTo(0, triangles, 0, numSurfaceTriangles);
      int outIdx = numSurfaceTriangles;
      for (int i = 0; i < numSurfaceTriangles; i += 3)
      {
        // Triangle vertex indices
        int[] t = { m_triangles[i + 0], m_triangles[i + 1], m_triangles[i + 2] };

        // Create perpendicular faces from edges. In order for winding to be
        // correct (CW), perpendicular face vertices must be added in reverse
        // order of the shared edge.
        for (int j = 0; j < 3; j++)
        {
          // First two vertices are from the surface, whose vertices are at the
          // front of the vertex array
          int i1 = t[(j + 1) % 3];
          int i2 = t[j];

          // For interior edges, we do not generate faces
          Vector3 midpoint = 0.5f * (m_vertices[i1] + m_vertices[i2]);
          if (IsInteriorEdge(midpoint))
            continue;

          // Next two are the newly generated base, which are in the upper half
          // of the vertex array.
          int i3 = i2 + numSurfaceVertices;
          int i4 = i1 + numSurfaceVertices;

          // Generate two triangles
          triangles[outIdx++] = i1;
          triangles[outIdx++] = i2;
          triangles[outIdx++] = i3;

          triangles[outIdx++] = i1;
          triangles[outIdx++] = i3;
          triangles[outIdx++] = i4;
        }
      }

      // Apply offset in direction of normal for the surface triangles. Surface
      // triangle vertices are stored 1:1 with the indices, so it is safe to do
      // this.
      Vector3 avgNormal = Vector3.zero;
      for (int i = 0; i < m_faceNormals.Count; i++)
      {
        avgNormal += m_faceNormals[i];
      }
      avgNormal /= m_faceNormals.Count;
      for (int i = 0; i < numSurfaceVertices; i++)
      {
        // We divide by 4 here because we store only one face normal for every
        // quad (two triangles)
        //vertices[i] += m_faceNormals[i / 4] * offset;
        vertices[i] += avgNormal * offset;
      }

      // Update mesh
      m_mesh.vertices = vertices; // always assign vertices first
      //m_mesh.normals = normals;
      m_mesh.triangles = triangles;
      m_mesh.RecalculateNormals();
      m_mesh.RecalculateBounds();
    }

    public void UpdateMesh()
    {
      if (m_state == State.Select)
        UpdateSelectionMesh();
      else
        UpdateVolumeMesh();
    }

    private bool IsInteriorEdge(Vector3 midpoint)
    {
      // If this edge is shared, it is not an external edge
      foreach (Tuple<Vector3, int> edge in m_edges)
      {
        float sqrDist = Vector3.SqrMagnitude(edge.first - midpoint);
        if (sqrDist <= 1e-2f * 1e-2f)
        {
          return edge.second > 1;
        }
      }
      return false;
    }

    private void RemoveQuadEdge(Vector3 midpoint)
    {
      for (int i = 0; i < m_edges.Count; i++)
      {
        Tuple<Vector3, int> edge = m_edges[i];
        float sqrDist = Vector3.SqrMagnitude(edge.first - midpoint);
        if (sqrDist <= 1e-2f * 1e-2f)
        {
          if (edge.second <= 1)
          {
            m_edges.RemoveAt(i);
            return;
          }
          m_edges[i] = new Tuple<Vector3, int>(edge.first, edge.second - 1);
          return;
        }
      }
    }

    private void RemoveQuadEdges(int start, int numVertices)
    {
      for (int i = start; i < start + numVertices; i += 4)
      {
        RemoveQuadEdge(0.5f * (m_vertices[i + 0] + m_vertices[i + 1]));
        RemoveQuadEdge(0.5f * (m_vertices[i + 1] + m_vertices[i + 2]));
        RemoveQuadEdge(0.5f * (m_vertices[i + 2] + m_vertices[i + 3]));
        RemoveQuadEdge(0.5f * (m_vertices[i + 3] + m_vertices[i + 0]));
        // And the diagonal edges
        RemoveQuadEdge(0.5f * (m_vertices[i + 0] + m_vertices[i + 2]));
        RemoveQuadEdge(0.5f * (m_vertices[i + 3] + m_vertices[i + 2]));
      }
    }

    private void AddQuadEdge(Vector3 midpoint)
    {
      // If edge exists already, increase ref count
      for (int i = 0; i < m_edges.Count; i++)
      {
        Tuple<Vector3, int> edge = m_edges[i];
        float sqrDist = Vector3.SqrMagnitude(edge.first - midpoint);
        if (sqrDist <= 1e-2f * 1e-2f)
        {
          m_edges[i] = new Tuple<Vector3, int>(edge.first, edge.second + 1);
          Debug.Log("found dupe edge");
          return;
        }
      }

      // Otherwise, insert into list
      m_edges.Add(new Tuple<Vector3, int>(midpoint, 1));
    }

    private void AddQuadEdges(Vector3[] vertices)
    {
      // Compute the 4 edge midpoints and add them
      AddQuadEdge(0.5f * (vertices[0] + vertices[1]));
      AddQuadEdge(0.5f * (vertices[1] + vertices[2]));
      AddQuadEdge(0.5f * (vertices[2] + vertices[3]));
      AddQuadEdge(0.5f * (vertices[3] + vertices[0]));
      // And the diagonal edges
      AddQuadEdge(0.5f * (vertices[0] + vertices[2]));
      AddQuadEdge(0.5f * (vertices[1] + vertices[3]));
    }

    private void RemoveExcessQuads()
    {
      if (m_maxNumQuads <= 0)
        return;
      int numQuads = m_triangles.Count / 6;
      int numQuadsToRemove = numQuads - m_maxNumQuads;
      if (numQuadsToRemove > 0)
      {
        RemoveQuadEdges(0, numQuadsToRemove * 4);
        m_vertices.RemoveRange(0, numQuadsToRemove * 4);
        m_triangles.RemoveRange(0, numQuadsToRemove * 6);
        for (int i = 0; i < m_triangles.Count; i++)
        {
          // Fix up the indices!
          m_triangles[i] -= numQuadsToRemove * 4;
        }
        m_centroids.RemoveRange(0, numQuadsToRemove);
        m_faceNormals.RemoveRange(0, numQuadsToRemove);
      }
    }

    private bool QuadExists(Vector3 centroid)
    {
      foreach (Vector3 c in m_centroids)
      {
        float absDist = Mathf.Abs(Vector3.Distance(c, centroid));
        if (absDist < 2e-2)
        {
          return true;
        }
      }
      return false;
    }

    private bool AddQuad(MeshCollider collider, int triangleIndex, Vector3 faceNormal)
    {
      // Fetch pre-cached mesh data
      Tuple<Vector3[], int[]> vertsAndTris;
      Vector3[] colliderVertices;
      int[] colliderTriangles;
      if (m_meshDataByCollider.TryGetValue(collider, out vertsAndTris))
      {
        colliderVertices = vertsAndTris.first;
        colliderTriangles = vertsAndTris.second;
      }
      else
      {
        // We haven't encountered this collider yet. Cache its mesh data.
        Mesh mesh = collider.sharedMesh;
        colliderVertices = mesh.vertices;
        colliderTriangles = mesh.GetTriangles(0);
        m_meshDataByCollider.Add(collider, new Tuple<Vector3[], int[]>(colliderVertices, colliderTriangles));
      }

      // Extract the triangle
      Vector3 v0 = colliderVertices[colliderTriangles[triangleIndex * 3 + 0]];
      Vector3 v1 = colliderVertices[colliderTriangles[triangleIndex * 3 + 1]];
      Vector3 v2 = colliderVertices[colliderTriangles[triangleIndex * 3 + 2]];

      // Compute edges
      Vector3[] e = new Vector3[3];
      e[0] = v1 - v0;
      e[1] = v2 - v0;
      e[2] = v2 - v1;

      /*
       * Figure out which two edges are perpendicular (spatial understanding
       * produces rectangular patches consisting of two triangles). If none are
       * then discard this selection entirely. Knowing the perpendicular edges,
       * we can compute the 4th vertex to complete the square and emit a second
       * triangle.
       * 
       * Vertices are reordered such that the 4 vertices can be traversed in
       * clockwise order around the square (to assist with edge calculation
       * later on). For example:
       * 
       *  0 --- 1
       *  |     |
       *  3 --- 2
       */

      Vector3[] v = new Vector3[4];
      int[] triangles = new int[6];
      triangles[0] = m_vertices.Count + 0;
      triangles[1] = m_vertices.Count + 1;
      triangles[2] = m_vertices.Count + 2;
      triangles[3] = m_vertices.Count + 0;
      triangles[4] = m_vertices.Count + 2;
      triangles[5] = m_vertices.Count + 3;
      if (Mathf.Abs(Vector3.Dot(e[0], e[1])) < 1e-1 * Vector3.Magnitude(e[0]) * Vector3.Magnitude(e[1]))
      {
        v[0] = v0;
        v[1] = v1;
        v[2] = v0 + e[0] + e[1];
        v[3] = v2;
      }
      else if (Mathf.Abs(Vector3.Dot(e[0], e[2])) < 1e-1 * Vector3.Magnitude(e[0]) * Vector3.Magnitude(e[2]))
      {
        v[0] = v0;
        v[1] = v1;
        v[2] = v2;
        v[3] = v0 + e[2];
      }
      else if (Mathf.Abs(Vector3.Dot(e[1], e[2])) < 1e-1 * Vector3.Magnitude(e[0]) * Vector3.Magnitude(e[2]))
      {
        v[0] = v1;
        v[1] = v2;
        v[2] = v0;
        v[3] = v0 - e[2];
      }
      else
      {
        return false;
      }
            
      // Compute centroid, which we will use to identify this quad, and check
      // whether we have already included it
      Vector3 centroid = 0.25f * (v[0] + v[1] + v[2] + v[3]);
      if (QuadExists(centroid))
      {
        return false;
      }
      m_centroids.Add(centroid);

      // Add edges
      AddQuadEdges(v);

      // Append this triangle and the one we generated
      m_vertices.AddRange(v);
      m_triangles.AddRange(triangles);

      // Only *one* face normal for both triangles
      m_faceNormals.Add(faceNormal);

      return true;
    }

    public void SelectPoints(Vector3 rayPosition, Vector3 rayDirection)
    {
      RaycastHit hit;
      int layerMask = 1 << SpatialMappingManager.Instance.PhysicsLayer;
      float distance = 5f;
      if (!Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, distance, layerMask))
      {
        // If we didn't hit anything, just leave current selection as-is
        return;
      }
      else
      {
        // Add first hit point
        MeshCollider collider = hit.collider as MeshCollider;
        if (collider == null)
        {
          return;
        }
        AddQuad(collider, hit.triangleIndex, hit.normal);
      }

      // Define a set of points in the plane of the hit normal through which we
      // will perform raycasts to find more triangles
      float scale = 2.5e-2f;
      float distanceAbove = scale;
      Vector2[] rayMask =
      {
        scale * new Vector2(-1, 0),
        scale * new Vector2(1, 0),
        scale * new Vector2(0, -1),
        scale * new Vector2(0, 1),
        scale * new Vector2(-1, -1),
        scale * new Vector2(1, -1),
        scale * new Vector2(1, 1),
        scale * new Vector2(-1, 1)
      };

      // Perform the ray casts at close proximity above the originally detected
      // point
      RaycastHit maskHit;
      Quaternion toWorld = Quaternion.LookRotation(hit.normal);
      for (int i = 0; i < rayMask.Length; i++)
      {
        Vector3 point = hit.point + toWorld * new Vector3(rayMask[i].x, rayMask[i].y, distanceAbove);
        //Debug.Log("hit point = " + hit.point + ", normal = " + hit.normal + ", test point = " + toWorld*new Vector3(0, 1, 1));
        if (Physics.Raycast(point, -hit.normal, out maskHit, 2 * distanceAbove, layerMask))
        {
          MeshCollider collider = maskHit.collider as MeshCollider;
          if (collider != null)
          {
            AddQuad(collider, maskHit.triangleIndex, maskHit.normal);
          }
        }
      }

      // Cap the number of selected quads
      RemoveExcessQuads();
    }

    public ExtrudableMesh(Material selectedMaterial, Transform parentXform, int maxNumQuads)
    {
      m_maxNumQuads = maxNumQuads;

      // Create reticle game object and mesh
      m_gameObject = new GameObject("Selected-Patch-" + m_instanceNumber++.ToString());
      m_gameObject.transform.parent = parentXform;
      m_mesh = m_gameObject.AddComponent<MeshFilter>().mesh;
      m_meshRenderer = m_gameObject.AddComponent<MeshRenderer>();
      m_meshRenderer.material = selectedMaterial;
      m_meshRenderer.material.color = Color.white;
      m_meshRenderer.enabled = true;
    }
  }

  private ExtrudableMesh m_currentMesh = null;

  private MeshCollider FindGazeTarget(out RaycastHit hit, float distance)
  {
    int layerMask = 1 << SpatialMappingManager.Instance.PhysicsLayer;
    if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, distance, layerMask))
      return hit.collider as MeshCollider;
    return null;
  }

  private void Update()
  {
    if (!PlayspaceManager.Instance.IsScanningComplete())
      return;

    // Get current joypad axis values
#if UNITY_EDITOR
    float hor = Input.GetAxis("Horizontal");
    float ver = Input.GetAxis("Vertical");
    bool buttonA = Input.GetKeyDown(KeyCode.Joystick1Button0);
    bool buttonB = Input.GetKeyDown(KeyCode.Joystick1Button1);
#else
    m_xboxController.Update();
    float hor = m_xboxController.GetAxisLeftThumbstickX();
    float ver = m_xboxController.GetAxisLeftThumbstickY();
    bool buttonA = m_xboxController.GetButtonDown(ControllerButton.A);
    bool buttonB = m_xboxController.GetButtonDown(ControllerButton.B);
#endif

    float delta = (Mathf.Abs(ver) > 0.25f ? ver : 0) * Time.deltaTime;

    if (m_currentMesh == null)
    {
      m_currentMesh = new ExtrudableMesh(selectedMaterial, transform, 75);
    }

    if (m_currentMesh.state == ExtrudableMesh.State.Select)
    {
      m_currentMesh.SelectPoints(Camera.main.transform.position, Camera.main.transform.forward);
      if (buttonA)
      {
        m_currentMesh.GenerateVolumeMesh();
      }
      m_currentMesh.UpdateMesh();
    }
    else if (m_currentMesh.state == ExtrudableMesh.State.Volume)
    {
      m_currentMesh.offset += delta;
      m_currentMesh.UpdateMesh();
      if (buttonA)
      {
        m_currentMesh.FinalizeMesh();
      }
    }
  }

  private void Awake()
  {
#if !UNITY_EDITOR
    m_xboxController = new ControllerInput(0, 0.19f);
#endif
  }
}
