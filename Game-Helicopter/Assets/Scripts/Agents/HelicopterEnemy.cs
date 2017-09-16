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
    Thinking,
    FlyingTowards,
    BackingAway
  }

  private State m_state = State.Thinking;
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
    Ray ray = new Ray(target.position, direction);
    RaycastHit hit;
    if (Physics.Raycast(ray, out hit, maxDistance))
      return (hit.point - target.position).magnitude;
    return maxDistance;
  }

  private bool TryAttackPatternVerticalAndBehind(Vector3 toTarget, Vector3 verticalDirection)
  {
    float minDistanceVertical = 2 * m_boundingRadius; // minimum distance above/beneath target that must be clear
    float minDistanceBehind = 2* m_boundingRadius;    // minimum distance behind the target that must be clear

    Vector3[] positions = new Vector3[2];

    // Do we have enough clearance directly above/below to fly through?
    float clearance = FindClearance(transform.position, verticalDirection);
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
    m_autopilot.FollowPathAndLookAt(positions, target);
    return true;
  }

  bool TryAttackPatternUnderAndBehind(Vector3 toTarget)
  {
    return TryAttackPatternVerticalAndBehind(toTarget, -Vector3.up);
  }

  bool TryAttackPatternAboveAndBehind(Vector3 toTarget)
  {
    return TryAttackPatternVerticalAndBehind(toTarget, Vector3.up);
  }

  bool TryBackOffPattern(Vector3 toTarget)
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

  bool TryMatchAltitudePattern(Vector3 toTarget)
  {
    return true;
  }

  private void FixedUpdate()
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

    switch (m_state)
    {
      case State.Thinking:


        /*
        if (distanceToTarget > 2)
        {
          m_autopilot.Follow(target, 2f, 60 * 2, 
            () =>
            {
              Debug.Log("Caught target!");
              m_state = State.Thinking;
            });
          m_state = State.FlyingTowards;
        }
        else if (distanceToTarget < 1.5f)
        {
          // Keep our distance!
          Vector3 awayFromTarget = (transform.position - target.position).normalized;
          m_autopilot.FlyTo(target.position + 2 * awayFromTarget, target, 10, 
            () =>
            {
              Debug.Log("Backed off");
            });
          m_state = State.BackingAway;
        }
        */


        m_state = State.FlyingTowards;
        break;
      case State.FlyingTowards:

        break;
      default:
        break;
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