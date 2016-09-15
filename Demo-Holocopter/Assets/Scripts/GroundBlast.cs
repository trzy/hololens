using UnityEngine;
using System.Collections;

public class GroundBlast: MonoBehaviour
{
  /*
    * In xz plane:
    * 
    *        4
    *        |
    *        0
    *
    *  7 - 3   1 - 5
    *  
    *        2
    *        |
    *        6
    */

  // Petals flat
  private Vector3[] m_flat_verts =
  {
    new Vector3(0, 0, 1),   // 0
    new Vector3(1, 0, 0),   // 1
    new Vector3(0, 0, -1),  // 2
    new Vector3(-1, 0, 0),  // 3
    new Vector3(0, 0, 2),   // 4
    new Vector3(2, 0, 0),   // 5
    new Vector3(0, 0, -2),  // 6
    new Vector3(-2, 0, 0)   // 7
  };

  // Petals standing up
  private Vector3[] m_standing_verts =
  {
    new Vector3(0, 0, 1),   // 0
    new Vector3(1, 0, 0),   // 1
    new Vector3(0, 0, -1),  // 2
    new Vector3(-1, 0, 0),  // 3
    new Vector3(0, 1, 1),   // 4
    new Vector3(1, 1, 0),   // 5
    new Vector3(0, 1, -1),  // 6
    new Vector3(-1, 1, 0)   // 7
  };

  // Vertex buffer used by mesh
  private Vector3[] m_verts = new Vector3[8];

  // Triangles
  private int[] m_triangles =
  {
    // Interior: facing up/inward (when flat/standing)
    0, 4, 1,
    4, 5, 1,
    5, 6, 1,
    2, 1, 6,
    3, 2, 6,
    7, 3, 6,
    4, 0, 3,
    4, 3, 7,
    // Exterior: facing down/outward (when flat/standing)
    1, 4, 0,
    1, 5, 4,
    1, 6, 5,
    6, 1, 2,
    6, 2, 3,
    6, 3, 7,
    3, 0, 4,
    7, 3, 4
  };

  private Mesh m_mesh;
  private Vector3 m_scale;
  private float m_t0 = 0;

  private void Interpolate(Vector3[] result, Vector3[] initial, Vector3[] final, float alpha)
  {
    for (int i = 0; i < result.Length; i++)
    {
      result[i].x = Mathf.Lerp(initial[i].x, final[i].x, alpha);
      result[i].y = Mathf.Lerp(initial[i].y, final[i].y, alpha);
      result[i].z = Mathf.Lerp(initial[i].z, final[i].z, alpha);
    }
  }

  void Awake()
  {
    m_scale = transform.localScale;
    Interpolate(m_verts, m_flat_verts, m_standing_verts, 1);
    m_mesh = GetComponent<MeshFilter>().mesh;
    m_mesh.vertices = m_verts;
    m_mesh.triangles = m_triangles;
    //TODO: normals?
    GetComponent<MeshRenderer>().material.color = new Color(128f, 100f, 0f);
  }

  void Start()
  {
    m_t0 = Time.time;
  }

	void Update()
  {
    float duration = .15f;
    float delta = Time.time - m_t0;
    if (delta > duration)
    {
      delta = 0;
      m_t0 = Time.time;
    }
    Interpolate(m_verts, m_flat_verts, m_standing_verts, Mathf.Min(delta / duration, 1));
    m_mesh.vertices = m_verts;
    float scale = Mathf.Lerp(1, 0, Mathf.Min(delta / duration, 1));
    float hscale = Mathf.Lerp(1, 5, Mathf.Min(delta / duration, 1));
    m_scale.x = scale;
    m_scale.z = scale;
    m_scale.y = hscale;
    transform.localScale = m_scale;
	}
}
