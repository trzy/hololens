using UnityEngine;
using System.Collections;

public class Waypoint: MonoBehaviour
{
  void Start()
  {
  }

  void Update()
  {
    transform.Rotate(new Vector3(0, 180.0F * Time.deltaTime, 0));
  }
}
