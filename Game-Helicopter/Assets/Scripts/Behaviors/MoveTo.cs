using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class MoveTo: MonoBehaviour
{
  private NavMeshAgent m_agent;

  public void Move(Vector3 position)
  {
    m_agent.destination = position;
  }

  private void Awake()
  {
    m_agent = GetComponent<NavMeshAgent>();
  }
}