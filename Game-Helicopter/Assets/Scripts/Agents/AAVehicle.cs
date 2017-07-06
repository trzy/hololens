using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AAVehicle: MonoBehaviour
{
  public enum DefaultNavigationBehavior
  {
    Nothing,
    Patrol
  }

  public enum DefaultTurretBehavior
  {
    Nothing,
    Scan,
    Track
  }

  public MoveTo moveTo;
  public Patrol patrol;
  public Follow follow;
  public Scan scan;
  public Track track;
  public ResetTurret resetTurret;
  public DefaultNavigationBehavior defaultNavigationBehavior = DefaultNavigationBehavior.Nothing;
  public DefaultTurretBehavior defaultTurretBehavior = DefaultTurretBehavior.Nothing;
  public float startEngagingDistance = 1f;
  public float stopEngagingDistance = 2;
  public float startTrackDistance = 2;
  public float stopTrackDistance = 3;

  private MonoBehaviour[] m_navigationBehaviors;
  private MonoBehaviour[] m_turretBehaviors;
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

  private void DisableTurretBehaviorsExcept(MonoBehaviour doNotDisable = null)
  {
    foreach (MonoBehaviour behavior in m_turretBehaviors)
    {
      if (behavior != doNotDisable)
        behavior.enabled = false;
    }
  }

  private void EnableDefaultNavigationBehavior()
  {
    switch (defaultNavigationBehavior)
    {
      default:
      case DefaultNavigationBehavior.Nothing:
        break;
      case DefaultNavigationBehavior.Patrol:
        patrol.enabled = true;
        break;
    }
  }

  private void EnableDefaultTurretBehavior()
  {
    switch (defaultTurretBehavior)
    {
      default:
        break;
      case DefaultTurretBehavior.Scan:
        scan.enabled = true;
        break;
      case DefaultTurretBehavior.Track:
        track.enabled = true;
        break;
    }
  }

  private void FixedUpdate()
  {
    float distance = MathHelpers.Azimuthal(transform.position - m_target.position).magnitude;

    if (distance < startEngagingDistance)
    {
      DisableNavigationBehaviorsExcept(follow);
      follow.enabled = true;
    }
    else if (distance > stopEngagingDistance && follow.enabled == true)
    {
      // Return back to starting position and resume default behavior
      DisableNavigationBehaviorsExcept();
      moveTo.enabled = true;
      moveTo.Move(m_homePosition, () => { EnableDefaultNavigationBehavior(); });
    }

    if (distance < startTrackDistance)
    {
      DisableTurretBehaviorsExcept(track);
      track.enabled = true;
    }
    else if (distance > stopTrackDistance && track.enabled == true)
    {
      // Reset turret and resume default behavior
      DisableTurretBehaviorsExcept();
      resetTurret.enabled = true;
      resetTurret.OnComplete = () => { EnableDefaultTurretBehavior(); };
    }
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

    m_navigationBehaviors = new MonoBehaviour[] { moveTo, patrol, follow };
    m_turretBehaviors = new MonoBehaviour[] { scan, track, resetTurret };
    DisableNavigationBehaviorsExcept();
    DisableTurretBehaviorsExcept();
    EnableDefaultNavigationBehavior();
    EnableDefaultTurretBehavior();

    m_homePosition = transform.position;

    //TODO: add a SetTarget() method
    m_target = Camera.main.transform;
  }
}
