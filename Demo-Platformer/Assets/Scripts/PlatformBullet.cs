//TODO: spatial understanding meshes do not have PhysicsLayer set! This should be set when
//      colliders are enabled on them. Need to create a git pull request.
using UnityEngine;
using HoloToolkit.Unity.SpatialMapping;
using System.Collections;

public class PlatformBullet: MonoBehaviour
{
  public float velocity = 6f;
  public float maxLifeTime = 3f;
  public Material platformMaterial;

  private float m_t0;
  private bool m_collided = false;  // once true, the object will be destroyed once platform is extruded
  private Vector3 m_hitPosition;
  private Vector3 m_hitNormal;
  private GeoMaker m_geo = new GeoMaker();

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
    GameObject hitObject = collision.collider.gameObject;

    // We are responsible for collisions with the spatial mesh (which includes SurfacePlanes)
    if (hitObject.layer == SpatialMappingManager.Instance.PhysicsLayer)
    {
      ContactPoint contact = collision.contacts[0];
      Debug.Log("platform bullet hit surface");
      m_hitPosition = contact.point;
      m_hitNormal = contact.normal;
      m_geo.StartSelection(GeoMaker.PlatformType.Wall, platformMaterial, 0.25f);
    }
    //else
    //  Debug.Log("hit something else: " + hitObject.layer);

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
    if (m_collided)
    {
      m_geo.Update(m_hitPosition + 0.1f * m_hitNormal, -m_hitNormal, 0.5f);
      if (m_geo.state == GeoMaker.State.Select)
        m_geo.FinishSelection(platformMaterial, null);
      else if (m_geo.state == GeoMaker.State.Idle)
        Destroy(gameObject);
    }
    else if (Time.time - m_t0 >= maxLifeTime)
      Destroy(gameObject);
  }
}