/*
 * TODO:
 * -----
 * - Reticle should behave like a 3D cursor. Display 1m out unless intersecting
 *   with some geometry, in which case it should be positioned oriented coplanar 
 *   with geometry.
 * - Remember to use gun position, not helicopter position, which is slightly 
 *   vertically offset (need to modify AutoAim for this, too).
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TargetingReticle: MonoBehaviour
{
  public Material material = null;
  public float zDistance = 2f;

  private MeshRenderer m_renderer = null;
  private Mesh m_mesh = null;

  private void GenerateReticle(float radius, float thickness)
  {
    //float thickness = .02f;
    //float thickness = .0025f; // looks great on actual device at z=2m
    //thickness = .01f;
    float innerRadius = radius - 0.5f * thickness;
    float outerRadius = radius + 0.5f * thickness;
    int segments = 10;
    List<Vector3> verts = new List<Vector3>();
    List<int> triangles = new List<int>();
    List<Color32> colors = new List<Color32>();
    Color32 black = new Color32(0xff, 0, 0, 0xc0);
    //Color32 red = new Color32(0xff, 0, 0, 0xc0);
    Color32 blue = new Color32(101, 215, 252, 0xc0);
    Color32 orange = new Color32(0xff, 0x8c, 0, 0xff);
    Color32 gold = new Color32(0xff, 0xd7, 0x00, 0xff);

    Color32 red = new Color32(244, 66, 66, 0xe0);

    Color32 startColor = red;
    Color32 endColor = red;

    // Outer circle
    ProceduralMeshUtils.DrawArc(verts, triangles, colors, startColor, endColor, innerRadius, outerRadius, 0, 360, segments);
    // Inner dot
    //ProceduralMeshUtils.DrawCircle(...);

    m_mesh.vertices = verts.ToArray();
    m_mesh.triangles = triangles.ToArray();
    m_mesh.colors32 = colors.ToArray();
    m_mesh.RecalculateBounds();  // absolutely needed because Unity may erroneously think we are off-screen at times
  }

  private void OnDestroy()
  {
    //TODO: clean up material? (what about mesh?)
  }

  private void Update()
  {
    //m_renderer.enabled = GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(Camera.main), targetObject.GetComponent<BoxCollider>().bounds);
    //transform.position = targetObject.ComputeCameraSpaceCentroidAt(zDistance);
    //transform.rotation = Camera.main.transform.rotation;
    //Debug.Log("Distance = " + Vector3.Magnitude(transform.position - Camera.main.transform.position) + ", center=" + pbb.center);
  }

  private void Awake()
  {
    m_renderer = GetComponent<MeshRenderer>();
    m_mesh = GetComponent<MeshFilter>().mesh;
    float thickness = .0025f;
    float radius = 0.02f;
    GenerateReticle(radius, thickness);
  }
}
