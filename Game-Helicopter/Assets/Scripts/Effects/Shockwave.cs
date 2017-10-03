using UnityEngine;

public class Shockwave: MonoBehaviour
{
  public float expansionDuration = 0.5f;
  public float fadeOutStartTime = 0.5f;
  public float fadeOutDuration = 0.5f;

  private Material m_material;
  private Vector3 m_finalScale;
  private Color m_startColor;
  private float m_startTime;

  private void Update()
  {
    float t1 = Mathf.Min(1, (Time.time - m_startTime) / expansionDuration);
    float t2 = Mathf.Max(0, (Time.time - m_startTime - fadeOutStartTime) / fadeOutDuration);
    transform.localScale = m_finalScale * t1;
    m_material.SetColor("_TintColor", new Color(m_startColor.r, m_startColor.g, m_startColor.b, m_startColor.a * (1 - t2)));
    if (t1 >= 1 && t2 >= 1)
      gameObject.SetActive(false);
  }

  private void OnEnable()
  {
    m_material = GetComponent<Renderer>().material;
    m_finalScale = transform.localScale;
    m_startColor = m_material.GetColor("_TintColor");
    m_startTime = Time.time;
  }

  private void Start()
  {
    OnEnable();
  }
}