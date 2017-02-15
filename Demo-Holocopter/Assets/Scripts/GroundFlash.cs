using UnityEngine;
using System.Collections;

public class GroundFlash: MonoBehaviour
{
  [Tooltip("Time in seconds to reach scale factor specified in editor.")]
  public float expandTime = 0.01f;

  [Tooltip("Time in seconds to shrink back down to 0 after reaching maximum size.")]
  public float contractTime = 0.01f;

  private Vector3 m_maxScale;
  private float m_t0 = 0;

  void Awake()
  {
  }

  void Start()
  {
    m_t0 = Time.time;
    m_maxScale = transform.localScale; // use local scale from editor as max size
    transform.localScale = Vector3.zero;
  }
	
	void Update()
  {
    float delta = Time.time - m_t0;
    if (delta >= expandTime + contractTime)
      Destroy(this.gameObject);
    if (delta < expandTime)
      transform.localScale = Mathf.Lerp(0, 1, delta / expandTime) * m_maxScale;
    else
      transform.localScale = Mathf.Max(0, Mathf.Lerp(1, 0, (delta - expandTime) / contractTime)) * m_maxScale;
  }
}
