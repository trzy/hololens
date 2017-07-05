using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class DriveOutOfTunnel: MonoBehaviour
{
  public float acceleration = 1f;
  public float targetSpeed = 1.5f;
  public MonoBehaviour OnNavMeshReached;

  private enum State
  {
    ExitingTunnel,
    WaitUntilStopped,
    SeekingNavMesh,
    Stopped
  }

  private State m_state = State.ExitingTunnel;
  private Vector3 m_navMeshPosition;
  private Rigidbody m_rb;

  private bool NavMeshIsReachable()
  {
    NavMeshHit hit;
    if (!NavMesh.SamplePosition(transform.position, out hit, 1, NavMesh.AllAreas))
    {
      Debug.Log("NavMesh not reachable");
      return false;
    }
    // TODO: Check whether reachable, make sure Y position is not too different
    // TODO: box overlap test?
    m_navMeshPosition = hit.position;
    return true;
  }

  private void SetLayer(string layerName)
  {
    int layer = LayerMask.NameToLayer(layerName);
    gameObject.layer = layer;
    foreach (Transform transform in GetComponentsInChildren<Transform>())
    {
      transform.gameObject.layer = layer;
    }
  }

  private void FixedUpdate()
  {
    switch (m_state)
    {
      default:
      case State.Stopped:
        enabled = false;
        break;
      case State.ExitingTunnel:
        if (m_rb.velocity.y < 0)
        {
          SetLayer("Default");
          m_state = State.WaitUntilStopped;
        }
        else if (m_rb.velocity.magnitude < targetSpeed)
          m_rb.AddRelativeForce(acceleration * Vector3.forward, ForceMode.VelocityChange);
        break;
      case State.WaitUntilStopped:
        if (m_rb.velocity.magnitude < 0.1f)
          m_state = NavMeshIsReachable() ? State.SeekingNavMesh : State.Stopped;
        break;
      case State.SeekingNavMesh:
        Vector3 toTarget = m_navMeshPosition - transform.position;
        Debug.Log("toTarget=" + toTarget);
        if (toTarget.magnitude < 0.1f)
        {
          GetComponent<NavMeshAgent>().enabled = true;
          m_rb.isKinematic = true;
          OnNavMeshReached.enabled = true;
          m_state = State.Stopped;
        }
        else if (m_rb.velocity.magnitude < targetSpeed)
          m_rb.AddRelativeForce(acceleration * toTarget.normalized, ForceMode.VelocityChange);
        break;
    }
  }

  private void Start()
  {
    m_rb.isKinematic = false;
  }

  private void Awake()
  {
    m_rb = GetComponent<Rigidbody>();
    m_rb.isKinematic = true;
  }
}
