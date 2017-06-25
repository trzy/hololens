using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class CircularOscillator: MonoBehaviour
{
  [Tooltip("Radius of the circular path. Should match sine wave amplitude.")]
  public float radius = 1;

  [Tooltip("Frequency of oscillation in Hz.")]
  public float frequency = 1;

  [Tooltip("Pauses updates.")]
  public bool paused = false;

  [Tooltip("Sine wave to connect to. Will modify the sine wave's transform.")]
  public SineRenderer sineWave = null;

  private LineRenderer m_line;
  private float m_angle = 0;

  // LateUpdate to ensure SineRenderer is updated first
  private void LateUpdate()
  {
    if (paused)
      return;

    // Update circular arc and adjust its horizontal position so that the end
    // of the line is always at x = 0 when a sine wave is attached
    m_angle += Time.deltaTime * frequency * 360;
    float radians = m_angle * Mathf.Deg2Rad;
    float x = radius * Mathf.Cos(radians);
    float y = radius * Mathf.Sin(radians);
    float adjustment = sineWave == null ? 0 : -x;
    Vector3[] points = new Vector3[2] { new Vector3(adjustment, 0, 0), new Vector3(x + adjustment, y, 0) };
    m_line.SetPositions(points);

    // Adjust sine wave position so that the latest time, t, is plotted at
    // circular oscillator's center position. Only the x position needs to be
    // modified.
    if (sineWave)
    {
      float xOffset = transform.position.x - sineWave.lastPosition.x;
      Vector3 position = sineWave.transform.position;
      position.x = xOffset;
      sineWave.transform.position = position;
    }
  }

  private void InitLineRenderer()
  {
    m_line = GetComponent<LineRenderer>();
    m_line.startWidth = .01f;
    m_line.endWidth = .01f;
    m_line.startColor = Color.red;
    m_line.endColor = Color.red;
    m_line.positionCount = 2;
    m_line.useWorldSpace = false;
  }

  private void Start()
  {
    InitLineRenderer();
  }

}
