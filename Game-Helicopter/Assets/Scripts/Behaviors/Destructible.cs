/*
 * Requirements:
 * -------------
 * - projectileLayer: Set this to the projectile layer and make sure all
 *   objects in that layer implement IProjectile.
 */

using UnityEngine;

public class Destructible : MonoBehaviour
{
  [Tooltip("Health is how many hit points of damage this object can take before being destroyed.")]
  public int healthPoints = 10;

  [Tooltip("Which layer projectile objects that can damage us are in. These must all implement IProjectile.")]
  public LayerMask projectileLayer;

  private void SelfDestruct()
  {
    foreach (MonoBehaviour behavior in gameObject.GetComponentsInChildren<MonoBehaviour>())
    {
      behavior.enabled = false;
    }
    gameObject.SetActive(false);
  }

  private void OnCollisionEnter(Collision collision)
  {
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
      healthPoints = 0;
      SelfDestruct();
    }
  }

  private void Awake()
  {
  }
}