using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity.SpatialMapping;

public class MeshExtruder
{
  private List<SelectionTile> m_tiles;
  private List<Vector3> m_vertices;
  private List<int> m_triangles;

  private void EmitCappedSideQuad(Vector3[] vertices, int vertIdx, int[] triangles, int triIdx, Vector2[] uv, Vector2[] uvTemplate, Vector3 topLeft, Vector3 topRight, Vector3 bottomRight, Vector3 bottomLeft)
  {
    vertices[vertIdx + 0] = topLeft;
    vertices[vertIdx + 1] = topRight;
    vertices[vertIdx + 2] = bottomRight;
    vertices[vertIdx + 3] = bottomLeft;
    // Triangle 1
    triangles[triIdx++] = vertIdx + 0;
    triangles[triIdx++] = vertIdx + 1;
    triangles[triIdx++] = vertIdx + 2;
    // Triangle 2
    triangles[triIdx++] = vertIdx + 2;
    triangles[triIdx++] = vertIdx + 3;
    triangles[triIdx++] = vertIdx + 0;
    // UV
    for (int j = 0; j < 4; j++)
    {
      uv[vertIdx++] = uvTemplate[j];
    }
  }

  /*   
   * Selected surface. Top UVs applied here. Also refered to as the "top" or
   * "extruded" surface.
   *    |   
   *    V
   *   +----+
   *  /    /|
   * +----+ | <-- Crown. Fixed height.
   * |    | +
   * |    |/|
   * +----+ | <-- Base. Like the side wall in ExtrudeSimple(), height varies by
   * |    | |     extrusion length.
   * |    | +
   * |    |/
   * +----+
   */
  public void ExtrudeCapped(out Vector3[] vertices, out int[] triangles, out Vector2[] uv, float extrudeLength, Vector2[] topUV, Vector2[] crownUV, Vector2[] baseUV)
  {
    float extrudeLengthCM = 100 * extrudeLength;
    Vector3 extrude = new Vector3(0, 0, extrudeLengthCM);

    // Create mesh data arrays with enough headroom for inserted triangles
    int extraVertsPerTile = 8 * 4;  // for each side, 4 for crown, 4 for base
    int extraTrisPerTile = 2 * 2 * 4 * 3;
    vertices = new Vector3[m_vertices.Count + m_tiles.Count * extraVertsPerTile];
    uv = new Vector2[vertices.Length];
    triangles = new int[m_triangles.Count + m_tiles.Count * extraTrisPerTile];
    m_vertices.CopyTo(vertices);
    m_triangles.CopyTo(triangles);

    // Apply UVs to top surface
    for (int i = 0; i < m_vertices.Count; i += 4)
    {
      for (int j = 0; j < 4; j++)
      {
        uv[i + j] = topUV[j];
      }
    }

    // TODO: parameterize this better
    float crownHeightCM = extrudeLengthCM * 0.25f;
    float baseHeightCM = extrudeLengthCM * 0.75f;
    Vector3 baseExtrude = new Vector3(0, 0, baseHeightCM);

    // Go through each tile and generate side walls
    int tile = 0;
    int vertIdx = m_vertices.Count;
    int triIdx = m_triangles.Count;
    for (int i = 0; i < m_triangles.Count; i += 6, tile++)
    {
      // We expect the quad to be layed out in a very particular way. Note that
      // these coordinates refer to the quad corners when looking down onto it.
      // Not to be confused with top or bottom surfaces!
      Vector3 topLeft = vertices[m_triangles[i + 0]];
      Vector3 topRight = vertices[m_triangles[i + 1]];
      Vector3 bottomRight = vertices[m_triangles[i + 2]];
      Vector3 bottomLeft = vertices[m_triangles[i + 4]];

      // Emit the sides
      if ((m_tiles[tile].neighbors & (byte)SelectionTile.Edge.Top) == 0)
      {
        Vector3 crownTopLeft = topRight + extrude;
        Vector3 crownTopRight = topLeft + extrude;
        Vector3 crownBottomRight = topLeft + baseExtrude;
        Vector3 crownBottomLeft = topRight + baseExtrude;
        EmitCappedSideQuad(vertices, vertIdx, triangles, triIdx, uv, crownUV, crownTopLeft, crownTopRight, crownBottomRight, crownBottomLeft);
        vertIdx += 4;
        triIdx += 6;

        Vector3 baseTopLeft = topRight + baseExtrude;
        Vector3 baseTopRight = topLeft + baseExtrude;
        Vector3 baseBottomRight = topLeft;
        Vector3 baseBottomLeft = topRight;
        EmitCappedSideQuad(vertices, vertIdx, triangles, triIdx, uv, baseUV, baseTopLeft, baseTopRight, baseBottomRight, baseBottomLeft);
        vertIdx += 4;
        triIdx += 6;
      }

      if ((m_tiles[tile].neighbors & (byte)SelectionTile.Edge.Right) == 0)
      {
        Vector3 crownTopLeft = bottomRight + extrude;
        Vector3 crownTopRight = topRight + extrude;
        Vector3 crownBottomRight = topRight + baseExtrude;
        Vector3 crownBottomLeft = bottomRight + baseExtrude;
        EmitCappedSideQuad(vertices, vertIdx, triangles, triIdx, uv, crownUV, crownTopLeft, crownTopRight, crownBottomRight, crownBottomLeft);
        vertIdx += 4;
        triIdx += 6;

        Vector3 baseTopLeft = bottomRight + baseExtrude;
        Vector3 baseTopRight = topRight + baseExtrude;
        Vector3 baseBottomRight = topRight;
        Vector3 baseBottomLeft = bottomRight;
        EmitCappedSideQuad(vertices, vertIdx, triangles, triIdx, uv, baseUV, baseTopLeft, baseTopRight, baseBottomRight, baseBottomLeft);
        vertIdx += 4;
        triIdx += 6;
      }

      if ((m_tiles[tile].neighbors & (byte)SelectionTile.Edge.Bottom) == 0)
      {
        Vector3 crownTopLeft = bottomLeft + extrude;
        Vector3 crownTopRight = bottomRight + extrude;
        Vector3 crownBottomRight = bottomRight + baseExtrude;
        Vector3 crownBottomLeft = bottomLeft + baseExtrude;
        EmitCappedSideQuad(vertices, vertIdx, triangles, triIdx, uv, crownUV, crownTopLeft, crownTopRight, crownBottomRight, crownBottomLeft);
        vertIdx += 4;
        triIdx += 6;

        Vector3 baseTopLeft = bottomLeft + baseExtrude;
        Vector3 baseTopRight = bottomRight + baseExtrude;
        Vector3 baseBottomRight = bottomRight;
        Vector3 baseBottomLeft = bottomLeft;
        EmitCappedSideQuad(vertices, vertIdx, triangles, triIdx, uv, baseUV, baseTopLeft, baseTopRight, baseBottomRight, baseBottomLeft);
        vertIdx += 4;
        triIdx += 6;
      }

      if ((m_tiles[tile].neighbors & (byte)SelectionTile.Edge.Left) == 0)
      {
        Vector3 crownTopLeft = topLeft + extrude;
        Vector3 crownTopRight = bottomLeft + extrude;
        Vector3 crownBottomRight = bottomLeft + baseExtrude;
        Vector3 crownBottomLeft = topLeft + baseExtrude;
        EmitCappedSideQuad(vertices, vertIdx, triangles, triIdx, uv, crownUV, crownTopLeft, crownTopRight, crownBottomRight, crownBottomLeft);
        vertIdx += 4;
        triIdx += 6;

        Vector3 baseTopLeft = topLeft + baseExtrude;
        Vector3 baseTopRight = bottomLeft + baseExtrude;
        Vector3 baseBottomRight = bottomLeft;
        Vector3 baseBottomLeft = topLeft;
        EmitCappedSideQuad(vertices, vertIdx, triangles, triIdx, uv, baseUV, baseTopLeft, baseTopRight, baseBottomRight, baseBottomLeft);
        vertIdx += 4;
        triIdx += 6;
      }
    }

    // Don't forget to extrude the original, top vertices
    for (int i = 0; i < m_vertices.Count; i++)
    {
      vertices[i].z += extrudeLengthCM;
    }
  }

