using UnityEngine;
using System.Collections;

public class GroundFlash: MonoBehaviour
{
  [Tooltip("Time in seconds to reach scale factor specified in editor.")]
  public float m_expand_time = 0.01f;

  [Tooltip("Time in seconds to shrink back down to 0 after reaching maximum size.")]
  public float m_contract_time = 0.01f;

  private Vector3 m_max_scale;
  private float m_t0 = 0;

  void Awake()
  {
    m_max_scale = transform.localScale; // use local scale from editor as max size
    transform.localScale = Vector3.zero;
  }

  void Start()
  {
    m_t0 = Time.time;
	}
	
	void Update()
  {
    float delta = Time.time - m_t0;
    if (delta >= m_expand_time + m_contract_time)
      Destroy(this.gameObject);
    if (delta < m_expand_time)
      transform.localScale = Mathf.Lerp(0, 1, delta / m_expand_time) * m_max_scale;
    else
      transform.localScale = Mathf.Max(0, Mathf.Lerp(1, 0, (delta - m_expand_time) / m_contract_time)) * m_max_scale;
  }
}
