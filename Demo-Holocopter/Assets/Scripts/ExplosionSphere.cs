using UnityEngine;
using System.Collections;

public class ExplosionSphere : MonoBehaviour
{
  [Tooltip("If non-zero, object's render queue value is offset from the shader's default. Higher values are rendered later. Use this (sparingly) to enforce draw order.")]
  public int m_render_queue_offset = 0;

  [Tooltip("Time in seconds over which to ramp to maximum size using S-curve.")]
  public float m_ramp_up_time = 0.4f;

  [Tooltip("Time over which to fade out (and be destroyed) after ramping to maximum size.")]
  public float m_fade_out_time = 0.3f;

  [Tooltip("Texture scroll speed, expressed as number of seconds to loop over entire V range.")]
  public float m_texture_scroll_time = 5;

  private Vector3 m_max_scale;
  private Material m_material;
  private int m_v_offset;
  private float m_t0 = 0;

	void Awake()
  {
    Debug.Log("Awake");
    m_max_scale = transform.localScale; // use local scale from editor as max size
    transform.localScale = Vector3.zero;
    m_material = GetComponent<Renderer>().material;
    m_v_offset = Shader.PropertyToID("_VOffset");
    if (m_render_queue_offset != 0)
      m_material.renderQueue = m_material.shader.renderQueue + m_render_queue_offset;
    this.gameObject.SetActive(false); // we require explicit activation
  }

  void Start()
  {
    m_t0 = Time.time;
  }
	
	private float Sigmoid1(float t)
  {
    return 1 / (1 + Mathf.Exp(-4*(t-0.5f)));
  }

	void Update()
  {
    float delta = Time.time - m_t0;
    if (delta > m_ramp_up_time + m_fade_out_time)
      Destroy(this.gameObject);
    float size = Sigmoid1(delta / m_ramp_up_time);
    transform.localScale = size * m_max_scale;
    Color color = new Color(m_material.color.r, m_material.color.g, m_material.color.b, 1);
    color.a *= Mathf.Clamp(1 - (delta - m_ramp_up_time) / m_fade_out_time, 0, 1);
    m_material.color = color;
    m_material.SetFloat(m_v_offset, delta / m_texture_scroll_time);
  }
}