  private void ExtrudeSimpleWithBottom(out Vector3[] vertices, out int[] triangles, out Vector2[] uv, float extrudeLength, Vector2[] topUV, Vector2[] sideUV, float sideRepeatDistanceCM, bool hasBottomSurface)
  {
    bool textured = topUV != null && sideUV != null;

    // Extrude length is specified in world units (meters)
    float extrudeLengthCM = 100 * extrudeLength;
    Vector3 extrude = new Vector3(0, 0, extrudeLengthCM);

    // Create mesh data arrays with enough headroom for inserted triangles
    int maxNumTriangles = m_triangles.Count + (m_triangles.Count / 6) * 4 * 6;  // top surface plus side walls
    maxNumTriangles += hasBottomSurface ? m_triangles.Count : 0;                // optional bottom surface
    vertices = new Vector3[(maxNumTriangles / 2) * 4];
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
    Vector2 sideUVTopLeft = sideUV[0] + new Vector2(0, extrudeLengthCM / sideRepeatDistanceCM - 1);
    Vector2 sideUVTopRight = sideUV[1] + new Vector2(0, extrudeLengthCM / sideRepeatDistanceCM - 1);
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

    // Because I'm lazy, I duplicate the original top surface vertices again
    // to create a bottom side. We *could* share the outer vertices but we
    // would have to generate interior ones anyway, so it doesn't save much.
    if (hasBottomSurface)
    {
      for (int i = 0; i < m_vertices.Count; i += 4)
      {
        for (int j = 0; j < 4; j++)
        {
          vertices[vertIdx + j] = vertices[i + j];
          uv[vertIdx + j] = topUV[j];
        }
        // Triangle 1 (reverse winding to be visible from bottom)
        triangles[triIdx++] = vertIdx + 2;
        triangles[triIdx++] = vertIdx + 1;
        triangles[triIdx++] = vertIdx + 0;
        // Second
        triangles[triIdx++] = vertIdx + 0;
        triangles[triIdx++] = vertIdx + 3;
        triangles[triIdx++] = vertIdx + 2;
        vertIdx += 4;
      }
    }

    // Don't forget to extrude the original, top vertices
    for (int i = 0; i < m_vertices.Count; i++)
    {
      vertices[i].z += extrudeLengthCM;
    }
  }

