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
  public ParticleEffectsManager m_particle_fx_manager;
  public float m_velocity = 6f;
  public float m_max_lifetime = 5f;

  private float m_t0;
  private bool m_collided = false;

  private void CreateSurfaceHitFX(Vector3 hit_point, Vector3 hit_normal)
  {
    m_particle_fx_manager.CreateBulletImpact(hit_point, hit_normal);
    if (Vector3.Angle(hit_normal, Vector3.up) < 10)
    {
      // Lingering fireball only when hitting the ground
      m_particle_fx_manager.CreateLingeringFireball(hit_point, hit_normal, 0);
    }
    else if (Mathf.Abs(90 - Vector3.Angle(hit_normal, Vector3.up)) < 10)
    {
      // Debris when hitting walls
      //TODO: wall detection should actually involve testing against detected wall planes
      m_particle_fx_manager.CreateBulletImpactDebris(hit_point, hit_normal, 0.1f, 3, 0);
    }
  }

  void OnCollisionEnter(Collision collision)
  {
    // Any object that wishes to react to bullets should implement its own
    // collision callback. Spatial mesh collisions, however, are handled here
    // directly.
    GameObject target = collision.collider.gameObject;

    // We are responsible for collisions with the spatial mesh
    if (Layers.Instance.IsSpatialMeshLayer(target.layer))
    {
      // Sphere collider can collide with spatial mesh at multiple points
      // simultaneously but we only create a blast effect at one point
      if (m_collided)
        return;

      // Create a blast effect
      ContactPoint contact = collision.contacts[0];
      CreateSurfaceHitFX(contact.point, contact.normal);
    }

    // If we hit a collidable object, the bullet should be destroyed
    if (Layers.Instance.IsCollidableLayer(target.layer))
    {
      m_collided = true;
      Destroy(gameObject);
    }
  }

  void Start()
  {
    Rigidbody rb = GetComponent<Rigidbody>();
    Vector3 forward = Vector3.Normalize(transform.forward);
    rb.velocity = forward * m_velocity;
    m_t0 = Time.time;
  }

  void FixedUpdate()
  {
    if (Time.time - m_t0 >= m_max_lifetime)
      Destroy(gameObject);
  }
}