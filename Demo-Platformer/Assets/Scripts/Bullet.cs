using UnityEngine;
using System.Collections;

public class Bullet: MonoBehaviour
{
  public float velocity = 6f;
  public float maxLifeTime = 3f;

  private float m_t0;
  private bool m_collided = false;  // once true, the object will be destroyed once audio is complete

  private void CreateSurfaceHitFX(GameObject hitObject, Vector3 hitPoint, Vector3 hitNormal)
  {
    /*
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
        ParticleEffectsManager.Instance.CreateCrater(hitPoint, hitNormal);
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
    /*
    if (Layers.Instance.IsSpatialMeshLayer(target.layer))
    {
      // Create a blast effect
      ContactPoint contact = collision.contacts[0];
      CreateSurfaceHitFX(target, contact.point, contact.normal);
    }
    */

    // If we hit a collidable object, the bullet should be destroyed
    //if (Layers.Instance.IsCollidableLayer(target.layer))
    {
      m_collided = true;  // schedule for destruction
      Disable();
    }
  }

  void Start()
  {
    Rigidbody rb = GetComponent<Rigidbody>();
    Vector3 forward = Vector3.Normalize(transform.forward);
    rb.velocity = forward * velocity;
    m_t0 = Time.time;
  }

  void FixedUpdate()
  {
    if (m_collided || Time.time - m_t0 >= maxLifeTime)
      Destroy(gameObject);
  }
}