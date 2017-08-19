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
  bool m_engagingTarget = false;

  private void FixedUpdate()
  {
    if (target == null)
      return;

    float distanceToTarget = (target.position - transform.position).magnitude;

    if (!m_engagingTarget)
    {
      if (distanceToTarget < 1.5f)
      {
        m_engagingTarget = true;
        m_autopilot.Halt();
      }
      else if (!m_autopilot.flying)
      {
        // Doing nothing, start circling in place
        m_autopilot.Orbit(transform.position, transform.position.y + 0.5f, 1f);
        m_autopilot.throttle = 0.25f;
      }
    }
    else
    {
      if (distanceToTarget < 1.5f && !m_autopilot.flying)
      {
        // Circle the target when it is up close
        //TODO: callback should take number of revolutions completed
        m_autopilot.OrbitAndLookAt(target.transform, 0, 1.5f);
        m_autopilot.throttle = 1;
      }
    }




    switch (m_state)
    {
      case State.Thinking:
        
        
        
        
        
        
        
        /*
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
        */

        
        m_state = State.FlyingTowards;
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
