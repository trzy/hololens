using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity.SpatialMapping;

public class MeshExtruder
{
  private List<SelectionTile> m_tiles;
  private List<Vector3> m_vertices;
  private List<int> m_triangles;
  private int m_numTopVertices;
  private float m_extrudeLengthCM = 1;

  // Offset in meters
  public float extrudeLength
  {
    get { return m_extrudeLengthCM * 1e-2f ; }
    set { m_extrudeLengthCM = value * 100; }
  }

  public Tuple<Vector3[], int[]> GetMeshData()
  {
    Vector3[] vertices = m_vertices.ToArray();
    for (int i = 0; i < m_numTopVertices; i++)
    {
      vertices[i].z += m_extrudeLengthCM;
    }
    return new Tuple<Vector3[], int[]>(vertices, m_triangles.ToArray());
  }

  private void GenerateSides()
  {
    int numTopTriangles = m_triangles.Count;

    // Go through each quad
    int tile = 0;
    int vertIdx = m_vertices.Count;
    for (int i = 0; i < numTopTriangles; i += 6, tile++)
    {
      // We expect the quad to be layed out in a very particular way
      int topLeft = m_triangles[i + 0];
      int topRight = m_triangles[i + 1];
      int bottomRight = m_triangles[i + 2];
      int bottomLeft = m_triangles[i + 4];
      
      // For each of the 4 quad edges, create a perpendicular face unless that
      // edge neighbors another tile. In order for winding to be correct (CW),
      // perpendicular face vertices must be added in *reverse* order of the 
      // shared edge.
      if ((m_tiles[tile].neighbors & (byte)SelectionTile.Edge.Top) == 0)
      {
        m_vertices.Add(m_vertices[topLeft]);  // +0
        m_vertices.Add(m_vertices[topRight]); // +1
        // Triangle 1
        m_triangles.Add(topRight);
        m_triangles.Add(topLeft);
        m_triangles.Add(vertIdx + 0);
        // Triangle 2
        m_triangles.Add(vertIdx + 0);
        m_triangles.Add(vertIdx + 1);
        m_triangles.Add(topRight);
        vertIdx += 2;
      }

      if ((m_tiles[tile].neighbors & (byte)SelectionTile.Edge.Right) == 0)
      {
        m_vertices.Add(m_vertices[topRight]);     // +0
        m_vertices.Add(m_vertices[bottomRight]);  // +1
        // Triangle 1
        m_triangles.Add(bottomRight);
        m_triangles.Add(topRight);
        m_triangles.Add(vertIdx + 0);
        // Triangle 2
        m_triangles.Add(vertIdx + 0);
        m_triangles.Add(vertIdx + 1);
        m_triangles.Add(bottomRight);
        vertIdx += 2;
      }

      if ((m_tiles[tile].neighbors & (byte)SelectionTile.Edge.Bottom) == 0)
      {
        m_vertices.Add(m_vertices[bottomRight]);  // +0
        m_vertices.Add(m_vertices[bottomLeft]);   // +1
        // Triangle 1
        m_triangles.Add(bottomLeft);
        m_triangles.Add(bottomRight);
        m_triangles.Add(vertIdx + 0);
        // Triangle 2
        m_triangles.Add(vertIdx + 0);
        m_triangles.Add(vertIdx + 1);
        m_triangles.Add(bottomLeft);
        vertIdx += 2;
      }

      if ((m_tiles[tile].neighbors & (byte)SelectionTile.Edge.Left) == 0)
      {
        m_vertices.Add(m_vertices[bottomLeft]); // +0
        m_vertices.Add(m_vertices[topLeft]);    // +1
        // Triangle 1
        m_triangles.Add(topLeft);
        m_triangles.Add(bottomLeft);
        m_triangles.Add(vertIdx + 0);
        // Triangle 2
        m_triangles.Add(vertIdx + 0);
        m_triangles.Add(vertIdx + 1);
        m_triangles.Add(topLeft);
        vertIdx += 2;
      }
    }
  }

  public MeshExtruder(PlanarTileSelection selection)
  {
    m_tiles = new List<SelectionTile>(selection.tiles);
    Tuple<Vector3[], int[]> meshData = selection.GenerateMeshData();
    m_vertices = new List<Vector3>(meshData.first.Length * 2);
    m_vertices.AddRange(meshData.first);
    m_triangles = new List<int>(meshData.second);

    for (int i =0; i < m_tiles.Count; i++)
    {
      Debug.Log("tile " + i + ": " + m_tiles[i].neighbors);
    }

    // Generate the sides, which will be appended after the top vertices that
    // are extruded by the offset amount
    m_numTopVertices = m_vertices.Count;
    GenerateSides();
  }
}
