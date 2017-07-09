using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Missile: MonoBehaviour
{
  [Tooltip("Missile's nozzle (point where thrust is applied).")]
  public Transform nozzle;

  [Tooltip("Velocity (m/sec).")]
  public float thrust = 10;

  [Tooltip("Maximum lifetime before being removed (sec).")]
  public float lifeTime = 3f;

  [Tooltip("A surface hit effect will be created when the missile collides with any of these layers.")]
  public LayerMask surfaceHitFXLayers;

  private Rigidbody m_rb;
  private bool m_collided = false;
  private float m_t0;

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
    if (Time.time - m_t0 >= lifeTime)
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