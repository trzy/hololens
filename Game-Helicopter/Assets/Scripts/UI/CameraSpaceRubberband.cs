/*
 * Smoothly pins an item (as if affixed with a rubber band) in camera space.
 * The object's local position at startup is taken as the camera-relative
 * position to pin to, regardless of location in hierarchy.
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraSpaceRubberband: MonoBehaviour
{
  public enum RubberBandMode
  {
    None,
    XYZ,
    XY
  }

  [Tooltip("Smoothly change position and orientation. If disabled, arrow will be fixed onto HUD and update instantaneously.")]
  public RubberBandMode rubberBandMode = RubberBandMode.None;

  [Tooltip("Duration of smooth transition in seconds. Set to 0 to pin the object in camera space.")]
  public float transitionDuration = 1;

  private Vector3 m_desiredCameraSpacePosition = Vector3.zero;
  private float m_desiredDistanceFromCamera = 0;
  private Vector3 m_currentCameraSpacePosition = Vector3.zero;


  private void LateUpdate()
  {
    float t = (transitionDuration == 0) ? 1 : Time.deltaTime / transitionDuration;

    switch (rubberBandMode)
    {
      default:
      case RubberBandMode.None:
        break;

      case RubberBandMode.XY:
        // In this mode, we are always affixed to HUD plane and lerp only in
        // the XY plane
        m_currentCameraSpacePosition = Camera.main.transform.InverseTransformPoint(transform.position);
        m_currentCameraSpacePosition = Vector3.Lerp(m_currentCameraSpacePosition, m_desiredCameraSpacePosition, t);
        m_currentCameraSpacePosition = m_currentCameraSpacePosition.normalized * m_desiredDistanceFromCamera;
        transform.position = Camera.main.transform.TransformPoint(m_currentCameraSpacePosition);
        break;

      case RubberBandMode.XYZ:
        // Lerp in all directions, which means distance from camera is not
        // fixed and changes smoothly
        Vector3 desiredWorldSpacePosition = Camera.main.transform.TransformPoint(m_desiredCameraSpacePosition);
        Vector3 position = Vector3.Lerp(transform.position, desiredWorldSpacePosition, t);
        transform.position = position;
        m_currentCameraSpacePosition = Camera.main.transform.InverseTransformPoint(position); // maintain this in case mode is switched
        break;
    }
  }

  private void Start()
  {
    m_desiredCameraSpacePosition = transform.localPosition;
    m_desiredDistanceFromCamera = m_desiredCameraSpacePosition.magnitude;
    m_currentCameraSpacePosition = m_desiredCameraSpacePosition;
  }
}
