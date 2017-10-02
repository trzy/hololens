using System.Collections;
using UnityEngine;

public class SelfDestruct: MonoBehaviour
{
  [Tooltip("Time in seconds from Start until self-destruct.")]
  public float time = 1;

  IEnumerator Start()
  {
    yield return new WaitForSeconds(time);
    Destroy(gameObject);
  }
}