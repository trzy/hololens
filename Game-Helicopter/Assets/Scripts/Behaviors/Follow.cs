using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
class Follow: MonoBehaviour
{
  [Tooltip("Object to try to follow.")]
  public GameObject target = null;

  [Tooltip("Distance from target at which to stand still.")]
  public float targetDistance = 0.5f;

  [Tooltip("How many seconds we should stay on a given path before changing course.")]
  public float secondsBetweenPathChanges = 0;

  private NavMeshAgent m_agent;
  private float m_lastPathChangeTime;

  private void UpdateDestination()
  {
    m_agent.destination = target.transform.position;
    m_lastPathChangeTime = Time.time;
  }

  private bool TooFarFromTarget()
  {
    return Vector3.Distance(m_agent.destination, target.transform.position) > targetDistance;
  }

  private bool EnoughTimeSincePathChanged()
  {
    return (Time.time - m_lastPathChangeTime) >= secondsBetweenPathChanges;
  }

  private void Start()
  {
    m_agent = GetComponent<NavMeshAgent>();
    m_agent.stoppingDistance = targetDistance;
  }

  private void Update()
  {
    if (target == null)
      return;
    if (!m_agent.pathPending && TooFarFromTarget() && EnoughTimeSincePathChanged())
      UpdateDestination();
  }
}