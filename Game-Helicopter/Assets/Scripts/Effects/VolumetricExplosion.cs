using UnityEngine;

public class VolumetricExplosion : MonoBehaviour
{
  public enum TweenFunction
  {
    ExponentialEaseOut,
    CircularEaseOut,
    Linear
  }

  public TweenFunction expandTweenFunction = TweenFunction.ExponentialEaseOut;
  public float expandDuration = 0.5f;
  public float fadeDuration = 0.5f;
  public float loopDuration = 5;
  public float blastDisplacement = 0.25f;
  public float smokeDisplacement = 0.1f;

  private Material m_material;
  private Vector3 m_finalSize;
  private Vector3 m_finalGlowSize;
  private float m_startTime;

  private float ExponentialEaseOut(float from, float to, float t, float alpha = -3)
  {
    return from + (to - from) * (Mathf.Exp(alpha * t) - 1) / (Mathf.Exp(alpha) - 1);
  }

  private float CircularEaseOut(float from, float to, float t)
  {
    float t1 = Mathf.Clamp(t, 0, 1) - 1;  // shift curve right by 1
    return from + (to - from) * Mathf.Sqrt(1 - t1 * t1);
  }

  // Animate the displacement
  private void Animate()
  {
    float r = Mathf.Sin((Time.time / loopDuration) * (2 * Mathf.PI)) * 0.5f + 0.25f;
    float g = Mathf.Sin((Time.time / loopDuration + 0.33333333f) * 2 * Mathf.PI) * 0.5f + 0.25f;
    float b = Mathf.Sin((Time.time / loopDuration + 0.66666667f) * 2 * Mathf.PI) * 0.5f + 0.25f;
    float correction = 1 / (r + g + b);
    r *= correction;
    g *= correction;
    b *= correction;
    m_material.SetVector("_ChannelFactor", new Vector4(r, g, b, 0));
  }

  // During fade-out, shift range of colors to use in ramp texture toward smoke
  private void ColorFade(float t)
  {
    m_material.SetVector("_Range", new Vector4(t, Mathf.Min(1, t + 0.5f), 0, 0));
  }

  // During fade-out, displacement is ramped to a different value (a smaller
  // displacement will lessen the oscillations, which looks better)
  private void DisplacementFade(float t)
  {
    if (t <= 0)
      return;
    m_material.SetFloat("_Displacement", Mathf.Lerp(blastDisplacement, smokeDisplacement, t));
  }

  // Grow the sphere during the initial expansion phase
  private void Expand(float t)
  {
    float scale = 1;
    switch (expandTweenFunction)
    {
      default:
        break;
      case TweenFunction.ExponentialEaseOut:
        scale = ExponentialEaseOut(0, 1, t);
        break;
      case TweenFunction.CircularEaseOut:
        scale = CircularEaseOut(0, 1, t);
        break;
      case TweenFunction.Linear:
        scale = Mathf.Lerp(0, 1, t);
        break;
    }
    transform.localScale = m_finalSize * scale;
  }

  private void Update()
  {
    // t1 = [0,1] during explosion expansion phase
    // t2 = [0,1] during explosion fade-out phase, for explosion
    float t1 = Mathf.Min(1.0f, (Time.time - m_startTime) / expandDuration);
    float t2 = Mathf.Max(0, (Time.time - m_startTime - expandDuration) / fadeDuration);
    Animate();
    ColorFade(t2);
    DisplacementFade(t2);
    Expand(t1);
    if (t2 >= 1)
      gameObject.SetActive(false);
  }

  private void OnEnable()
  {
    m_material = GetComponent<Renderer>().material;
    m_material.SetFloat("_Displacement", blastDisplacement);
    m_finalSize = transform.localScale;
    m_startTime = Time.time;
    Debug.Log("OnEnable => m_finalSize=" + m_finalSize);
  }

  private void Start()
  {
  }
}
  
