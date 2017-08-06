/*
 * TODO:
 * -----
 * 1. Re-do billboarding to point toward camera (camera pos - object pos)
 * 2. Octant constraint mode?
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GuidanceArrow : MonoBehaviour
{
  public enum OrientationConstraintMode
  {
    None,
    LeftRight,
    Quadrants
  }

  public enum RubberBandMode
  {
    None,
    XYZ,
    XY
  }

  [Tooltip("Arrow object, where local +z faces camera and +y is pointing direction.")]
  public GameObject arrow;

  [Tooltip("Distance of HUD plane from user's eye.")]
  public float HUDDistance = 2;

  [Tooltip("Edge of the viewable space as a percentage of view angles, which determines where indicator is drawn.")]
  public float viewBoundaryFraction = 0.85f;

  [Tooltip("Whether direction indicator should point along precise orientation or be constrained to a particular orientations.")]
  public OrientationConstraintMode orientationConstraint = OrientationConstraintMode.None;

  [Tooltip("If true, always pushes arrow to edge of screen, otherwise does so only if object is behind us.")]
  public bool alwaysAtPeriphery = false;

  [Tooltip("Smoothly change position and orientation. If disabled, arrow will be fixed onto HUD and update instantaneously.")]
  public RubberBandMode rubberBandMode = RubberBandMode.None;

  [Tooltip("Duration of smooth transition in seconds.")]
  public float transitionDuration = 1;

  [Tooltip("Continue to draw arrow even when target appears on-screen.")]
  public bool drawWhenTargetOnScreen = false;

  // Target object to point at
  public Transform target
  {
    get { return m_target; }
    set
    {
      bool update = m_target != value;
      m_target = value;
      if (update)
        OnEnable();
    }
  }

  public Transform debugTargetOverride = null;

  // Called in frames that target transitions from invisible to visible state
  // (if world space pinning is enabled, this also signals the start of the 
  // transition animation)
  public System.Action OnTargetAppeared = null;

  // Called in frames that target transitions from visible to invisible state
  public System.Action OnTargetDisappeared = null;

  private Vector3 m_currentCameraSpacePosition;
  private Vector3 m_desiredCameraSpacePosition;
  private float m_currentOrientation = 0;
  private float m_desiredOrientation = 0;
  private int m_currentOrientationID = 0; // if in orientation-constrained mode, an arbitrary ID describing orientation
  private int m_previousOrientationID = 0;

  private Transform m_target = null;
  private bool m_wasTargetVisibleLastFrame = false;

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

  private Vector3 Project(Vector3 cameraSpacePoint, float zDistance)
  {
    //Vector3 cameraSpacePoint = Camera.main.transform.InverseTransformPoint(worldSpacePoint);
    float zScale = zDistance / cameraSpacePoint.z;

    // If point is behind us, we also need to flip x and y, hence the Sign()
    Vector3 projected = Mathf.Sign(zScale) * zScale * new Vector3(cameraSpacePoint.x, cameraSpacePoint.y, 0);
    projected.z = zDistance;
    return projected;
  }

  private Vector3 RestrictOrientation(out int orientationID, Vector3 point)
  {
    switch (orientationConstraint)
    {
      default:
      case OrientationConstraintMode.None:
        // If there is no orientation constraint, we 
        // the point
        orientationID = 0;
        return point;
      case OrientationConstraintMode.LeftRight:
        orientationID = 1 * (int) Mathf.Sign(point.x);
        return new Vector3(point.x, 0, point.z);
      case OrientationConstraintMode.Quadrants:
        Vector3 direction = point.normalized;
        if (Mathf.Abs(direction.y) > Mathf.Abs(direction.x))
        {
          // Should point either up or down based on sign of y
          orientationID = 2 * (int) Mathf.Sign(point.y);
          return new Vector3(0, point.y, point.z);
        }
        else
        {
          // Either left or right
          orientationID = 1 * (int) Mathf.Sign(point.x);
          return new Vector3(point.x, 0, point.z);
        }
    }
  }

  private void AimAtTarget()
  {
    // The camera aspect ratio for HoloLens seems to be incorrect and produces
    // horizontal extents that are slightly too large
    float fovY = Camera.main.fieldOfView * viewBoundaryFraction;
    float fovX = fovY * Camera.main.aspect;
    float xExtent = HUDDistance * Mathf.Tan(0.5f * fovX * Mathf.Deg2Rad);
    float yExtent = HUDDistance * Mathf.Tan(0.5f * fovY * Mathf.Deg2Rad);

    // Project the target point into camera-local space on a plane at HUD distance
    Vector3 cameraSpacePoint = Camera.main.transform.InverseTransformPoint(target.position);
    Vector3 point = Project(cameraSpacePoint, HUDDistance);

    // Restrict orientation, if called for, to some particular set of allowed
    // directions, and keep track of direction between frames
    m_previousOrientationID = m_currentOrientationID;
    point = RestrictOrientation(out m_currentOrientationID, point);

    // Direction from center point of HUD plane toward target (as projected on HUD)
    Vector3 direction = new Vector3(point.x, point.y, 0);

    // If point is behind us, we need to push it to screen boundaries. This
    // also happens to smooth out the undesirable behavior caused by the
    // singularity near z=0.
    if (alwaysAtPeriphery || cameraSpacePoint.z < Camera.main.nearClipPlane)
      point += 1e6f * direction.normalized;

    // Clip Y and X to screen extents. We take advantage of being in camera
    // space, where (0, 0, z) is directly in front and center.
    // Vector equation of line: p = v * t, where t = [0,1].
    // Solve for p.y = yclip: yclip = v.y * tclip. Then, xclip = v.x * tclip.
    point = ClipXY(point, yExtent / point.y);
    point = ClipXY(point, -yExtent / point.y);
    point = ClipXY(point, xExtent / point.x);
    point = ClipXY(point, -xExtent / point.x);
    point.z = HUDDistance;

    // TODO: handle NaNs that occur at z=0?
    // Transform position back to world space
    if (!MathHelpers.IsNaN(point))
      m_desiredCameraSpacePosition = point;
    else
      m_desiredCameraSpacePosition = new Vector3(0, 0, HUDDistance);

    // Point the arrow at the object and make sure arrow is facing camera and lies
    // in the HUD plane (not quite the same as billboarding, where orientation toward
    // camera changes slightly with XY position).
    // Our convention is that the arrow points along its local y axis and that
    // its z axis should point along the camera's. We must rotate our direction from
    // camera back into world space.
    Vector3 desiredCameraSpaceDirection = direction;
    Quaternion localRotation = Quaternion.LookRotation(Vector3.forward, direction);
    m_desiredOrientation = localRotation.eulerAngles.z;
  }

  private void LateUpdate()
  {
    if (target == null)
    {
      // Ensures that when target is set again, OnTargetAppeared() will be
      // called correctly
      m_wasTargetVisibleLastFrame = false;
      return;
    }

    bool isTargetVisible = IsPointVisible(target.position);

    // Handle appeared/disappeared callbacks
    if (isTargetVisible != m_wasTargetVisibleLastFrame)
    {
      if (isTargetVisible)
      {
        if (OnTargetAppeared != null)
          OnTargetAppeared();
      }
      else
      {
        if (OnTargetDisappeared != null)
          OnTargetDisappeared();
      }
    }
    m_wasTargetVisibleLastFrame = isTargetVisible;

    // Hide arrow if requested when target is visible
    if (isTargetVisible && !drawWhenTargetOnScreen)
    {
      arrow.SetActive(false);
      return;
    }

    // Turn on the arrow
    bool arrowAppearedThisFrame = arrow.activeSelf == false;
    arrow.SetActive(true);

    // Update desired arrow position and orientation
    float now = Time.time;
    AimAtTarget();

    // Billboard
    transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward);

    // If rubber band mode enabled, move smoothly toward desired position, else
    // snap to it immediately
    if (rubberBandMode != RubberBandMode.None)
    {
      // If the arrow was previously invisible, we want to pop in at the
      // desired position/orientation immediately, without lerping. Also, if
      // we are operating in a directionally-constrained mode and the direction
      // has changed, we do not want to lerp between directions and instead 
      // only allow lerping locally (in the same quadrant).
      bool orientationChanged = (orientationConstraint != OrientationConstraintMode.None) && (m_currentOrientationID != m_previousOrientationID);
      bool immediateTransition = arrowAppearedThisFrame || orientationChanged;
      float t = immediateTransition ? 1 : Time.deltaTime / transitionDuration;

      // Set position
      if (rubberBandMode == RubberBandMode.XY)
      {
        // In this mode, we are always affixed to HUD plane and lerp only in
        // the XY plane
        m_currentCameraSpacePosition = Vector3.Lerp(m_currentCameraSpacePosition, m_desiredCameraSpacePosition, t);
        transform.position = Camera.main.transform.TransformPoint(m_currentCameraSpacePosition);
      }
      else
      {
        Vector3 desiredWorldSpacePosition = Camera.main.transform.TransformPoint(m_desiredCameraSpacePosition);
        transform.position = Vector3.Lerp(transform.position, desiredWorldSpacePosition, t);
      }

      // Set orientation -- lerp if unconstrained for smooth transitions,
      // otherwise lock directly into desired orientation
      m_currentOrientation = MathHelpers.ShortestAngleLerp(m_currentOrientation, m_desiredOrientation, t);
      transform.Rotate(new Vector3(0, 0, m_currentOrientation));
    }
    else
    {
      transform.position = Camera.main.transform.TransformPoint(m_desiredCameraSpacePosition);
      transform.Rotate(new Vector3(0, 0, m_desiredOrientation));

      // In case rubber band mode is switched on, we want a sensible starting point
      m_currentCameraSpacePosition = m_desiredCameraSpacePosition;
    }
  }

  private void OnEnable()
  {
    if (debugTargetOverride != null)
      target = debugTargetOverride;

    if (target != null)
    {
      AimAtTarget();
      m_currentCameraSpacePosition = m_desiredCameraSpacePosition;
      m_currentOrientation = m_desiredOrientation;
      if (IsPointVisible(target.position) && !drawWhenTargetOnScreen)
        arrow.SetActive(false);
    }
  }

  private void Awake()
  {
  }
}