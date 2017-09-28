using UnityEngine;

public class CollisionForwarder: MonoBehaviour
{
  [Tooltip("Component that will handle the collision. ")]
  public MonoBehaviour collisionHandler;

  private void OnCollisionEnter(Collision collision)
  {
    // Do not forward to ourselves
    if (collisionHandler != null && collisionHandler.gameObject != gameObject)
      collisionHandler.SendMessage("OnCollisionEnter", collision);
  }
}