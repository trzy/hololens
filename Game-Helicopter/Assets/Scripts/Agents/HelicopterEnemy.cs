using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(HelicopterAutopilot))]
public class HelicopterEnemy : MonoBehaviour
{
  public Transform target;

  private HelicopterAutopilot m_autopilot;
  private float m_boundingRadius;

  private enum State
  {
    Invalid,
    IdleDecide,
    IdleOrbit,
    Idle,
    EngageDecide,
    WaitForCompletion
  }

  private State m_state = State.IdleDecide;
  private State m_prevState = State.Invalid;
  private float m_idleStartTime = 0;
  bool m_engagingTarget = false;

  private void DrawLine(Vector3[] points)
  {
    LineRenderer lr = GetComponent<LineRenderer>();
    lr.positionCount = points.Length;
    for (int i = 0; i < points.Length; i++)
    {
      lr.SetPosition(i, points[i]);
    }
  }

  private bool IsPathBlocked(Vector3 destination)
  {
    Vector3 toDestination = destination - transform.position;
    float distance = toDestination.magnitude;
    Ray ray = new Ray(transform.position + toDestination.normalized * m_boundingRadius, toDestination);
    RaycastHit hit;
    bool result = Physics.SphereCast(ray, m_boundingRadius, out hit, distance);
    if (result)
      Debug.Log("HIT " + hit.collider.name);
    return result;
    //return Physics.SphereCast(ray, m_boundingRadius, distance);
  }

  private float FindClearance(Vector3 position, Vector3 direction, float maxDistance = 4)
  {
    Ray ray = new Ray(position, direction);
    RaycastHit hit;
    if (Physics.Raycast(ray, out hit, maxDistance))
      return (hit.point - position).magnitude;
    return maxDistance;
  }

  private bool TryAttackPatternVerticalAndBehind(Vector3 toTarget, Vector3 verticalDirection, System.Action OnComplete)
  {
    float minDistanceVertical = 2 * m_boundingRadius; // minimum distance above/beneath target that must be clear
    float minDistanceBehind = 2* m_boundingRadius;    // minimum distance behind the target that must be clear

    Vector3[] positions = new Vector3[2];

    // Do we have enough clearance directly above/below to fly through?
    float clearance = FindClearance(target.position, verticalDirection);
    if (clearance < minDistanceVertical)
    {
      Debug.Log("VERTICAL: NO CLEARANCE: " + clearance);
      return false;
    }

    // Choose a point halfway between the minimum vertical distance and
    // clearance
    float mid = 0.5f * (minDistanceVertical + clearance);
    positions[0] = target.position + verticalDirection * mid;
    if (IsPathBlocked(positions[0]))
    {
      Debug.Log("VERTICAL: PATH BLOCKED");
      return false;
    }

    // Perform raycasts in a semicircle behind (and at same altitude as) target
    Vector3 directionBehind = MathHelpers.Azimuthal(toTarget).normalized;
    Vector3 bestDirection = Vector3.zero;
    float bestClearance = 0;
    for (float angle = -90; angle < 90; angle += 20)
    {
      Vector3 direction = Quaternion.Euler(angle * Vector3.up) * directionBehind;
      clearance = FindClearance(target.position, direction);
      if (clearance > bestClearance)
      {
        bestDirection = direction;
        bestClearance = clearance;
      }
    }

    // Check if sufficient room to go behind
    if (bestClearance < minDistanceBehind)
    {
      Debug.Log("REAR: NO CLEARANCE: " + bestClearance);
      return false;
    }

    // Determine point, ideally in between the min and max distance
    mid = 0.5f * (minDistanceBehind + bestClearance);
    positions[1] = target.position + bestDirection * mid;
    if (IsPathBlocked(positions[1]))
    {
      Debug.Log("REAR: PATH BLOCKED");
      return false;
    }

    Debug.Log("LAUNCHING ATTACK PATTERN");
    DrawLine(new Vector3[] { transform.position, positions[0], positions[1] });
    m_autopilot.FollowPathAndLookAt(positions, target, OnComplete);
    return true;
  }