  /*
   * Extrudes columns from each tile, creating a single quad (two triangles)
   * for each side wall. Interior side walls (i.e., edges where tiles are 
   * neighbors) are not created. Works for both textured and untextured cases.
   * 
   * When textured, shared vertices cannot be used and the selected vertices
   * passed into the constructor are assumed to be unique (4 vertices present
   * per tile). An additional 4 vertices are created for each side wall. A
   * repeat distance is specified for the side wall texture.
   * 
   * In the untextured case, vertices can be shared. Only two new vertices are
   * created for each tile side wall edge, and they are connected to the top,
   * so-called "extruded" vertices.
   * 
   * If a bottom surface is requested, all vertices from the top surfaces are
   * simply duplicated.
   */
  public void ExtrudeSimple(out Vector3[] vertices, out int[] triangles, out Vector2[] uv, float extrudeLength, Vector2[] topUV, Vector2[] sideUV, float sideRepeatDistanceCM)
  {
    ExtrudeSimpleWithBottom(out vertices, out triangles, out uv, extrudeLength, topUV, sideUV, sideRepeatDistanceCM, false);
  }

  public void ExtrudeSimpleWithBottom(out Vector3[] vertices, out int[] triangles, out Vector2[] uv, float extrudeLength, Vector2[] topUV, Vector2[] sideUV, float sideRepeatDistanceCM)
  {
    ExtrudeSimpleWithBottom(out vertices, out triangles, out uv, extrudeLength, topUV, sideUV, sideRepeatDistanceCM, true);
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
