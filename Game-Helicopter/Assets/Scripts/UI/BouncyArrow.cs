using System;
using UnityEngine;

public class BouncyArrow: MonoBehaviour
{
  public float bounceAmplitude = 0.25f;
  public float bounceFrequency = 2;
  public float numberOfBounces = 3;
  public float timeBetweenBounces = 2;
  public bool bounceOnEnable = true;

  private Vector3 m_restingPosition;
  private float m_nextBounceTime;

  private void ScheduleNextBounceAnimation(float now)
  {
    m_nextBounceTime = now + timeBetweenBounces;
  }

  private void Update()
  {
    //TODO: rather than oscillate about position, maybe should just always be a positive displacement
    //      (no negative values)

    float now = Time.time;
    float t = now - m_nextBounceTime;
    if (t < 0)
      return;

    float bouncePeriod = 1 / bounceFrequency;
    if (t >= numberOfBounces * bouncePeriod)
    {
      transform.localPosition = m_restingPosition;
      ScheduleNextBounceAnimation(now);
      return;
    }

    float bounceOffset = bounceAmplitude * Mathf.Sin(2 * Mathf.PI * t * bounceFrequency);
    transform.localPosition = m_restingPosition + Vector3.up * bounceOffset;
  }

  private void OnDisable()
  {
    // In case we are disabled mid-bounce, we don't want to drift to a new 
    // resting position
    transform.localPosition = m_restingPosition;
  }

  private void OnEnable()
  {
    m_restingPosition = transform.localPosition;
    if (bounceOnEnable)
      m_nextBounceTime = Time.time;
    else
      ScheduleNextBounceAnimation(Time.time);
  }
}
