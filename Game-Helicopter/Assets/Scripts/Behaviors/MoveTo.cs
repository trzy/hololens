using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class MoveTo: MonoBehaviour
{
  private NavMeshAgent m_agent;
  private Action m_OnTargetReached = null;
  private float m_nearEnough = 0;

  public void Move(Vector3 position, Action OnTargetReached = null, float nearEnough = 0.1f)
  {
    m_agent.destination = position;
    m_OnTargetReached = OnTargetReached;
    m_nearEnough = nearEnough;
  }

  private void FixedUpdate()
  {
    if (!m_agent.pathPending && m_agent.remainingDistance <= m_nearEnough)
    {
      if (m_OnTargetReached != null)
      {
        m_OnTargetReached();
        m_OnTargetReached = null;
      }
      enabled = false;
    }
  }

  private void Awake()
  {
    m_agent = GetComponent<NavMeshAgent>();
  }
}