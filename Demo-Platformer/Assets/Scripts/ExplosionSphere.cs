using UnityEngine;
using System.Collections;

public class ExplosionSphere : MonoBehaviour
{
  [Tooltip("If non-zero, object's render queue value is offset from the shader's default. Higher values are rendered later. Use this (sparingly) to enforce draw order.")]
  public int renderQueueOffset = 0;

  [Tooltip("Time in seconds to delay appearance.")]
  public float delayTime = 0;

  [Tooltip("Time in seconds over which to ramp to maximum size using S-curve.")]
  public float rampUpTime = 0.4f;

  [Tooltip("Time over which to fade out (and be destroyed) after ramping to maximum size.")]
  public float fadeOutTime = 0.3f;

  [Tooltip("Texture scroll speed, expressed as number of seconds to loop over entire V range.")]
  public float textureScrollTime = 5;

  private Vector3 m_maxScale;
  private Renderer m_renderer;
  private int m_vOffset;
  private float m_t0 = 0;

	void Awake()
  {
    //Debug.Log("Awake");
    m_maxScale = transform.localScale; // use local scale from editor as max size
    Debug.Log("maxScale = " + m_maxScale);
    transform.localScale = Vector3.zero;
    m_renderer = GetComponent<Renderer>();
    m_vOffset = Shader.PropertyToID("_VOffset");
    if (renderQueueOffset != 0)
      m_renderer.material.renderQueue = m_renderer.material.shader.renderQueue + renderQueueOffset;
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
    // Wait until after delay period to begin
    float delta = Time.time - m_t0;
    if (delta < delayTime)
      return;
    delta -= delayTime;
    if (!m_renderer.enabled)
      m_renderer.enabled = true;

    // Animate
    if (delta > rampUpTime + fadeOutTime)
      Destroy(this.gameObject);
    float size = Sigmoid1(delta / rampUpTime);
    transform.localScale = size * m_maxScale;
    Color color = new Color(m_renderer.material.color.r, m_renderer.material.color.g, m_renderer.material.color.b, m_renderer.material.color.a);
    color.a *= Mathf.Clamp(1 - (delta - rampUpTime) / fadeOutTime, 0, 1);
    m_renderer.material.color = color;
    m_renderer.material.SetFloat(m_vOffset, delta / textureScrollTime);
  }
}
