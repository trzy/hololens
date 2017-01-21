/*
 * This script should be attached to the gun muzzle point, so that the position
 * is at the point bullets are emitted and the forward direction is along the
 * barrel.
 * 
 * TODO: Eventually, auto-aim should control a gun. The muzzle point will be 
 * at the end of the gun and the script will move the gun itself about some
 * pivot point anchored beneath the helicopter body.
 */
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class AutoAim: MonoBehaviour
{
  [Tooltip("Speed (m/s) at which reticle moves to snap to new target")]
  public float updateSpeed = .01f;

  [Tooltip("Proximity to enemies for auto-aim to take effect")]
  public float proximity = 1.5f;

  [Tooltip("Distance from forward ray within which auto-aim takes effect")]
  public float rayProximity = 0.5f;

  public GameObject reticlePrefab = null;

  private TargetingReticle m_reticle = null;
  private Vector3 m_reticleWorldPosition;
  private bool m_smoothUpdate = false;

  private bool InsideProximityRadius(Vector3 position)
  {
    return Vector3.Magnitude(transform.position - position) <= proximity;
  }

  private float DistanceFromAimingRay(Vector3 targetPosition)
  {
    /*
     * Equation of aiming line:
     * 
     *  R = H + t dot F
     *  
     * Where H is helicopter gun position and F is (unit) forward vector along
     * aiming direction. The scalar t is the length along the line and
     * determines the point R.
     * 
     * To find distance to line R from some enemy position P, we want to first
     * find nearest point on line. To do this, note that the angle of a vector
     * (P - R) will be 90 degrees to the line R at its closest point. So we can
     * simply solve:
     * 
     *  (P - R) dot F = 0
     *  
     * This gives:
     * 
     *  t0 = (P dot F) - (H dot F)
     *  
     * And then the distance is simply the magnitude |P - R(t0)|.
     */
    float t = Vector3.Dot(targetPosition, transform.forward) - Vector3.Dot(transform.position, transform.forward);
    Vector3 rayPoint = transform.position + t * transform.forward;
    return Vector3.Magnitude(rayPoint - targetPosition);
  }

  // This function will return targetPoint if the angle of the gun is
  // acceptable, else it returns the netural target position (straight ahead)
  public Vector3 ClampToAllowableRange(Vector3 targetPoint)
  {
    Vector3 neutralTarget = transform.position + transform.forward;

    // Transform aim vector to muzzle-local space and then take projections
    // onto the relevant planes of gun motion
    Vector3 u = Vector3.Normalize(transform.InverseTransformVector(targetPoint - transform.position));
    Vector3 xz = Vector3.Normalize(new Vector3(u.x, 0, u.z));
    Vector3 yz = Vector3.Normalize(new Vector3(0, u.y, u.z));
    Debug.Log("xz=" + xz.ToString("F2") + ", yz=" + yz.ToString("F2"));

    // Cannot aim gun above the horizontal point or below 45 degrees 
    if (yz.y > 0 || yz.y < -0.707f)
    {
      return neutralTarget;
    }

    // Cannot aim more than 45 degrees left or right
    if (xz.x > 0.707f || xz.x < -0.707f)
    {
      return neutralTarget;
    }

    return targetPoint;
  }

  // Should be called each frame
  public Vector3 UpdateReticle()
  {
    Vector3 defaultTargetPoint = transform.position + transform.forward;
    Vector3 targetPoint;

    // Identify the closest target, if any exists
    GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
    List<Tuple<float, GameObject>> targets = new List<Tuple<float, GameObject>>(8);
    foreach (GameObject enemy in enemies)
    {
      if (InsideProximityRadius(enemy.transform.position))
      {
        float distanceToRay =  DistanceFromAimingRay(enemy.transform.position);
        if (distanceToRay <= rayProximity)
        {
          targets.Add(new Tuple<float, GameObject>(distanceToRay, enemy));
        }
      }
    }
    if (targets.Count > 0)
    {
      targets.Sort((target1, target2) => Math.Sign(target1.first - target2.first));
      targetPoint = ClampToAllowableRange(targets[0].second.transform.position);
      m_smoothUpdate = true;
    }
    else
    {
      targetPoint = defaultTargetPoint;
    }

    // Smooth motion when acquiring or releasing target
    Vector3 positionThisFrame = targetPoint;
    if (m_smoothUpdate)
    {
      Vector3 direction = targetPoint - m_reticleWorldPosition;
      if (direction != Vector3.zero)
      {
        float distance = Vector3.Magnitude(direction);
        float moveBy = Mathf.Min(updateSpeed * Time.deltaTime, distance);
        Vector3 unitDirection = direction / distance;
        positionThisFrame = m_reticleWorldPosition + moveBy * unitDirection;
      }

      // If we're "releasing" a target and moving back to the default, forward
      // aim position, snap to it when close enough and halt smooth updates
      if (Vector3.Distance(positionThisFrame, defaultTargetPoint) <= .01f)
      {
        m_smoothUpdate = false;
      }
    }
    m_reticleWorldPosition = positionThisFrame;
    m_reticle.transform.position = positionThisFrame;
    m_reticle.transform.rotation = transform.rotation;
    return positionThisFrame;
  }

  private void Awake()
  {
    GameObject reticle = Instantiate(reticlePrefab) as GameObject;
    m_reticle = reticle.GetComponent<TargetingReticle>();
    reticle.transform.parent = null;
    m_reticleWorldPosition = transform.position + transform.forward;
    reticle.transform.position = m_reticleWorldPosition;
  }
}
