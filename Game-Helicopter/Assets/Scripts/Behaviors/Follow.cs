using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Follow: MonoBehaviour
{
  [Tooltip("Object to try to follow.")]
  public Transform target = null;

  [Tooltip("Distance from target at which to stand still.")]
  public float targetDistance = 0.5f;

  [Tooltip("How many seconds we should stay on a given path before changing course.")]
  public float secondsBetweenPathChanges = 0;

  private NavMeshAgent m_agent = null;
  private float m_lastPathChangeTime = 0;

  private void UpdateDestination(Vector3 position)
  {
    m_agent.destination = position;
    m_lastPathChangeTime = Time.time;
  }

  private bool TooFarFromTarget()
  {
    return MathHelpers.Azimuthal(m_agent.destination - target.position).magnitude > targetDistance;
  }

  private void StopMoving()
  {
    m_agent.destination = transform.position;
    m_agent.ResetPath();
  }

  private bool EnoughTimeSincePathChanged()
  {
    return (Time.time - m_lastPathChangeTime) >= secondsBetweenPathChanges;
  }

  private void Update()
  {
    if (target == null)
      return;
    if (!m_agent.pathPending)
    {
      if (TooFarFromTarget())
      {
        if (EnoughTimeSincePathChanged())
          UpdateDestination(target.position);
      }
      else
        StopMoving();
    }
  }

  private void Start()
  {
    m_agent = GetComponent<NavMeshAgent>();
  }
}