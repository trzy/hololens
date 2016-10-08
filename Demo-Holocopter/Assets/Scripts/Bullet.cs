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
  public GroundFlash m_ground_flash_prefab;
  public GroundBlast m_ground_blast_prefab;
  public float m_velocity = 6f;
  public float m_max_lifetime = 5f;

  private float m_t0;
  private bool m_collided = false;

  private void CreateSurfaceHitFX(Vector3 hit_point, Vector3 hit_normal)
  {
    GroundFlash flash = Instantiate(m_ground_flash_prefab, hit_point + hit_normal * 0.01f, Quaternion.LookRotation(hit_normal)) as GroundFlash;
    GroundBlast blast = Instantiate(m_ground_blast_prefab, hit_point + hit_normal * 0.02f, Quaternion.LookRotation(hit_normal)) as GroundBlast;
    blast.transform.localScale = new Vector3(0.03f, 0.03f, 0.03f);  // scale down to real world size
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