  private bool TryAttackPatternUnderAndBehind(Vector3 toTarget, System.Action OnComplete = null)
  {
    return TryAttackPatternVerticalAndBehind(toTarget, -Vector3.up, OnComplete);
  }

  private bool TryAttackPatternAboveAndBehind(Vector3 toTarget, System.Action OnComplete = null)
  {
    return TryAttackPatternVerticalAndBehind(toTarget, Vector3.up, OnComplete);
  }

  private bool TryAttackPatternStrafe(float altitude, System.Action OnComplete = null)
  {
    Vector3 position = transform.position;
    position.y = altitude;
    Vector3 right = MathHelpers.Azimuthal(transform.right);
    float d1 = FindClearance(transform.position, right, 2) - 2 * m_boundingRadius;
    float d2 = FindClearance(transform.position, -right, 2) - 2 * m_boundingRadius;
    Vector3[] waypoints = new Vector3[2];
    int firstIdx = Random.Range(0, 2) == 0 ? 0 : 1;
    waypoints[firstIdx ^ 0] = position + right * d1;
    waypoints[firstIdx ^ 1] = position - right * d2;
    m_autopilot.FollowPathAndLookAt(waypoints, target, OnComplete);
    DrawLine(waypoints);
    return true;
  }

  private bool TryBackOffPattern(Vector3 toTarget)
  {
    //TODO: margin represents, roughly, the diameter of a bounding sphere
    // around us. This should probably be computed at start up from the 
    // collider footprint.
    float margin = 0.75f;

    // Measure clearance behind us
    Vector3 back = -MathHelpers.Azimuthal(toTarget);
    Ray ray = new Ray(transform.position, back);
    RaycastHit hit;
    float clearance = 0;
    if (Physics.Raycast(ray, out hit, 10f))
      clearance = (hit.point - transform.position).magnitude;
    else
      clearance = 10;

    // Is there enough?
    if (clearance - margin <= 0)
      return false;

    // If so, move back some random distance but at least halfway to maximum
    float minDistance = 0.5f * (margin + (clearance - margin));
    float maxDistance = clearance - margin;
    float distance = UnityEngine.Random.Range(minDistance, maxDistance);
    m_autopilot.FlyTo(transform.position + distance * back, target);
    DrawLine(new Vector3[] { transform.position, transform.position + distance * back });
    return true;
  }

