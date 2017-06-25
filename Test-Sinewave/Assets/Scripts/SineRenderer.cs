using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class SineRenderer: MonoBehaviour
{
  public enum Mode
  {
    Unbounded,
    Bounded
  }

  [Tooltip("Whether to grow without limit or fixed to a maximum length. This setting is only parsed at start-up.")]
  public Mode mode = Mode.Unbounded;

  [Tooltip("In bounded mode, how many periods to plot before removing old points.")]
  public int boundedPeriods = 1;

  [Tooltip("Amplitude of the wave.")]
  public float amplitude = 1;

  [Tooltip("Frequency in Hz.")]
  public float frequency = 1;

  [Tooltip("Phase shift in degrees.")]
  public float phase = 0;

  [Tooltip("Number of points to plot per period. If this is too low, peaks may not be plotted.")]
  public int pointsPerPeriod = 20;

  [Tooltip("Pauses updates.")]
  public bool paused = false;

  public Vector3 lastPosition
  {
    get
    {
      // World space
      return transform.localToWorldMatrix * new Vector3(m_lastTimeSampled, Sin(m_lastTimeSampled), 0);
    }
  }

  private List<Vector3> m_points = new List<Vector3>();
  private Vector3[] m_boundedPoints;
  private int m_unboundedIdx;
  private LineRenderer m_line;
  private float m_lastTimeSampled = 0;

  private float Sin(float t)
  {
    return amplitude * Mathf.Sin(2 * Mathf.PI * frequency * t + phase * Mathf.Deg2Rad);
  }

  private void Update()
  {
    if (paused)
      return;

    float pointsPerSecond = pointsPerPeriod * frequency;
    float secondsPerPoint = 1f / pointsPerSecond;
    float t = m_lastTimeSampled + Time.deltaTime;

    if (mode == Mode.Bounded)
    {
      float lastTimePlotted = m_boundedPoints[(m_unboundedIdx - 1) % m_boundedPoints.Length].x;

      // Compute new points
      for (float ti = lastTimePlotted + secondsPerPoint; ti <= t; ti += secondsPerPoint)
      {
        int idx = m_unboundedIdx % m_boundedPoints.Length;
        m_boundedPoints[idx] = new Vector3(ti, Sin(ti), 0);
        m_unboundedIdx++;
      }

      // Draw the line
      if (m_unboundedIdx <= m_boundedPoints.Length)
      {
        // We haven't yet wrapped around the bounded, circular buffer
        m_line.positionCount = m_unboundedIdx;
        for (int i = 0; i < m_unboundedIdx; i++)
        {
          m_line.SetPosition(i, m_boundedPoints[i]);
        }
      }
      else
      {
        m_line.positionCount = m_boundedPoints.Length;
        int startIdx = m_unboundedIdx % m_boundedPoints.Length;
        int linePos = 0;
        for (int i = startIdx; i < m_boundedPoints.Length; i++)
        {
          m_line.SetPosition(linePos++, m_boundedPoints[i]);
        }
        for (int i = 0; i < startIdx; i++)
        {
          m_line.SetPosition(linePos++, m_boundedPoints[i]);
        }
      }
    }
    else
    {
      float lastTimePlotted = m_points[m_points.Count - 1].x;

      // Compute new points
      for (float ti = lastTimePlotted + secondsPerPoint; ti <= t; ti += secondsPerPoint)
      {
        m_points.Add(new Vector3(ti, Sin(ti), 0));
      }

      // Draw the line
      m_line.positionCount = m_points.Count;
      m_line.SetPositions(m_points.ToArray());
    }

    m_lastTimeSampled = t;
  }

  private void InitLineRenderer()
  {
    m_line = GetComponent<LineRenderer>();
    m_line.startWidth = .01f;
    m_line.endWidth = .01f;
    m_line.startColor = Color.red;
    m_line.endColor = Color.red;
    m_line.positionCount = 0;
    m_line.useWorldSpace = false;
  }

  private void Start()
  {
    m_points.Add(new Vector3(0, Sin(0), 0));
    if (mode == Mode.Bounded)
    {
      m_boundedPoints = new Vector3[pointsPerPeriod * boundedPeriods + 1];
      m_boundedPoints[0] = new Vector3(0, Sin(0), 0);
      m_unboundedIdx = 1;
    }
    InitLineRenderer();
  }
}
