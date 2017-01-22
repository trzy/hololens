using UnityEngine;

public class StepwiseAnimator
{
  public delegate float TimeScaleFunction(float t01);

  public float currentValue = 0;
  public float previousValue = 0;

  private float[] m_values = null;
  private float[] m_timeDeltas = null;
  private TimeScaleFunction[] m_timeScale = null;
  private int m_step = 0;
  private bool m_finished = true;
  private float m_tStart = 0;     // beginning of animation
  private float m_tSegmentStart;  // beginning of current animation segment

  public static float Sigmoid01(float x)
  {
    // f(x) = x / (1 + |x|), f(x): [-0.5, 0.5] for x: [-1, 1]
    // To get [0, 1] for [0, 1]: f'(x) = 0.5 + f(2 * (x - 0.5))
    float y = 2 * (x - 0.5f);
    float f = 0.5f + y / (1 + Mathf.Abs(y));
    return f;
  }

  public bool IsPlaying()
  {
    return !m_finished;
  }

  public void Update()
  {
    if (m_timeDeltas == null || m_finished)
    {
      return;
    }

    // Is it time to advance to next animation step?
    float deltaTSeg = Time.time - m_tSegmentStart;
    while (m_step < (m_timeDeltas.Length - 1) && deltaTSeg > m_timeDeltas[m_step])
    {
      m_tSegmentStart += m_timeDeltas[m_step];
      deltaTSeg -= m_timeDeltas[m_step];
      ++m_step;
    }

    // Interpolate between animation frames
    int i = m_step;
    float t = deltaTSeg / m_timeDeltas[i];
    float tScaled = (m_timeScale == null || m_timeScale[i] == null) ? t : m_timeScale[i](t);
    float value = Mathf.Lerp(m_values[i + 0], m_values[i + 1], tScaled);
    previousValue = currentValue;
    currentValue = value;

    // Finished?
    if (m_step == m_timeDeltas.Length - 1 && deltaTSeg >= m_timeDeltas[m_step])
    {
      m_finished = false;
    }
  }

  public void Reset()
  {
    m_step = 0;
    m_finished = false;
    m_tStart = Time.time;
    m_tSegmentStart = m_tStart;
    currentValue = 0;
    // Retain previous value
  }

  public void Reset(float[] values, float[] timeDeltas, TimeScaleFunction[] timeScale)
  {
    m_values = (float[])values.Clone();
    m_timeDeltas = (float[])timeDeltas.Clone();
    m_timeScale = timeScale != null ? (TimeScaleFunction[])timeScale.Clone() : null;
    m_step = 0;
    m_finished = false;
    m_tStart = Time.time;
    m_tSegmentStart = m_tStart;
    currentValue = 0;
    // Retain previous value
  }

  public StepwiseAnimator(float[] values, float[] timeDeltas, TimeScaleFunction[] timeScale)
  {
    Reset(values, timeDeltas, timeScale);
  }
}