  private void FixedUpdate()
  {
    if (target == null)
      return;

    Vector3 toTarget = target.position - transform.position;
    float distanceToTarget = toTarget.magnitude;

    State currentState = m_state;
    bool enteredState = currentState != m_prevState;
    switch (currentState)
    {
      default:
      case State.IdleDecide:
        m_autopilot.Halt();
        m_state = Random.Range(0, 2) == 0 ? State.IdleOrbit : State.Idle;
        break;
      case State.IdleOrbit:
        m_autopilot.Orbit(transform.position, transform.position.y + 0, 1f);
        m_autopilot.throttle = 0.25f;
        m_state = State.Idle;
        break;
      case State.Idle:
        if (enteredState)
          m_idleStartTime = Time.time;
        else if (Time.time - m_idleStartTime > 10)
          m_state = State.IdleDecide;
        else
        {
          // Do we need to engage the target?
          if (distanceToTarget < 1.5f)
            m_state = State.EngageDecide;
        }
        break;
      case State.EngageDecide:
        int decision = 0;// Random.Range(0, 7);
        //TODO next: strafe left/right pattern, attack multiple points pattern (fly to left of target, then front, then right),
        // attack single point, orbit once
        //TODO next: collisions should finish autopilot behavior
        //TODO next: collisions with spatial mesh should produce Jungle Strike-like bounce effect
        //TODO next: add gun firing and missiles -- may need to make helicopter pitch and roll less for accurate firing
        switch (decision)
        {
          default:
          case 0:
            Debug.Log("ENGAGE: Follow");
            m_autopilot.Follow(target, 1.75f, () => m_state = State.EngageDecide);
            m_autopilot.throttle = 0.6f;
            m_state = State.WaitForCompletion;
            break;
          case 1:
            Debug.Log("ENGAGE: Hover-and-match-altitude");
            m_autopilot.MatchAltitudeAndLookAt(target, () => m_state = State.EngageDecide);
            m_autopilot.throttle = 0.35f;
            m_state = State.WaitForCompletion;
            break;
          case 2:
            Debug.Log("ENGAGE: Hover");
            m_autopilot.HoverAndLookAt(target, () => m_state = State.EngageDecide);
            m_autopilot.throttle = 1;
            m_state = State.WaitForCompletion;
            break;
          case 3:
            Debug.Log("ENGAGE: Above-and-behind");
            if (TryAttackPatternAboveAndBehind(toTarget, () => m_state = State.EngageDecide))
            {
              m_autopilot.throttle = 0.75f;
              m_state = State.WaitForCompletion;
              return;
            }
            break;
          case 4:
            Debug.Log("ENGAGE: Under-and-behind");
            if (TryAttackPatternUnderAndBehind(toTarget, () => m_state = State.EngageDecide))
            {
              m_autopilot.throttle = 0.75f;
              m_state = State.WaitForCompletion;
              return;
            }
            break;
          case 5:
            Debug.Log("ENGAGE: Strafe Player Altitude");
            if (TryAttackPatternStrafe(target.position.y, () => m_state = State.EngageDecide))
            {
              m_autopilot.throttle = 0.75f;
              m_state = State.WaitForCompletion;
            }
            break;
          case 6:
            Debug.Log("ENGAGE: Strafe Self Altitude");
            if (TryAttackPatternStrafe(transform.position.y, () => m_state = State.EngageDecide))
            {
              m_autopilot.throttle = 0.75f;
              m_state = State.WaitForCompletion;
            }
            break;
        }
        break;
      case State.WaitForCompletion:
        break;
    }
    m_prevState = currentState;
  }

  private void OldFixedUpdate()
  {
    if (target == null)
      return;

    Vector3 toTarget = target.position - transform.position;
    float distanceToTarget = toTarget.magnitude;

    if (!m_engagingTarget)
    {
      if (distanceToTarget < 1.5f)
      {
        m_engagingTarget = true;
        m_autopilot.Halt();
      }
      else if (!m_autopilot.flying)
      {
        // Doing nothing, start circling in place
        m_autopilot.Orbit(transform.position, transform.position.y + 0.5f, 1f);
        m_autopilot.throttle = 0.25f;
      }
    }
    else
    {
      if (!m_autopilot.flying)
      {
        // Not currently flying, select an attack pattern based on distance to target
        if (distanceToTarget > 2.5f)
        {
          // Circle the target when it is up close
          //TODO: callback should take number of revolutions completed
          //m_autopilot.OrbitAndLookAt(target.transform, 0, 1.5f, 10);
          //m_autopilot.throttle = 3;

          if (TryAttackPatternAboveAndBehind(toTarget))
            m_autopilot.throttle = 1.5f;
          else
            m_autopilot.throttle = 1;

          //TODO: additional attack patterns: strafe left/right (up/down?), circle about a point that is in front of target, random points  in front hemisphere of target
          //TODO: If altitude mismatch, attempt correction by selecting a point on the semicircle directly in front of target (actually, on the side of the target closest
          //      to us
        }
        else
        {
          // Attack patterns: back off, strafe
          //TryBackOffPattern(toTarget);
          m_autopilot.throttle = 1;
        }
      }
      else
      {
        // Currently flying. Attack target and change flight patterns
        // periodically.
      }
    }
  }

  private void Start()
  {
    //m_autopilot.Orbit(Camera.main.transform.position, 2, 0.5f);
  }

  private void Awake()
  {
    m_autopilot = GetComponent<HelicopterAutopilot>();
    m_boundingRadius = Footprint.BoundingRadius(gameObject);
    Debug.Log("Bounding radius: " + m_boundingRadius);
  }
}