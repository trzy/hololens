using UnityEngine;

public class FloatingPlatform: MonoBehaviour
{
  private void Update()
  {
    // Platform surface normal is always +z (forward) axis
    transform.position = transform.position + Time.deltaTime * transform.forward;
  }
}