using UnityEngine;
using System.Collections;

public class ExplosionBillboard: MonoBehaviour
{
  private const float FRAME_DURATION = 1f / 60f;  // duration in seconds of each explosion frame
  private const float U_STEP = .25f;              // one step right in the texture sheet
  private const float V_STEP = -.0625f;           // one step down the texture sheet
  private float[] m_u_steps =
  {
    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
    2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
    3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3
  };
  private float[] m_v_steps =
  {
    5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
    0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
    0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
    0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14
  };
  private float m_t0 = 0;
  private Material m_material;
  private int m_u_offset = 0;
  private int m_v_offset = 0;

  void Awake()
  {
    m_material = GetComponent<Renderer>().material;
    m_u_offset = Shader.PropertyToID("_UOffset");
    m_v_offset = Shader.PropertyToID("_VOffset");
    this.gameObject.SetActive(false); // we require explicit activation
    m_material.SetFloat(m_u_offset, m_u_steps[0]);
    m_material.SetFloat(m_v_offset, m_v_steps[0]);
  }

  void Start()
  {
    // Will be called after first OnEnable() (itself triggered by SetActive())
    m_t0 = Time.time;
  }

  void Update()
  {
    transform.forward = -Camera.main.transform.forward;
    int num_steps = m_u_steps.Length;
    int frame = (int) ((Time.time - m_t0) / FRAME_DURATION);
    if (frame >= num_steps)
    {
      Destroy(this.gameObject);
      return;
    }
    m_material.SetFloat(m_u_offset, U_STEP * m_u_steps[frame]);
    m_material.SetFloat(m_v_offset, V_STEP * m_v_steps[frame]);
  }
}
