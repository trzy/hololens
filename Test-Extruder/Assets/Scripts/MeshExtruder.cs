using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity.SpatialMapping;

public class MeshExtruder
{
  private List<SelectionTile> m_tiles;
  private List<Vector3> m_vertices;
  private List<int> m_triangles;

  public void ExtrudeSimple(out Vector3[] vertices, out int[] triangles, out Vector2[] uv, float extrudeLength, Vector2[] topUV, Vector2[] sideUV)
  {
    bool textured = topUV != null && sideUV != null;

    // Extrude length is specified in world units (meters)
    float extrudeLengthCM = 100 * extrudeLength;
    Vector3 extrude = new Vector3(0, 0, extrudeLengthCM);

    // Create mesh data arrays with enough headroom for inserted triangles
    int maxNumTriangles = m_triangles.Count + (m_triangles.Count / 6) * 4 * 6;
    vertices = new Vector3[m_vertices.Count * 3];
    triangles = new int[maxNumTriangles];
    uv = new Vector2[vertices.Length];
    m_vertices.CopyTo(vertices);
    m_triangles.CopyTo(triangles);

    // Generate UVs for top vertices. For this to work, vertices must not be
    // shared (selection mesh should also have been textured) and must be laid
    // out as in the selection mesh.
    if (textured)
    {
      for (int i = 0; i < m_vertices.Count; i += 4)
      {
        for (int j = 0; j < 4; j++)
        {
          uv[i + j] = topUV[j];
        }
      }
    }

    // Side wall UV indices
    Vector2 sideUVTopLeft = sideUV[0];
    Vector2 sideUVTopRight = sideUV[1];
    Vector2 sideUVBottomRight = sideUV[2];
    Vector2 sideUVBottomLeft = sideUV[3];

    // Go through each tile and generate side walls
    int tile = 0;
    int vertIdx = m_vertices.Count;
    int triIdx = m_triangles.Count;
    for (int i = 0; i < m_triangles.Count; i += 6, tile++)
    {
      // We expect the quad to be layed out in a very particular way
      int topLeft = m_triangles[i + 0];
      int topRight = m_triangles[i + 1];
      int bottomRight = m_triangles[i + 2];
      int bottomLeft = m_triangles[i + 4];

      // If texture mapping is enabled, top vertices have to be duplicated for
      // the sides and these will be reassigned
      int topLeftExtruded = topLeft;
      int topRightExtruded = topRight;
      int bottomRightExtruded = bottomRight;
      int bottomLeftExtruded = bottomLeft;

      // For each of the 4 quad edges, create a perpendicular face unless that
      // edge neighbors another tile. In order for winding to be correct (CW),
      // perpendicular face vertices must be added in *reverse* order of the 
      // shared edge.
      if ((m_tiles[tile].neighbors & (byte)SelectionTile.Edge.Top) == 0)
      {
        if (textured)
        {
          // If texturing is enabled, we have to duplicate the two top vertices for
          // each edge
          vertices[vertIdx + 0] = vertices[topLeft] + extrude;
          vertices[vertIdx + 1] = vertices[topRight] + extrude;
          uv[vertIdx + 0] = sideUVTopRight;
          uv[vertIdx + 1] = sideUVTopLeft;
          topLeftExtruded = vertIdx + 0;
          topRightExtruded = vertIdx + 1;
          vertIdx += 2;
          uv[vertIdx + 0] = sideUVBottomRight;
          uv[vertIdx + 1] = sideUVBottomLeft;
        }
        // Bottom (fixed, non-extruded) vertices
        vertices[vertIdx + 0] = vertices[topLeft];
        vertices[vertIdx + 1] = vertices[topRight];
        // Triangle 1
        triangles[triIdx++] = topRightExtruded;
        triangles[triIdx++] = topLeftExtruded;
        triangles[triIdx++] = vertIdx + 0;
        // Triangle 2
        triangles[triIdx++] = vertIdx + 0;
        triangles[triIdx++] = vertIdx + 1;
        triangles[triIdx++] = topRightExtruded;
        vertIdx += 2;
      }

      if ((m_tiles[tile].neighbors & (byte)SelectionTile.Edge.Right) == 0)
      {
        if (textured)
        {
          // If texturing is enabled, we have to duplicate the two top vertices for
          // each edge
          vertices[vertIdx + 0] = vertices[topRight] + extrude;
          vertices[vertIdx + 1] = vertices[bottomRight] + extrude;
          uv[vertIdx + 0] = sideUVTopRight;
          uv[vertIdx + 1] = sideUVTopLeft;
          topRightExtruded = vertIdx + 0;
          bottomRightExtruded = vertIdx + 1;
          vertIdx += 2;
          uv[vertIdx + 0] = sideUVBottomRight;
          uv[vertIdx + 1] = sideUVBottomLeft;
        }
        // Bottom (fixed, non-extruded) vertices
        vertices[vertIdx + 0] = vertices[topRight];
        vertices[vertIdx + 1] = vertices[bottomRight];
        // Triangle 1
        triangles[triIdx++] = bottomRightExtruded;
        triangles[triIdx++] = topRightExtruded;
        triangles[triIdx++] = vertIdx + 0;
        // Triangle 2
        triangles[triIdx++] = vertIdx + 0;
        triangles[triIdx++] = vertIdx + 1;
        triangles[triIdx++] = bottomRightExtruded;
        vertIdx += 2;
      }

      if ((m_tiles[tile].neighbors & (byte)SelectionTile.Edge.Bottom) == 0)
      {
        if (textured)
        {
          // If texturing is enabled, we have to duplicate the two top vertices for
          // each edge
          vertices[vertIdx + 0] = vertices[bottomRight] + extrude;
          vertices[vertIdx + 1] = vertices[bottomLeft] + extrude;
          uv[vertIdx + 0] = sideUVTopRight;
          uv[vertIdx + 1] = sideUVTopLeft;
          bottomRightExtruded = vertIdx + 0;
          bottomLeftExtruded = vertIdx + 1;
          vertIdx += 2;
          uv[vertIdx + 0] = sideUVBottomRight;
          uv[vertIdx + 1] = sideUVBottomLeft;
        }
        // Bottom (fixed, non-extruded) vertices
        vertices[vertIdx + 0] = vertices[bottomRight];
        vertices[vertIdx + 1] = vertices[bottomLeft];
        // Triangle 1
        triangles[triIdx++] = bottomLeftExtruded;
        triangles[triIdx++] = bottomRightExtruded;
        triangles[triIdx++] = vertIdx + 0;
        // Triangle 2
        triangles[triIdx++] = vertIdx + 0;
        triangles[triIdx++] = vertIdx + 1;
        triangles[triIdx++] = bottomLeftExtruded;
        vertIdx += 2;
      }

      if ((m_tiles[tile].neighbors & (byte)SelectionTile.Edge.Left) == 0)
      {
        if (textured)
        {
          // If texturing is enabled, we have to duplicate the two top vertices for
          // each edge
          vertices[vertIdx + 0] = vertices[bottomLeft] + extrude;
          vertices[vertIdx + 1] = vertices[topLeft] + extrude;
          uv[vertIdx + 0] = sideUVTopRight;
          uv[vertIdx + 1] = sideUVTopLeft;
          bottomLeftExtruded = vertIdx + 0;
          topLeftExtruded = vertIdx + 1;
          vertIdx += 2;
          uv[vertIdx + 0] = sideUVBottomRight;
          uv[vertIdx + 1] = sideUVBottomLeft;
        }
        // Bottom (fixed, non-extruded) vertices
        vertices[vertIdx + 0] = vertices[bottomLeft];
        vertices[vertIdx + 1] = vertices[topLeft];
        // Triangle 1
        triangles[triIdx++] = topLeftExtruded;
        triangles[triIdx++] = bottomLeftExtruded;
        triangles[triIdx++] = vertIdx + 0;
        // Triangle 2
        triangles[triIdx++] = vertIdx + 0;
        triangles[triIdx++] = vertIdx + 1;
        triangles[triIdx++] = topLeftExtruded;
        vertIdx += 2;
      }
    }

    // Don't forget to extrude the original, top vertices
    for (int i = 0; i < m_vertices.Count; i++)
    {
      vertices[i].z += extrudeLengthCM;
    }
  }

  public MeshExtruder(PlanarTileSelection selection)
  {
    m_tiles = new List<SelectionTile>(selection.tiles);
    Vector3[] vertices;
    int[] triangles;
    Vector2[] uv;
    selection.GenerateMeshData(out vertices, out triangles, out uv);
    m_vertices = new List<Vector3>(vertices);
    m_triangles = new List<int>(triangles);
  }
}
