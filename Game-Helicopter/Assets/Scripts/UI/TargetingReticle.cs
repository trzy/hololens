using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;

[RequireComponent(typeof(Billboard))]
public class TargetingReticle : MonoBehaviour
{
  public Material material;
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
      {
        m_billboard.enabled = true;
        m_defaultReticle.SetActive(false);
        m_lockedReticle.SetActive(true);
      }
      else if (m_lockedOn && !value)
      {
        m_defaultReticle.SetActive(true);
        m_lockedReticle.SetActive(false);
        m_billboard.enabled = false;
        transform.localRotation = Quaternion.identity;  // billboard script modified rotation
      }
      m_lockedOn = value;
    }
  }

  private MeshRenderer m_renderer;
  private Mesh m_mesh;
  private GameObject m_defaultReticle;
  private GameObject m_lockedReticle;
  private Billboard m_billboard;
  private bool m_lockedOn = false;

  private void GenerateReticles(float radius, float thickness)
  {
    Color32 unusedColor = new Color32();
    Mesh mesh = null;
    List<Vector3> verts = new List<Vector3>();
    List<int> triangles = new List<int>();

    m_defaultReticle = new GameObject("DefaultReticle");
    m_defaultReticle.SetActive(true);
    m_defaultReticle.transform.parent = transform;
    m_defaultReticle.transform.localPosition = Vector3.zero;
    m_defaultReticle.transform.localRotation = Quaternion.identity;
    m_defaultReticle.AddComponent<MeshRenderer>().sharedMaterial = material;
    mesh = m_defaultReticle.AddComponent<MeshFilter>().mesh;
    verts.Clear();
    triangles.Clear();
    float innerRadius = radius - 0.5f * thickness;
    float outerRadius = radius + 0.5f * thickness;
    int segments = 8;
    ProceduralMeshUtils.DrawArc(verts, triangles, null, unusedColor, unusedColor, innerRadius, outerRadius, 0, 360, segments);
    mesh.vertices = verts.ToArray();
    mesh.triangles = triangles.ToArray();
    mesh.RecalculateBounds();  // absolutely needed because Unity may erroneously think we are off-screen at times

    m_lockedReticle = new GameObject("LockedReticle");
    m_lockedReticle.SetActive(false);
    m_lockedReticle.transform.parent = transform;
    m_lockedReticle.transform.localPosition = Vector3.zero;
    m_lockedReticle.transform.localRotation = Quaternion.identity;
    m_lockedReticle.AddComponent<MeshRenderer>().sharedMaterial = material;
    mesh = m_lockedReticle.AddComponent<MeshFilter>().mesh;
    verts.Clear();
    triangles.Clear();
    ProceduralMeshUtils.DrawQuad(verts, triangles, null, unusedColor, +1 * Vector3.up * radius, thickness, radius);
    ProceduralMeshUtils.DrawQuad(verts, triangles, null, unusedColor, -1 * Vector3.up * radius, thickness, radius);
    ProceduralMeshUtils.DrawQuad(verts, triangles, null, unusedColor, +1 * Vector3.right * radius, radius, thickness);
    ProceduralMeshUtils.DrawQuad(verts, triangles, null, unusedColor, -1 * Vector3.right * radius, radius, thickness);
    mesh.vertices = verts.ToArray();
    mesh.triangles = triangles.ToArray();
    mesh.RecalculateBounds();
  }

  private void Awake()
  {
    float thickness = .0025f;
    float radius = 0.02f;
    GenerateReticles(radius, thickness);
    m_billboard = GetComponent<Billboard>();
  }
}
