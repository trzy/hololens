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
    FlyingTowards,
    BackingAway
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
        if (distanceToTarget > 2)
        {
          m_autopilot.Follow(target, 2f, 60 * 2, 
            () =>
            {
              Debug.Log("Caught target!");
              m_state = State.Thinking;
            });
          m_state = State.FlyingTowards;
        }
        else if (distanceToTarget < 1.5f)
        {
          // Keep our distance!
          Vector3 awayFromTarget = (transform.position - target.position).normalized;
          m_autopilot.FlyTo(target.position + 2 * awayFromTarget, target, 10, 
            () =>
            {
              Debug.Log("Backed off");
            });
          m_state = State.BackingAway;
        }
        break;
      case State.FlyingTowards:

        break;
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
