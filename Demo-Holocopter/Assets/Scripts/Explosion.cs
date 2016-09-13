using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Explosion: MonoBehaviour
{
  [Tooltip("Prefab for billboard explosion. Must destroy itself after animation completed.")]
  public GameObject m_explosion_billboard1_prefab;

  [Tooltip("Prefab for volumetric sphereical explosion. Must destroy itself after animation completed.")]
  public GameObject m_explosion_sphere_prefab;

  private struct DeferredExplosion
  {
    public float start_time;
    public GameObject explosion;
    public DeferredExplosion(float t, GameObject obj)
    {
      start_time = t;
      explosion = obj;
    }
  }

  private List<DeferredExplosion> m_explosions = null;
  private int m_next_explosion_idx = 0;

  private Vector3 RandomPosition(float radius)
  {
    // Random position within a spherical zone
    float r = Random.Range(0, radius);
    float theta = Random.Range(0, 180) * Mathf.Deg2Rad;
    float phi = Random.Range(0, 360) * Mathf.Deg2Rad;
    float sin_theta = Mathf.Sin(theta);
    return new Vector3(r * sin_theta * Mathf.Cos(phi), r * sin_theta * Mathf.Sin(phi), r * Mathf.Cos(theta));
  }
  
  public void CreateCloud(Vector3 centroid, float radius, int count, float delay_time = 0)
  {
    // Re-use is prevented to avoid memory leaks that would occur if 
    // SetActive() not called on previous explosion objects.
    if (m_explosions != null)
      return;
    m_explosions = new List<DeferredExplosion>(count);
    m_next_explosion_idx = 0;
    float start_time = Time.time;
    while (count-- > 0)
    {
      Vector3 pos = centroid + RandomPosition(radius);
      GameObject billboard_explosion = Instantiate(m_explosion_billboard1_prefab, pos, m_explosion_billboard1_prefab.transform.rotation) as GameObject;
      GameObject volumetric_explosion = Instantiate(m_explosion_sphere_prefab, pos, m_explosion_sphere_prefab.transform.rotation) as GameObject;
      m_explosions.Add(new DeferredExplosion(start_time, billboard_explosion));
      m_explosions.Add(new DeferredExplosion(start_time, volumetric_explosion));
      start_time += delay_time;
    }
  }

  public void CreatePillar(Vector3 base_pos, float height, int count, float delay_time = 0)
  {
    if (m_explosions != null)
      return;
    float vertical_step_size = height / count;
    Vector3 pos = base_pos;
    m_explosions = new List<DeferredExplosion>(count);
    m_next_explosion_idx = 0;
    float start_time = Time.time;
    while (count-- > 0)
    {
      GameObject billboard_explosion = Instantiate(m_explosion_billboard1_prefab, pos, m_explosion_billboard1_prefab.transform.rotation) as GameObject;
      GameObject volumetric_explosion = Instantiate(m_explosion_sphere_prefab, pos, m_explosion_sphere_prefab.transform.rotation) as GameObject;
      m_explosions.Add(new DeferredExplosion(start_time, billboard_explosion));
      m_explosions.Add(new DeferredExplosion(start_time, volumetric_explosion));
      start_time += delay_time;
      pos += Vector3.up * vertical_step_size;
    }
  }

  void Start()
  {
    //CreateCloud(new Vector3(0, 0, 0), 2, 8);
  }

	void Update()
  {
    if (m_explosions == null)
      return;
    float now = Time.time;
    while (m_next_explosion_idx < m_explosions.Count && now >= m_explosions[m_next_explosion_idx].start_time)
    {
      m_explosions[m_next_explosion_idx++].explosion.SetActive(true);
    }
    if (m_next_explosion_idx >= m_explosions.Count)
    {
      // All explosions have been activated and will destroy themselves on
      // completion. It is safe to destroy ourself now.
      m_explosions = null;
      Destroy(this.gameObject);
    }
	}
}
