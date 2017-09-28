using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleShooter: MonoBehaviour
{
  public Bullet bulletPrefab;
  public Transform muzzle;
  public float timeDelayToFirstFire = 2;
  public float minTimeDelayBetweenBursts = 1;
  public float maxTimeDelayBetweenBursts = 3;
  public float firingRateHz = 5;
  public int minBurstSize = 1;
  public int maxBurstSize = 5;

  private float m_fireDelay;
  private int m_bulletsRemainingInBurst = 0;
  private float m_startTime;
  private float m_nextBurstTime;
  private float m_lastFiredTime;

  private Bullet[] m_bulletPool = new Bullet[16];
  private int m_nextBulletIdx = 0;

  private Bullet GetBulletFromPool()
  {
    Bullet bullet = m_bulletPool[m_nextBulletIdx];
    if (bullet.gameObject.activeSelf)
      return null;  // no free bullets currently
    m_nextBulletIdx = (m_nextBulletIdx + 1) % m_bulletPool.Length;
    return bullet;
  }

  private void FixedUpdate()
  {
    float now = Time.time;

    // Wait initial delay period before any firing occurs
    float timeSinceLock = now - m_startTime;
    if (timeSinceLock < timeDelayToFirstFire)
      return;

    // Schedule a burst if none pending
    if (m_bulletsRemainingInBurst <= 0)
    {
      // Choose next burst size and time at which to start
      m_bulletsRemainingInBurst = UnityEngine.Random.Range(minBurstSize, maxBurstSize + 1);
      m_nextBurstTime = now + UnityEngine.Random.Range(minTimeDelayBetweenBursts, maxTimeDelayBetweenBursts);
      m_lastFiredTime = now - m_fireDelay;  // ensure that when burst starts, first shot fires immediately
    }

    // Wait until scheduled burst time
    if (now < m_nextBurstTime)
      return;

    // Wait until it's time to fire another bullet
    if (now - m_lastFiredTime < m_fireDelay)
      return;
    m_bulletsRemainingInBurst -= 1;
    m_lastFiredTime = now;

    // Let her rip!
    Bullet bullet = GetBulletFromPool();
    if (bullet == null)
      return;
    bullet.transform.position = muzzle.position;
    bullet.transform.rotation = muzzle.rotation;
    bullet.gameObject.SetActive(true);
  }
  
  private void OnEnable()
  {
    m_startTime = Time.time;
    m_fireDelay = 1 / firingRateHz;
    m_lastFiredTime = Time.time;
  }

  private void Awake()
  {
    for (int i = 0; i < m_bulletPool.Length; i++)
    {
      m_bulletPool[i] = Instantiate(bulletPrefab) as Bullet;
      m_bulletPool[i].gameObject.SetActive(false);
      m_bulletPool[i].GetComponent<IProjectile>().IgnoreCollisions(gameObject);
    }
  }
}
