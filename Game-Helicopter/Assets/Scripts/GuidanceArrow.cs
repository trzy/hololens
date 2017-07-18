/*
 * TODO:
 * -----
 * 1. Add a different mesh type.
 * 2. Make arrow object animate itself rather than doing it here.
 * 3. Add callbacks for when object becomes visible.
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GuidanceArrow: MonoBehaviour
{
  // What to do with arrow when the target is visible by user
  public enum TargetVisibleBehavior
  {
    Invisible,
    ArrowOnHUD,
    PinArrowInWorldSpace
  }

  // How to transition from world space back to HUD
  public enum WorldSpaceToHUDTransition
  {
    Instantaneous,
    Smooth
  }

  public enum TweenFunction
  {
    Linear,
    Sigmoid
  }

  [Tooltip("Arrow object, where local +z faces camera and +y is pointing direction.")]
  public GameObject arrow;

  [Tooltip("Target object to track.")]
  public Transform target;

  [Tooltip("Distance of HUD plane from user's eye.")]
  public float HUDDistance = 2;

  [Tooltip("Behavior when target is within user's view.")]
  public TargetVisibleBehavior targetVisibleBehavior = TargetVisibleBehavior.ArrowOnHUD;

  [Tooltip("How to transition from world space back to HUD.")]
  public WorldSpaceToHUDTransition worldSpaceToHUDTransition = WorldSpaceToHUDTransition.Instantaneous;

  [Tooltip("Always face camera when on-screen and pinned to target object. A reason not to enable this would be when using a volumetric arrow mesh.")]
  public bool billboardInWorldSpace = true;

  [Tooltip("Tweening function to use for smooth transitions to and from world space.")]
  public TweenFunction tweenFunction = TweenFunction.Linear;

  [Tooltip("Duration of smooth transition in seconds.")]
  public float transitionDuration = 1;

  //TODO: arrow mesh object should handle this
  public float bounceAmplitude = 0.25f;
  public float bounceFrequency = 2;
  public float numberOfBounces = 3;
  public float timeBetweenBounces = 2;

  // Called in frames that target transitions from invisible to visible state
  // (if world space pinning is enabled, this also signals the start of the 
  // transition animation)
  public System.Action OnTargetAppeared = null;

  // Called only when arrow is pinned in world space and first reaches target
  // point
  public System.Action OnTargetReached = null;

  // Called in frames that target transitions from visible to invisible state
  public System.Action OnTargetDisappeared = null;

  private enum State
  {
    HUD,
    PinnedInWorldSpace
  }

  private State m_state = State.HUD;
  private bool m_wasTargetVisibleLastFrame = false;
  private float m_transitionStartTime;
  private Vector3 m_transitionStartPosition;
  private bool m_pinned = false;
  private float m_orientation = 0;
  private Vector3 m_arrowLocalPosition;
  private float m_nextBounceTime;

  private bool IsPointVisible(Vector3 point)
  {
    Vector3 vpPoint = Camera.main.WorldToViewportPoint(point);
    if (vpPoint.z < Camera.main.nearClipPlane)
      return false;
    if (vpPoint.x < 0 || vpPoint.x > 1)
      return false;
    if (vpPoint.y < 0 || vpPoint.y > 1)
      return false;
    return true;
  }

  private Vector3 ClipXY(Vector3 point, float t)
  {
    if (t >= 0 && t <= 1)
    {
      point.x *= t;
      point.y *= t;
    }
    return point;
  }

  private Vector3 Project(Vector3 worldPoint, float zDistance)
  {
    Vector3 point = Camera.main.transform.InverseTransformPoint(worldPoint);
    float zScale = zDistance / point.z;

    // If point is behind us, we also need to flip x and y, hence the Sign()
    Vector3 projected = Mathf.Sign(zScale) * zScale * new Vector3(point.x, point.y, 0);
    projected.z = point.z;
    return projected;
  }

  private void AimAtTarget()
  {
    // This function assumes we are parented to the camera
    Vector3 point = Project(target.position, transform.localPosition.z);
    Vector3 pointXY = new Vector3(point.x, point.y, 0);
    transform.localRotation = Quaternion.LookRotation(Vector3.forward, pointXY);

    // If point is behind us, we need to push it to screen boundaries. This
    // also happens to smooth out the undesirable behavior caused by the
    // singularity near z=0.
    if (point.z < Camera.main.nearClipPlane)
      point += 1e6f * pointXY.normalized;

    // The camera aspect ratio for HoloLens seems to be incorrect and produces
    // horizontal extents that are slightly too large
    float fovY = Camera.main.fieldOfView * 0.9f;
    float fovX = fovY * Camera.main.aspect;
    float xExtent = transform.localPosition.z * Mathf.Tan(0.5f * fovX * Mathf.Deg2Rad);
    float yExtent = transform.localPosition.z * Mathf.Tan(0.5f * fovY * Mathf.Deg2Rad);

    // Clip Y and X to screen extents.
    // Vector equation of line: p = v * t, where t = [0,1].
    // Solve for p.y = yclip: yclip = v.y * tclip. Then, xclip = v.x * tclip.
    point = ClipXY(point, yExtent / point.y);
    point = ClipXY(point, -yExtent / point.y);
    point = ClipXY(point, xExtent / point.x);
    point = ClipXY(point, -xExtent / point.x);
    point.z = transform.localPosition.z;

    // TODO: handle NaNs that occur at z=0?
    if (!MathHelpers.IsNaN(point))
      transform.localPosition = point;
  }

  private void ScheduleNextBounceAnimation(float now)
  {
    m_nextBounceTime = now + timeBetweenBounces;
  }

  private void Bounce(float now)
  {
    //TODO: rather than oscillate about position, maybe should just always be a positive displacement
    //      (no negative values)

    float t = now - m_nextBounceTime;
    if (t < 0)
      return;

    float bouncePeriod = 1 / bounceFrequency;
    if (t > numberOfBounces * bouncePeriod)
    {
      ScheduleNextBounceAnimation(now);
      return;
    }

    float bounceOffset = bounceAmplitude * Mathf.Sin(2 * Mathf.PI * t * bounceFrequency);
    arrow.transform.localPosition = m_arrowLocalPosition + Vector3.up * bounceOffset;
  }

  private void UpdateHUD(float now)
  {
    Bounce(now);
    AimAtTarget();
    //Debug.Log("visible=" + IsPointVisible(target.position));
  }

  // Returns a number between 0 and 1 (indicating completeness) as a function
  // of the duration since transition start time. 
  private float TweenTransition(float now)
  {
    float delta = now - m_transitionStartTime;

    switch (tweenFunction)
    {
      case TweenFunction.Linear:
        // Linear, clamped to 1
        return Mathf.Min(delta / transitionDuration, 1);
      case TweenFunction.Sigmoid:
        // TODO: implement me!
        return 1;
    }

    return 1;
  }

  private void LateUpdate()
  {
    bool isTargetVisible = IsPointVisible(target.position);

    // Handle appeared/disappeared callbacks
    if (isTargetVisible != m_wasTargetVisibleLastFrame)
    {
      if (isTargetVisible)
        OnTargetAppeared();
      else
        OnTargetDisappeared();
    }
    m_wasTargetVisibleLastFrame = isTargetVisible;

    // Hide arrow if requested when target is visible
    if (isTargetVisible && targetVisibleBehavior == TargetVisibleBehavior.Invisible)
    {
      arrow.SetActive(false);
      return;
    }
    arrow.SetActive(true);

    // Update arrow behavior
    float now = Time.time;
    switch (m_state)
    {
      // Arrow is projected onto a HUD plane, which is a fixed distance from
      // the camera and always facing the user
      case State.HUD:
        UpdateHUD(now);

        if (isTargetVisible)
        {
          if (targetVisibleBehavior == TargetVisibleBehavior.PinArrowInWorldSpace)
          {
            // Transition to absolute positioning. Save arrow's orientation, un-
            // parent from camera, and begin transition animation.
            m_orientation = transform.rotation.eulerAngles.z;
            transform.parent = null;
            m_transitionStartTime = now;
            m_transitionStartPosition = transform.position;
            m_pinned = false;
            m_state = State.PinnedInWorldSpace;
          }
        }
        else
        {
          if (worldSpaceToHUDTransition == WorldSpaceToHUDTransition.Smooth)
          {
            float distanceFromHUDPlane = Mathf.Abs(transform.localPosition.z - HUDDistance);
            if (distanceFromHUDPlane > 1e-3f)
            {
              // Gradually move arrow along Camera's forward axis (z) until it is
              // within the HUD plane
              float completeness = TweenTransition(now);
              float z = m_transitionStartPosition.z + (HUDDistance - m_transitionStartPosition.z) * completeness;
              transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, z);
            }
          }
        }
        break;

      // Arrow takes a position in absolute world space
      case State.PinnedInWorldSpace:
        if (billboardInWorldSpace)
        {
          // Orient toward camera and then use saved orientation from last HUD
          // state frame to rotate the arrow into a fixed orientation pointing
          // at the target
          Vector3 cameraToObject = Camera.main.transform.position - transform.position;
          transform.rotation = Quaternion.LookRotation(-cameraToObject);
          transform.Rotate(new Vector3(0, 0, m_orientation));
        }

        if (isTargetVisible)
        {
          if ((transform.position - target.position).sqrMagnitude > (1e-3f * 1e-3f))
          {
            // Gradually move arrow toward its final target
            float completeness = TweenTransition(now);
            transform.position = m_transitionStartPosition + completeness * (target.position - m_transitionStartPosition);
          }
          else if (!m_pinned && OnTargetReached != null)
          {
            OnTargetReached();
            m_pinned = true;
          }
        }
        else
        {
          // Transition back to HUD plane
          transform.parent = Camera.main.transform;
          m_transitionStartTime = now;
          m_transitionStartPosition = transform.localPosition;
          if (worldSpaceToHUDTransition == WorldSpaceToHUDTransition.Instantaneous)
            transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, HUDDistance);
          m_state = State.HUD;
        }
        break;
    }
  }

  private void OnEnable()
  {
    m_arrowLocalPosition = arrow.transform.localPosition;
    ScheduleNextBounceAnimation(Time.time);
  }

  private void Awake()
  {
    HUDDistance = transform.localPosition.z;
    transform.parent = Camera.main.transform;
    //TEMP testing
    OnTargetAppeared = () => { Debug.Log("TARGET APPEARED"); };
    OnTargetReached = () => { Debug.Log("TARGET REACHED"); };
    OnTargetDisappeared = () => { Debug.Log("TARGET DISAPPEARED"); };
  }
}