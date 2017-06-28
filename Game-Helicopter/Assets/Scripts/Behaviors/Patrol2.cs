using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Patrol2: MonoBehaviour
{
  private NavMeshAgent m_agent;
  private NavMeshHit m_hit = new NavMeshHit();
  private bool m_hitObstacle = false;

  private void Start()
  {
    m_agent = GetComponent<NavMeshAgent>();
    m_agent.destination = NextDestination();
  }

  private Vector3 NextDestination()
  {
    float maxDistance = 5;
    Vector3 direction = transform.right;
    if (m_hitObstacle)
    {
      // We were heading toward an obstacle and must have arrived. We want to
      // go to the right, directly perpendicular to its normal. Unity uses a
      // left-handed cross product and the normal is pointing toward us.
      direction = Vector3.Cross(m_hit.normal, transform.up).normalized;
    }

    Vector3 target = transform.position + maxDistance * direction;

    if (NavMesh.Raycast(transform.position, target, out m_hit, NavMesh.AllAreas))
    {
      // Moving toward an obstacle
      m_hitObstacle = true;
      return m_hit.position;
    }
    else
    {
      // No obstacle, move directly to point
      m_hitObstacle = false;
      return target;
    }
  }

  private void Update()
  {
    if (!m_agent.pathPending && m_agent.remainingDistance < 0.1f)
      m_agent.destination = NextDestination();
  }
}