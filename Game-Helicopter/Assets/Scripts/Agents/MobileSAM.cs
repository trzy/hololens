using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MobileSAM: MonoBehaviour
{
  public enum StopFollowBehavior
  {
    StayPut,
    ReturnToHome,
    Patrol,
  }

  public MoveTo moveTo;
  public Patrol2 patrol;
  public Follow follow;
  public Scan scan;
  public Track track;
  public StopFollowBehavior stopFollowBehavior = StopFollowBehavior.StayPut;
  public float startFollowDistance = 1f;
  public float stopFollowDistance = 2;
  public float startTrackDistance = 2;
  public float stopTrackDistance = 3;

  private MonoBehaviour[] m_allBehaviors;
  private MonoBehaviour[] m_navigationBehaviors;
  private Vector3 m_homePosition;
  private Transform m_target = null;

  private void DisableNavigationBehaviorsExcept(MonoBehaviour doNotDisable = null)
  {
    foreach (MonoBehaviour behavior in m_navigationBehaviors)
    {
      if (behavior != doNotDisable)
        behavior.enabled = false;
    }
  }

  private void DisableAllBehaviors()
  {
    foreach (MonoBehaviour behavior in m_navigationBehaviors)
    {
      behavior.enabled = false;
    }
  }

  private void FixedUpdate()
  {
    float distance = MathHelpers.GroundVector(transform.position - m_target.position).magnitude;

    if (distance < startFollowDistance)
    {
      DisableNavigationBehaviorsExcept(follow);
      follow.enabled = true;
    }
    else if (distance > stopFollowDistance && follow.enabled == true)
    {
      switch (stopFollowBehavior)
      {
        case StopFollowBehavior.StayPut:
        default:
          DisableNavigationBehaviorsExcept();
          break;
        case StopFollowBehavior.ReturnToHome:
          DisableNavigationBehaviorsExcept(moveTo);
          moveTo.enabled = true;
          moveTo.Move(m_homePosition);
          break;
        case StopFollowBehavior.Patrol:
          DisableNavigationBehaviorsExcept(patrol);
          patrol.enabled = true;
          break;
      }
    }

    if (distance < startTrackDistance)
    {
      track.enabled = true;
      scan.enabled = false;
    }
    else if (distance > stopTrackDistance)
    {
      track.enabled = false;
      scan.enabled = true;
    }
  }

  private void Start()
  {
    moveTo = GetComponent<MoveTo>();
    patrol = GetComponent<Patrol2>();
    follow = GetComponent<Follow>();
    follow.target = Camera.main.transform;
    scan = GetComponent<Scan>();
    track = GetComponent<Track>();
    track.target = Camera.main.transform;

    m_allBehaviors = new MonoBehaviour[] { moveTo, patrol, follow, scan, track };
    m_navigationBehaviors = new MonoBehaviour[] { moveTo, patrol, follow };
    DisableAllBehaviors();

    m_homePosition = transform.position;

    //TODO: add a SetTarget() method
    m_target = Camera.main.transform;
  }
}
