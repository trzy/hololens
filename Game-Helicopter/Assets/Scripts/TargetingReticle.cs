using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;

[RequireComponent(typeof(Billboard))]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class TargetingReticle : MonoBehaviour
{
  public Color32 color;
  public float thickness = .0025f;
  public float radius = .02f;

  public bool LockedOn
  {
    get
    {
      return m_lockedOn;
    }

    set
    {
      if (!m_lockedOn && value)
        m_billboard.enabled = true;
      else if (m_lockedOn && !value)
        m_billboard.enabled = false;
      m_lockedOn = value;
    }
  }

  private MeshRenderer m_renderer;
  private Mesh m_mesh;
  private Billboard m_billboard;
  private bool m_lockedOn = false;

  private void GenerateReticle(float radius, float thickness)
  {
    float innerRadius = radius - 0.5f * thickness;
    float outerRadius = radius + 0.5f * thickness;
    int segments = 8;
    List<Vector3> verts = new List<Vector3>();
    List<int> triangles = new List<int>();
    ProceduralMeshUtils.DrawArc(verts, triangles, null, color, color, innerRadius, outerRadius, 0, 360, segments);
    m_mesh.vertices = verts.ToArray();
    m_mesh.triangles = triangles.ToArray();
    //m_mesh.colors32 = colors.ToArray();
    m_mesh.RecalculateBounds();  // absolutely needed because Unity may erroneously think we are off-screen at times
  }

  private void Awake()
  {
    m_renderer = GetComponent<MeshRenderer>();
    m_mesh = GetComponent<MeshFilter>().mesh;
    float thickness = .0025f;
    float radius = 0.02f;
    GenerateReticle(radius, thickness);
    m_billboard = GetComponent<Billboard>();
  }
}
