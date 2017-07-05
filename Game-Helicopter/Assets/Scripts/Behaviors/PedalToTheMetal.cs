using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PedalToTheMetal: MonoBehaviour
{
  public float acceleration = 1f;
  public float targetSpeed = 1.5f;

  private bool m_stop = false;

  private void SetLayer(string layerName)
  {
    int layer = LayerMask.NameToLayer(layerName);
    gameObject.layer = layer;
    foreach (Transform transform in GetComponentsInChildren<Transform>())
    {
      transform.gameObject.layer = layer;
    }
  }

  private void FixedUpdate()
  {
    if (m_stop)
      return;
    Rigidbody rb = GetComponent<Rigidbody>();
    if (rb.velocity.y < 0)
    {
      SetLayer("Default");
      m_stop = true;
    }
    else if (rb.velocity.magnitude < targetSpeed)
      GetComponent<Rigidbody>().AddRelativeForce(acceleration * Vector3.forward, ForceMode.VelocityChange);
  }
}
