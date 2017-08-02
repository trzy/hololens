//TODO: design a more interesting transition later

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlyInTransition: MonoBehaviour
{
  [Tooltip("Scripts to activate once target position reached.")]
  public MonoBehaviour[] activateWhenPositionReached;

  [Tooltip("Scripts to activate once target orientation reached.")]
  public MonoBehaviour[] activateWhenOrientationReached;

  private float m_t0;
  private Vector3 m_startPosition;
  private Vector3 m_endPosition;
  private Vector3 m_startOrientation;
  private Vector3 m_endOrientation;

  private Vector3 m_forwardVector;

  private void ActivateScripts(MonoBehaviour[] scripts, bool enabled)
  {
    if (scripts == null)
      return;

    foreach (MonoBehaviour m in scripts)
    {
      m.enabled = enabled;
    }
  }

  private void LateUpdate()
  {
    float tPos = (Time.time - m_t0) / 1;
    float tRot = (Time.time - (m_t0 + 1)) / 1.5f; // start one second after beginning of position animation, animate 1.5 seconds
    Vector3 currentPosition = Vector3.Lerp(m_startPosition, m_endPosition, MathHelpers.CircularEaseOut(0, 1, tPos));
    transform.position = Camera.main.transform.TransformPoint(currentPosition);
    Vector3 forward = Vector3.Lerp(m_startOrientation, m_endOrientation, MathHelpers.CircularEaseOut(0, 1, tRot));
    transform.rotation = Quaternion.LookRotation(forward);

    if (tPos >= 1)
      ActivateScripts(activateWhenPositionReached, true);

    if (tRot >= 1)
    {
      ActivateScripts(activateWhenOrientationReached, true);
      enabled = false;
    }
  }

  private void Start()
  {
    m_startPosition = 1 * Camera.main.transform.right;
    m_endPosition = 2 * Camera.main.transform.forward;
    m_startOrientation = (Camera.main.transform.right + Camera.main.transform.forward).normalized;
    m_endOrientation = Camera.main.transform.forward;
    m_t0 = Time.time;

    m_forwardVector = Camera.main.transform.right;
  }

  private void Awake()
  {
    ActivateScripts(activateWhenPositionReached, false);
    ActivateScripts(activateWhenOrientationReached, false);
  }
}
