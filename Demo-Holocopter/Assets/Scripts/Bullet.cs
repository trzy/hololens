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
  public float m_velocity = 6f;
  public float m_max_lifetime = 3f;

  private AudioSource m_ricochet_sound;
  private static AudioSource m_last_ricochet_sound = null;
  private float m_t0;
  private bool m_collided = false;  // once true, the object will be destroyed once audio is complete

  private void PlayRicochetSound()
  {
    // Prevent sounds from mixing; sounds better
    if (m_last_ricochet_sound != null && m_last_ricochet_sound.isPlaying)
      m_last_ricochet_sound.Stop();
    m_ricochet_sound.Play();
    m_last_ricochet_sound = m_ricochet_sound;
  }

  private void CreateSurfaceHitFX(GameObject hit_object, Vector3 hit_point, Vector3 hit_normal)
  {
    ParticleEffectsManager.Instance.CreateBulletImpact(hit_point, hit_normal);
    if (hit_object.CompareTag(Layers.Instance.surfacePlaneTag))
    {
      HoloToolkit.Unity.SurfacePlane plane = hit_object.GetComponent<HoloToolkit.Unity.SurfacePlane>();
      switch (plane.PlaneType)
      {
      case HoloToolkit.Unity.PlaneTypes.Wall:
        ParticleEffectsManager.Instance.CreateBulletImpactDebris(hit_point, hit_normal, 0.1f, 3, 0);
        ParticleEffectsManager.Instance.CreateBulletHole(hit_point, hit_normal, plane);
        break;
      case HoloToolkit.Unity.PlaneTypes.Floor:
        ParticleEffectsManager.Instance.CreateLingeringFireball(hit_point, hit_normal, 0);
        ParticleEffectsManager.Instance.CreateCrater(hit_point, hit_normal);
        break;
      default:
        break;
      }
    }
    else
    {
      float cos80 = 0.1736f;
      if (Mathf.Abs(Vector3.Dot(hit_normal, Vector3.right)) > cos80 &&
          Mathf.Abs(Vector3.Dot(hit_normal, Vector3.up)) > cos80 &&
          Mathf.Abs(Vector3.Dot(hit_normal, Vector3.forward)) > cos80)
      {
        PlayRicochetSound();
      }
    }
    /*
    if (Vector3.Angle(hit_normal, Vector3.up) < 10)
    {
      // Lingering fireball only when hitting the ground
      ParticleEffectsManager.Instance.CreateLingeringFireball(hit_point, hit_normal, 0);
    }
    else if (Mathf.Abs(90 - Vector3.Angle(hit_normal, Vector3.up)) < 10)
    {
      // Debris when hitting walls
      //TODO: wall detection should actually involve testing against detected wall planes
      ParticleEffectsManager.Instance.CreateBulletImpactDebris(hit_point, hit_normal, 0.1f, 3, 0);
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
    m_ricochet_sound = GetComponent<AudioSource>();
    Rigidbody rb = GetComponent<Rigidbody>();
    Vector3 forward = Vector3.Normalize(transform.forward);
    rb.velocity = forward * m_velocity;
    m_t0 = Time.time;
  }

  void FixedUpdate()
  {
    if (m_ricochet_sound.isPlaying)
      return;
    if (m_collided || Time.time - m_t0 >= m_max_lifetime)
      Destroy(gameObject);
  }
}