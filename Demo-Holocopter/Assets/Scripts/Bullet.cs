/*
 * TODO:
 * -----
 * - Keep track of direction of travel and do not generate a blast effect when
 *   collision point normal angle is too far askew.
 */

using UnityEngine;
using System.Collections;

public class Bullet: MonoBehaviour
{
  public float velocity = 6f;
  public float maxLifeTime = 3f;

  private AudioSource m_ricochetSound;
  private static AudioSource m_lastRicochetSound = null;
  private float m_t0;
  private bool m_collided = false;  // once true, the object will be destroyed once audio is complete

  private void PlayRicochetSound()
  {
    // Prevent sounds from mixing; sounds better
    if (m_lastRicochetSound != null && m_lastRicochetSound.isPlaying)
      m_lastRicochetSound.Stop();
    m_ricochetSound.Play();
    m_lastRicochetSound = m_ricochetSound;
  }

  private void CreateSurfaceHitFX(GameObject hitObject, Vector3 hitPoint, Vector3 hitNormal)
  {
    if (hitObject.CompareTag(Layers.Instance.surfacePlaneTag))
    {
      ParticleEffectsManager.Instance.CreateBulletImpact(hitPoint, hitNormal);
      HoloToolkit.Unity.SpatialMapping.SurfacePlane plane = hitObject.GetComponent<HoloToolkit.Unity.SpatialMapping.SurfacePlane>();
      switch (plane.PlaneType)
      {
      case HoloToolkit.Unity.SpatialMapping.PlaneTypes.Wall:
        ParticleEffectsManager.Instance.CreateBulletImpactDebris(hitPoint, hitNormal, 0.1f, 3, 0);
        ParticleEffectsManager.Instance.CreateBulletHole(hitPoint, hitNormal, plane);
        break;
      case HoloToolkit.Unity.SpatialMapping.PlaneTypes.Floor:
        ParticleEffectsManager.Instance.CreateLingeringFireball(hitPoint, hitNormal, 0);
        //ParticleEffectsManager.Instance.CreateCrater(hitPoint, hitNormal);
        break;
      default:
        break;
      }
    }
    else
    {
      ParticleEffectsManager.Instance.CreateBulletImpact(hitPoint, hitNormal);
      float cos80 = 0.1736f;
      if (Mathf.Abs(Vector3.Dot(hitNormal, Vector3.right)) > cos80 &&
          Mathf.Abs(Vector3.Dot(hitNormal, Vector3.up)) > cos80 &&
          Mathf.Abs(Vector3.Dot(hitNormal, Vector3.forward)) > cos80)
      {
        PlayRicochetSound();
      }
      else if (Mathf.Abs(hitNormal.y) < 0.05f)
      {
        // Must have hit a wall
        //TODO: for spatial understanding, we should raycast and determine whether we actually hit a real wall
        ParticleEffectsManager.Instance.CreateBulletImpactDebris(hitPoint, hitNormal, 0.1f, 3, 0);
        ParticleEffectsManager.Instance.CreateBulletHole(hitPoint, hitNormal);
      }
      else if (hitNormal.y > 0.95f)
      {
        // Must have hit the floor
        //TODO: raycast spatial understanding
        ParticleEffectsManager.Instance.CreateLingeringFireball(hitPoint, hitNormal, 0);
      }
    }
    /*
    if (Vector3.Angle(hitNormal, Vector3.up) < 10)
    {
      // Lingering fireball only when hitting the ground
      ParticleEffectsManager.Instance.CreateLingeringFireball(hitPoint, hitNormal, 0);
    }
    else if (Mathf.Abs(90 - Vector3.Angle(hitNormal, Vector3.up)) < 10)
    {
      // Debris when hitting walls
      //TODO: wall detection should actually involve testing against detected wall planes
      ParticleEffectsManager.Instance.CreateBulletImpactDebris(hitPoint, hitNormal, 0.1f, 3, 0);
    }
    */
  }

  private void Disable()
  {
    Collider[] colliders = GetComponentsInChildren<Collider>();
    Renderer[] renderers = GetComponentsInChildren<Renderer>();
    foreach (Collider collider in colliders)
      collider.enabled = false;
    foreach (Renderer renderer in renderers)
      renderer.enabled = false;
  }

  void OnCollisionEnter(Collision collision)
  {
    // Sphere collider can collide with spatial mesh at multiple points
    // simultaneously but we only create a blast effect at one point
    if (m_collided)
      return;

    // Any object that wishes to react to bullets should implement its own
    // collision callback. Spatial mesh collisions, however, are handled here
    // directly.
    GameObject target = collision.collider.gameObject;

    // We are responsible for collisions with the spatial mesh (which includes SurfacePlanes)
    if (Layers.Instance.IsSpatialMeshLayer(target.layer))
    {
      // Create a blast effect
      ContactPoint contact = collision.contacts[0];
      CreateSurfaceHitFX(target, contact.point, contact.normal);
    }

    // If we hit a collidable object, the bullet should be destroyed
    if (Layers.Instance.IsCollidableLayer(target.layer))
    {
      m_collided = true;  // schedule for destruction
      Disable();          // prevent rendering or further collisions
    }
  }

  void Start()
  {
    m_ricochetSound = GetComponent<AudioSource>();
    Rigidbody rb = GetComponent<Rigidbody>();
    Vector3 forward = Vector3.Normalize(transform.forward);
    rb.velocity = forward * velocity;
    m_t0 = Time.time;
  }

  void FixedUpdate()
  {
    if (m_ricochetSound.isPlaying)
      return;
    if (m_collided || Time.time - m_t0 >= maxLifeTime)
      Destroy(gameObject);
  }
}