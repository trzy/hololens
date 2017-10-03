/*
 * Requirements:
 * -------------
 * - projectileLayer: Set this to the projectile layer and make sure all
 *   objects in that layer implement IProjectile.
 */

using UnityEngine;

[RequireComponent(typeof(SelfDestruct))]
public class Destructible : MonoBehaviour
{
  [Tooltip("Health is how many hit points of damage this object can take before being destroyed.")]
  public int healthPoints = 10;

  [Tooltip("Which layer projectile objects that can damage us are in. These must all implement IProjectile.")]
  public LayerMask projectileLayer;

  [Tooltip("Bullet impact particle effect.")]
  public ParticleSystem bulletImpactPrefab;

  [Tooltip("Bullet impact sound clips.")]
  public AudioClip[] bulletImpactClips;

  [Tooltip("Explosions when destroyed (a random one will be selected).")]
  public GameObject[] explosionPrefabs;

  [Tooltip("Explosion sound clips.")]
  public AudioClip[] explosionClips;

  private AudioSource m_audio;
  private GameObject[] m_explosions;
  private bool m_destroyed = false;

  private void SelfDestruct(float delay)
  {
    foreach (MonoBehaviour behavior in gameObject.GetComponentsInChildren<MonoBehaviour>())
    {
      behavior.enabled = false;
    }

    foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
    {
      renderer.enabled = false;
    }

    SelfDestruct destructor = GetComponent<SelfDestruct>();
    destructor.time = delay;
    destructor.enabled = true;
    //gameObject.SetActive(false);
  }

  private void OnCollisionEnter(Collision collision)
  {
    if (m_destroyed)
      return;

    GameObject collided = collision.collider.gameObject;
    int collidedLayer = 1 << collided.layer;
    if ((collidedLayer & projectileLayer) == 0)
      return;

    IProjectile projectile = collided.GetComponent<IProjectile>();
    if (projectile == null)
      return;

    healthPoints -= projectile.HitPoints;
    if (healthPoints <= 0)
    {
      m_destroyed = true;
      healthPoints = 0;
      int random = Random.Range(0, explosionPrefabs.Length - 1);
      if (explosionPrefabs.Length > 0)
      {
        m_explosions[random].transform.position = transform.position;
        m_explosions[random].transform.rotation = transform.rotation;
        m_explosions[random].SetActive(true);
      }
      random = Random.Range(0, explosionClips.Length - 1);
      m_audio.Stop();
      m_audio.PlayOneShot(explosionClips[random]);
      SelfDestruct(explosionClips[random].length);
    }
    else if (bulletImpactPrefab != null)
    {
      Instantiate(bulletImpactPrefab, collision.contacts[0].point, Quaternion.identity);
      int random = Random.Range(0, bulletImpactClips.Length - 1);
      m_audio.Stop();
      m_audio.PlayOneShot(bulletImpactClips[random]);
    }
  }

  private void Awake()
  {
    m_audio = GetComponent<AudioSource>();
    m_explosions = new GameObject[explosionPrefabs.Length];
    for (int i = 0; i < explosionPrefabs.Length; i++)
    {
      m_explosions[i] = Instantiate(explosionPrefabs[i]);
      m_explosions[i].SetActive(false);
    }
  }
}