using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class Missile: MonoBehaviour, IProjectile
{
  [Tooltip("How much damage this inflicts.")]
  public int hitPoints = 10;

  [Tooltip("Missile's nozzle (point where thrust is applied).")]
  public Transform nozzle;

  [Tooltip("Velocity (m/sec).")]
  public float thrust = 10;

  [Tooltip("Maximum lifetime before being removed (sec).")]
  public float lifetime = 3f;

  [Tooltip("A surface hit effect will be created when the missile collides with any of these layers.")]
  public LayerMask surfaceHitFXLayers;

  public int HitPoints
  {
    get
    {
      return hitPoints;
    }
  }

  public float Lifetime
  {
    get
    {
      return lifetime;
    }
  }

  private Rigidbody m_rb;
  private bool m_collided = false;
  private float m_t0;

  public void IgnoreCollisions(GameObject obj)
  {
    Collider ourCollider = GetComponent<Collider>();
    if (ourCollider == null)
      return;

    foreach (Collider collider in obj.GetComponentsInChildren<Collider>())
    {
      Physics.IgnoreCollision(ourCollider, collider);
    }
  }
  
  private void OnCollisionEnter(Collision collision)
  {
    if (m_collided)
      return;

    GameObject target = collision.collider.gameObject;

    if ((surfaceHitFXLayers.value & (1 << target.layer)) != 0)
    {
    }

    // Destroy the missile
    m_collided = true;
    gameObject.SetActive(false);
  }

  private void FixedUpdate()
  {
    if (Time.time - m_t0 >= lifetime)
    {
      gameObject.SetActive(false);
      return;
    }
    m_rb.AddForce(transform.forward * thrust);
  }

  private void OnEnable()
  {
    m_collided = false;
    m_t0 = Time.time;
    m_rb.isKinematic = false;
  }

  private void Awake()
  {
    m_rb = GetComponent<Rigidbody>();
    m_rb.isKinematic = true;  // disabled until firing
  }
}