using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AAVehicle: MonoBehaviour
{
  public enum DefaultBehavior
  {
    Nothing,
    Scan,
    Patrol
  }

  public MoveTo moveTo;
  public Patrol patrol;
  public Follow follow;
  public Scan scan;
  public Track track;
  public ResetTurret resetTurret;
  public DefaultBehavior defaultBehavior = DefaultBehavior.Nothing;
  public float startEngagingDistance = 1f;
  public float stopEngagingDistance = 2;
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
    foreach (MonoBehaviour behavior in m_allBehaviors)
    {
      behavior.enabled = false;
    }
  }

  private void EnableDefaultBehavior()
  {
    switch (defaultBehavior)
    {
      case DefaultBehavior.Scan:
        scan.enabled = true;
        break;
      case DefaultBehavior.Patrol:
        patrol.enabled = true;
        break;
    }
  }

  private void FixedUpdate()
  {
    float distance = MathHelpers.GroundVector(transform.position - m_target.position).magnitude;

    if (distance < startEngagingDistance)
    {
      DisableNavigationBehaviorsExcept(follow);
      follow.enabled = true;
    }
    else if (distance > stopEngagingDistance && follow.enabled == true)
    {
      // Return back to starting position and resume default behavior
      DisableAllBehaviors();
      resetTurret.enabled = true;
      moveTo.enabled = true;
      moveTo.Move(m_homePosition,
        () =>
        {
          DisableAllBehaviors();
          
          // Do this again to ensure turret has been reset and hand off to
          // default state only when this is done
          resetTurret.enabled = true;
          resetTurret.OnComplete = () => { Debug.Log("ResetTurret complete"); EnableDefaultBehavior(); };
        });
    }

    /*
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
    */
  }

  private void Start()
  {
    moveTo = GetComponent<MoveTo>();
    patrol = GetComponent<Patrol>();
    follow = GetComponent<Follow>();
    follow.target = Camera.main.transform;
    scan = GetComponent<Scan>();
    track = GetComponent<Track>();
    track.target = Camera.main.transform;
    resetTurret = GetComponent<ResetTurret>();

    m_allBehaviors = new MonoBehaviour[] { moveTo, patrol, follow, scan, track, resetTurret };
    m_navigationBehaviors = new MonoBehaviour[] { moveTo, patrol, follow };
    DisableAllBehaviors();
    EnableDefaultBehavior();

    m_homePosition = transform.position;

    //TODO: add a SetTarget() method
    m_target = Camera.main.transform;
  }
}
