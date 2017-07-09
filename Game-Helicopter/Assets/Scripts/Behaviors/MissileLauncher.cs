using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MissileLauncher: MonoBehaviour
{
  public Missile[] missiles;
  public float timeDelayToFirstFire = 2;
  public float minTimeDelayBetweenFiring = 5;
  public float maxTimeDelayBetweenFiring = 10;
  
  private float m_startTime;
  private float m_nextFiringTime;
  private int m_nextMissileIdx = 0;

  private void ScheduleNextFiringTime(float now)
  {
    m_nextFiringTime = now + UnityEngine.Random.Range(minTimeDelayBetweenFiring, maxTimeDelayBetweenFiring);
  }

  private void FixedUpdate()
  {
    if (m_nextMissileIdx >= missiles.Length)
      return;

    float now = Time.time;

    // Wait initial delay period before any firing occurs
    float timeSinceLock = now - m_startTime;
    if (timeSinceLock < timeDelayToFirstFire)
      return;

    // Wait until scheduled firing time and then schedule another
    if (now < m_nextFiringTime)
      return;
    ScheduleNextFiringTime(now);

    // Ignition!
    Missile missile = missiles[m_nextMissileIdx++];
    if (missile == null)
      return;
    missile.enabled = true;
  }
  
  private void OnEnable()
  {
    float now = Time.time;
    m_startTime = now;
    ScheduleNextFiringTime(now);
  }
}
