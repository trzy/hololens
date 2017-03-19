using UnityEngine;

public class FloatingPlatform: MonoBehaviour
{
  [Tooltip("Distance traveled by the platform")]
  public float peakToPeakAmplitude = 2;

  [Tooltip("Time taken to cover the distance in seconds")]
  public float period = 10;

  private float m_amplitude;
  private Vector3 m_centerPoint;
  private float m_t0;

  private void Update()
  {
    // Platform surface normal is always +z (forward) axis
    transform.position = m_centerPoint + m_amplitude * Mathf.Sin(2 * Mathf.PI * (Time.time - m_t0) / period - 0.5f * Mathf.PI) * transform.forward;
  }

  private void Start()
  {
    m_amplitude = 0.5f * peakToPeakAmplitude;
    m_centerPoint = transform.position + transform.forward * m_amplitude;
    m_t0 = Time.time;
  }

}