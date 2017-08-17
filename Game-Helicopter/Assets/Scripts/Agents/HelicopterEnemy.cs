using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(HelicopterAutopilot))]
public class HelicopterEnemy: MonoBehaviour
{
  public Transform target;

  private HelicopterAutopilot m_autopilot;

  private enum State
  {
    Thinking,
    Busy
  }

  private State m_state = State.Thinking;

  private void FixedUpdate()
  {
    if (target == null)
      return;

    float distanceToTarget = (target.position - transform.position).magnitude;

    switch (m_state)
    {
      case State.Thinking:
        if (distanceToTarget > 1)
        {
          m_autopilot.Follow(target, 0.5f, 60 * 2, () => { Debug.Log("Caught target!"); });
          m_state = State.Busy;
        }
        break;
      case State.Busy:
      default:
        break;
    }
  }

  private void Start()
  {
    //m_autopilot.Orbit(Camera.main.transform.position, 2, 0.5f);
  }

  private void Awake()
  {
    m_autopilot = GetComponent<HelicopterAutopilot>();
  }
}
