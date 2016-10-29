using UnityEngine;
using System.Collections;

public class TimeLimited: MonoBehaviour
{
  [Tooltip("Lifetime in seconds before being destroyed.")]
  public float lifeTime = 10;

  private float m_t0;

  void Start()
  {
    m_t0 = Time.time;
	}

  void FixedUpdate()
  {
    //Rigidbody rb = GetComponent<Rigidbody>();
    //rb.AddTorque(1e12f * rb.mass * Vector3.right, ForceMode.Acceleration);
  }

	void Update()
  {
    if (Time.time - m_t0 >= lifeTime)
      Destroy(this.gameObject);
	}
}
