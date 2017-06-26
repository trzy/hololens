using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
class Patrol1: MonoBehaviour
{
  private const int NUM_SAMPLES = 6;
  private const float DEGREES_PER_SAMPLE = 360f / NUM_SAMPLES;
  private const float PATROL_RADIUS = 1;

  private bool m_ready = false;

  private NavMeshAgent m_agent;

  private List<Vector3> m_waypoints = new List<Vector3>(NUM_SAMPLES);
  private int m_nextWaypoint = 0;

  private Vector3 TowardAngle(float angle)
  {
    angle *= Mathf.Deg2Rad;
    return new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)).normalized;
  }

  private IEnumerator Start()
  {
    m_agent = GetComponent<NavMeshAgent>();

    Vector3[] points = new Vector3[NUM_SAMPLES];
    int numPoints = 0;

    // Sample potential waypoints on the NavMesh around us
    for (int a = 0; a < NUM_SAMPLES; a++)
    {
      Vector3 direction = TowardAngle(a * DEGREES_PER_SAMPLE);
      NavMeshHit hit;
      if (NavMesh.Raycast(transform.position, transform.position + PATROL_RADIUS * direction, out hit, NavMesh.AllAreas))
        points[numPoints++] = hit.position; //TODO: move back a little from obstruction?
      else
        points[numPoints++] = transform.position + PATROL_RADIUS * direction;

      yield return null;
    }

    //TODO: compute paths between points and remove unreachable points

    //TODO: enforce a minimum distance between points, removing those too close
    //      together

    // Copy to final waypoints
    for (int i = 0; i < numPoints; i++)
    {
      m_waypoints.Add(points[i]);
    }

    m_ready = true;
  }

  private void Update()
  {
    if (!m_ready)
      return;
    if (!m_agent.pathPending && m_agent.remainingDistance < 0.1f)
    {
      m_agent.destination = m_waypoints[m_nextWaypoint];
      m_nextWaypoint = (m_nextWaypoint + 1) % m_waypoints.Count;
    }
  }
}