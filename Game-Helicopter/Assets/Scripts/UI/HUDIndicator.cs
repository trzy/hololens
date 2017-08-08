//TODO: setting that restricts maximum size of indicator?
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HUDIndicator: MonoBehaviour
{
  [Tooltip("Target to position the indicator on.")]
  public Transform target;
  
  [Tooltip("Factor to multiply target object dimensions by when scaling the indicator.")]
  public float margin = 1.25f;

  [Tooltip("Angle in degrees from horizontal of the four angled elements.")]
  [RangeAttribute(0, 360)]
  public float bendAngle = 20;

  [Tooltip("Length of bend as a multiple of the width of the indicator (distance between vertical bars).")]
  public float bendLength = 0.25f;

  [Tooltip("Starting point relative to camera for zoom-in effect.")]
  public Vector3 zoomPosition = Vector3.zero;

  [Tooltip("Starting scale for zoom-in effect.")]
  public float zoomScale = 2;

  [Tooltip("Starting angle in degrees for zoom-in effect.")]
  public float zoomAngle = 135;

  [Tooltip("Zoom-in effect duration (0 to disable).")]
  public float zoomTime = 0;

  [Tooltip("Line renderers that must be attached as sub-objects.")]
  public LineRenderer[] lineRenderers;

  [Tooltip("Text mesh sub-object.")]
  public TextMesh textMesh;

  private Vector3 m_startPosition;
  private Vector3 m_localScale;
  private float m_startTime;

  private void LateUpdate()
  {
    if (target == null)
      return;

    float t = (zoomTime == 0) ? 1 : (Time.time - m_startTime) / zoomTime;

    // Track the target and face the camera
    transform.position = Vector3.Lerp(m_startPosition, target.position, t);
    if (Camera.main.transform.position != transform.position)
      transform.rotation = Quaternion.LookRotation((transform.position - Camera.main.transform.position).normalized, Vector3.up);
    transform.Rotate(new Vector3(0, 0, Mathf.Lerp(zoomAngle, 0, t)));
    transform.localScale = m_localScale * Mathf.Lerp(zoomScale, 1, t);
  }

  private void OnEnable()
  {
    if (target == null)
    {
      m_localScale = Vector3.zero;
      return;
    }

    Vector3 targetSize = Footprint.Measure(target.gameObject);
    float width = margin * Mathf.Sqrt(targetSize.x * targetSize.x + targetSize.z * targetSize.z);
    float height = margin * targetSize.y;
    Vector3 xDir = Vector3.right;   //(target.right + target.forward).normalized;
    Vector3 yDir = Vector3.up;      //target.up.normalized;
    Vector3 xDirBend = Mathf.Cos(bendAngle * Mathf.Deg2Rad) * Vector3.right;
    Vector3 yDirBend = Mathf.Sin(bendAngle * Mathf.Deg2Rad) * Vector3.up;

    LineRenderer line1 = lineRenderers[0];
    LineRenderer line2 = lineRenderers[1];
    LineRenderer line3 = lineRenderers[2];
    line1.positionCount = 4;
    line2.positionCount = 4;
    line3.positionCount = 3;

    line1.SetPosition(0, -xDir * 0.5f * width + 0.5f * height * yDir + (xDirBend + yDirBend).normalized * bendLength * width);
    line1.SetPosition(1, -xDir * 0.5f * width + 0.5f * height * yDir);
    line1.SetPosition(2, -xDir * 0.5f * width - 0.5f * height * yDir);
    line1.SetPosition(3, -xDir * 0.5f * width - 0.5f * height * yDir + (xDirBend - yDirBend).normalized * bendLength * width);

    line2.SetPosition(0, xDir * 0.5f * width + 0.5f * height * yDir + (-xDirBend + yDirBend).normalized * bendLength * width);
    line2.SetPosition(1, xDir * 0.5f * width + 0.5f * height * yDir);
    line2.SetPosition(2, xDir * 0.5f * width - 0.5f * height * yDir);
    line2.SetPosition(3, xDir * 0.5f * width - 0.5f * height * yDir + (-xDirBend - yDirBend).normalized * bendLength * width);

    Vector2 textSize = Footprint.Measure(textMesh);
    line3.SetPosition(0, xDir * 0.5f * width * 1.25f + 0 * height * yDir);
    line3.SetPosition(1, line3.GetPosition(0) + (xDir + yDir).normalized * 0.5f * width);
    line3.SetPosition(2, line3.GetPosition(1) + xDir * textSize.x);
    textMesh.gameObject.transform.localPosition = line3.GetPosition(1) + yDir * 0.25f * textSize.y;

    m_startPosition = Camera.main.transform.position + zoomPosition;
    m_localScale = transform.localScale;
    m_startTime = Time.time;

  }

  private void OnDisable()
  {
    // Restore actual scale
    if (m_localScale != Vector3.zero)
      transform.localScale = m_localScale;
  }
}
