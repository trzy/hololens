using UnityEngine;
using System.Collections;

public class BulletImpactSpray: MonoBehaviour
{
  [Tooltip("Duration in seconds of effect")]
  public float m_duration = 0.16f;

  [Tooltip("Peak height (y scale factor) to grow to")]
  public float peakHeight = 5;

  [Tooltip("Number of revolutions to complete")]
  public float m_revolutions = 0;

  [Tooltip("Texture map")]
  public Material m_material;

  /*
   * In xy plane:
   *
   *        8   9
   *        |   |
   *        0   1
   *
   * 15 - 7       2 - 10
   *    
   * 14 - 6       3 - 11
   *    
   *        5   4 
   *        |   |
   *        13  12
   *
   * Outer octagon initially flat in xy plane and above inner/base octagon
   * along z axis in final position.
   */

  private const float INVSQRT2 = .7071067811865475f;  // 1 / sqrt(2)
  private const float THICKNESS = 1f;

  private Vector2[] m_uv =
  {
    new Vector2(0, 0),    // 0
    new Vector2(1, 0),    // 1
    new Vector2(0, 0),    // 2
    new Vector2(1, 0),    // 3
    new Vector2(0, 0),    // 4
    new Vector2(1, 0),    // 5
    new Vector2(0, 0),    // 6
    new Vector2(1, 0),    // 7
    new Vector2(0, 1),    // 8
    new Vector2(1, 1),    // 9
    new Vector2(0, 1),    // 10
    new Vector2(1, 1),    // 11
    new Vector2(0, 1),    // 12
    new Vector2(1, 1),    // 13
    new Vector2(0, 1),    // 14
    new Vector2(1, 1)     // 15
  };

  private Vector3[] m_flatVerts =
  {
    new Vector3(-0.5f,                        0.5f + INVSQRT2,              0), // 0
    new Vector3(0.5f,                         0.5f + INVSQRT2,              0), // 1
    new Vector3(0.5f + INVSQRT2,              0.5f,                         0), // 2
    new Vector3(0.5f + INVSQRT2,              -0.5f,                        0), // 3
    new Vector3(0.5f,                         -0.5f - INVSQRT2,             0), // 4
    new Vector3(-0.5f,                        -0.5f - INVSQRT2,             0), // 5
    new Vector3(-0.5f - INVSQRT2,             -0.5f,                        0), // 6
    new Vector3(-0.5f - INVSQRT2,             0.5f,                         0), // 7
    new Vector3(-0.5f,                        0.5f + INVSQRT2 + THICKNESS,  0), // 8
    new Vector3(0.5f,                         0.5f + INVSQRT2 + THICKNESS,  0), // 9
    new Vector3(0.5f + INVSQRT2 + THICKNESS,  0.5f,                         0), // 10
    new Vector3(0.5f + INVSQRT2 + THICKNESS,  -0.5f,                        0), // 11
    new Vector3(0.5f,                         -0.5f - INVSQRT2 - THICKNESS, 0), // 12
    new Vector3(-0.5f,                        -0.5f - INVSQRT2 - THICKNESS, 0), // 13
    new Vector3(-0.5f - INVSQRT2 - THICKNESS, -0.5f,                        0), // 14
    new Vector3(-0.5f - INVSQRT2 - THICKNESS, 0.5f,                         0), // 15
  };

  private Vector3[] m_standingVerts =
  {
    new Vector3(-0.5f,            0.5f + INVSQRT2,  0),         // 0
    new Vector3(0.5f,             0.5f + INVSQRT2,  0),         // 1
    new Vector3(0.5f + INVSQRT2,  0.5f,             0),         // 2
    new Vector3(0.5f + INVSQRT2,  -0.5f,            0),         // 3
    new Vector3(0.5f,             -0.5f - INVSQRT2, 0),         // 4
    new Vector3(-0.5f,            -0.5f - INVSQRT2, 0),         // 5
    new Vector3(-0.5f - INVSQRT2, -0.5f,            0),         // 6
    new Vector3(-0.5f - INVSQRT2, 0.5f,             0),         // 7
    new Vector3(-0.5f,            0.5f + INVSQRT2,  THICKNESS), // 8
    new Vector3(0.5f,             0.5f + INVSQRT2,  THICKNESS), // 9
    new Vector3(0.5f + INVSQRT2,  0.5f,             THICKNESS), // 10
    new Vector3(0.5f + INVSQRT2,  -0.5f,            THICKNESS), // 11
    new Vector3(0.5f,             -0.5f - INVSQRT2, THICKNESS), // 12
    new Vector3(-0.5f,            -0.5f - INVSQRT2, THICKNESS), // 13
    new Vector3(-0.5f - INVSQRT2, -0.5f,            THICKNESS), // 14
    new Vector3(-0.5f - INVSQRT2, 0.5f,             THICKNESS)  // 15
  };

  // Triangles
  private int[] m_triangles =
  {
    // Interior: facing up/inward (when flat/standing)
    0, 8, 1,
    8, 9, 1,
    1, 9, 2,
    9, 10, 2,
    2, 10, 3,
    10, 11, 3,
    3, 11, 4,
    11, 12, 4,
    4, 12, 5,
    12, 13, 5,
    5, 13, 6,
    13, 14, 6,
    6, 14, 7,
    14, 15, 7,
    7, 15, 0,
    15, 8, 0,
    // Exterior: facing down/outward (when flat/standing)
    1, 8, 0,
    1, 9, 8,
    2, 9, 1,
    2, 10, 9,
    3, 10, 2,
    3, 11, 10,
    4, 11, 3,
    4, 12, 11,
    5, 12, 4,
    5, 13, 12,
    6, 13, 5,
    6, 14, 13,
    7, 14, 6,
    7, 15, 14,
    0, 15, 7,
    0, 8, 15
  };

  // Vertex buffer used by mesh
  private Vector3[] m_verts = new Vector3[16];

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
    Interpolate(m_verts, m_flatVerts, m_standingVerts, 1);
    m_mesh = GetComponent<MeshFilter>().mesh;
    m_mesh.vertices = m_verts;
    m_mesh.triangles = m_triangles;
    m_mesh.uv = m_uv;
    //TODO: normals?
    GetComponent<MeshRenderer>().material = m_material;
  }

  void Start()
  {
    m_scale = transform.localScale;
    m_t0 = Time.time;
  }

	void Update()
  {
    float delta = Time.time - m_t0;
    if (delta > m_duration)
    {
      Destroy(this.gameObject);
      //delta = 0;
      //m_t0 = Time.time;
    }
    Interpolate(m_verts, m_flatVerts, m_standingVerts, Mathf.Min(delta / m_duration, 1));
    m_mesh.vertices = m_verts;
    float xyScale = Mathf.Lerp(1, 0, Mathf.Min(delta / m_duration, 1));
    float zScale = Mathf.Lerp(1, peakHeight, Mathf.Min(delta / m_duration, 1));
    Vector3 scale = new Vector3(m_scale.x * xyScale, m_scale.y * xyScale, m_scale.z * zScale);
    transform.localScale = scale;
    transform.Rotate(new Vector3(0, 0, m_revolutions * 360 * delta / m_duration));
	}
}
