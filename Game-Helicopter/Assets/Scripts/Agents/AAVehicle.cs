using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MoveTo))]
[RequireComponent(typeof(Patrol))]
[RequireComponent(typeof(Follow))]
[RequireComponent(typeof(Scan))]
[RequireComponent(typeof(Track))]
[RequireComponent(typeof(ResetTurret))]
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
  public MonoBehaviour fire;
  public DefaultNavigationBehavior defaultNavigationBehavior = DefaultNavigationBehavior.Nothing;
  public DefaultTurretBehavior defaultTurretBehavior = DefaultTurretBehavior.Nothing;
  public float startFollowingDistance = 1f;
  public float stopFollowingDistance = 2;
  public float startTrackDistance = 2;
  public float stopTrackDistance = 3;
  
  private MonoBehaviour[] m_navigationBehaviors;
  private MonoBehaviour[] m_turretBehaviors;
  private Vector3 m_homePosition;

  private Transform m_target = null;

  private bool m_lockedOnTarget = false;

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

    if (distance < startFollowingDistance)
    {
      DisableNavigationBehaviorsExcept(follow);
      follow.enabled = true;
    }
    else if (distance > stopFollowingDistance && follow.enabled == true)
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
      track.OnLockObtained =
        () =>
        {
          m_lockedOnTarget = true;
          fire.enabled = true;
        };
      track.OnLockLost =
        () =>
        {
          m_lockedOnTarget = false;
          fire.enabled = false;
        };
    }
    else if (distance > stopTrackDistance && track.enabled == true)
    {
      // Reset turret, stop firing, and resume default behavior
      DisableTurretBehaviorsExcept();
      resetTurret.enabled = true;
      resetTurret.OnComplete = () => { EnableDefaultTurretBehavior(); };
      fire.enabled = false;
      m_lockedOnTarget = false;
    }
  }

  private void Start()
  {
    /*
    moveTo = GetComponent<MoveTo>();
    patrol = GetComponent<Patrol>();
    follow = GetComponent<Follow>();
    */
    follow.target = Camera.main.transform;
    //scan = GetComponent<Scan>();
    //track = GetComponent<Track>();
    track.target = Camera.main.transform;
    //resetTurret = GetComponent<ResetTurret>();
    //fire = GetComponent<MonoBeha>();

    m_navigationBehaviors = new MonoBehaviour[] { moveTo, patrol, follow };
    m_turretBehaviors = new MonoBehaviour[] { scan, track, resetTurret };
    DisableNavigationBehaviorsExcept();
    DisableTurretBehaviorsExcept();
    EnableDefaultNavigationBehavior();
    EnableDefaultTurretBehavior();
    fire.enabled = false;

    m_homePosition = transform.position;

    //TODO: add a SetTarget() method
    m_target = Camera.main.transform;
  }

  private void Awake()
  {
  }
}
