using UnityEngine;
using System.Collections;

public class ExplosionBillboard: MonoBehaviour
{
  [Tooltip("Time in seconds to delay appearance.")]
  public float delayTime = 0;

  private const float FRAME_DURATION = 1f / 60f;  // duration in seconds of each explosion frame
  private const float U_STEP = .25f;              // one step right in the texture sheet
  private const float V_STEP = -.0625f;           // one step down the texture sheet
  private float[] m_uSteps =
  {
    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
    2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
    3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3
  };
  private float[] m_vSteps =
  {
    5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
    0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
    0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
    0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14
  };
  private float m_t0 = 0;
  private int m_uOffset = 0;
  private int m_vOffset = 0;
  private Renderer m_renderer;

  void Awake()
  {
    m_uOffset = Shader.PropertyToID("_UOffset");
    m_vOffset = Shader.PropertyToID("_VOffset");
    m_renderer = GetComponent<Renderer>();
    m_renderer.material.SetFloat(m_uOffset, m_uSteps[0]);
    m_renderer.material.SetFloat(m_vOffset, m_vSteps[0]);
    m_renderer.enabled = false;
  }

  void Start()
  {
    // Will be called after first OnEnable() (itself triggered by SetActive())
    m_t0 = Time.time;
  }

  void Update()
  {
    // Wait until after delay period to begin
    float delta = Time.time - m_t0;
    if (delta < delayTime)
      return;
    delta -= delayTime;
    if (!m_renderer.enabled)
      m_renderer.enabled = true;

    // Animate
    transform.forward = -Camera.main.transform.forward;
    int numSteps = m_uSteps.Length;
    int frame = (int) (delta / FRAME_DURATION);
    if (frame >= numSteps)
    {
      Destroy(this.gameObject);
      return;
    }
    m_renderer.material.SetFloat(m_uOffset, U_STEP * m_uSteps[frame]);
    m_renderer.material.SetFloat(m_vOffset, V_STEP * m_vSteps[frame]);
  }
}
