/*
 * TODO:
 * -----
 * 1. Text indicator line should have a mode specifying its positioning and
 *    size in fixed units rather than being proportional to width or height.
 * 2. Should text have a proportionally scalable mode?
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HUDIndicator: MonoBehaviour
{
  [Tooltip("Target to position the indicator on.")]
  public Transform target;
  
  [Tooltip("Factor to multiply target object dimensions by when scaling the indicator.")]
  public float margin = 1.25f;

  [Tooltip("Maximum dimensions of indicator horizontally and vertically (or unbounded if 0).")]
  public Vector2 maxSize = 0.5f * Vector2.one;

  [Tooltip("Angle in degrees from horizontal of the four angled elements.")]
  [RangeAttribute(0, 360)]
  public float bendAngle = 20;

  [Tooltip("Length of bend as a multiple of the width of the indicator (distance between vertical bars).")]
  public float bendLength = 0.25f;

  [Tooltip("Height of text above line as a multiple of text height.")]
  public float textHeight = 0.25f;

  [Tooltip("Duration in seconds over which to draw the line before text appears.")]
  public float textLineDrawTime = 0.5f;

  [Tooltip("Delay in seconds from start of animation to start drawing line. To start after done zooming, make this at least as large as zoom-in time.")]
  public float textLineStartTime = 0;

  [Tooltip("Starting point relative to camera for zoom-in effect.")]
  public Vector3 zoomPosition = Vector3.zero;

  [Tooltip("Starting scale for zoom-in effect.")]
  public float zoomScale = 2;

  [Tooltip("Starting angle in degrees for zoom-in effect.")]
  public float zoomAngle = 135;

  [Tooltip("Zoom-in effect duration in seconds (0 to disable).")]
  public float zoomTime = 0;

  [Tooltip("Line renderers that must be attached as sub-objects.")]
  public LineRenderer[] lineRenderers;

  [Tooltip("Text mesh sub-object.")]
  public TextMesh textMesh;

  private Vector3 m_startPosition;
  private Vector3 m_localScale;
  private float m_startTime;
  private IEnumerator m_coroutine = null;

  private float m_width;
  private float m_height;

  private void ResetCoroutine()
  {
    if (m_coroutine != null)
    {
      StopCoroutine(m_coroutine);
      m_coroutine = null;
    }
  }

  private float[] ComputePiecewiseInterpolationParams(Vector3[] points)
  {
    float[] segmentLengths = new float[points.Length];
    float totalLength = 0;
    segmentLengths[0] = 0;
    for (int i = 1; i < points.Length; i++)
    {
      segmentLengths[i] = Vector3.Distance(points[i], points[i - 1]);
      totalLength += segmentLengths[i];
    }

    float[] tParams = new float[points.Length];
    tParams[0] = 0;
    for (int i = 1; i < points.Length; i++)
    {
      tParams[i] = tParams[i - 1] + segmentLengths[i] / totalLength;
    }
    return tParams;
  }

  private void InterpolateLineSegments(LineRenderer lineRenderer, Vector3[] points, float[] tParams, float t)
  {
    // Draw complete and interpolated segments
    int i;
    lineRenderer.SetPosition(0, points[0]);
    for (i = 1; t > tParams[i - 1] && i < tParams.Length; i++)
    {
      // Compute the interpolation factor, t, for this segment and then
      // interpolate our position within the segment. Note that
      // Vector3.Lerp() clamps to 1, so segments earlier than the current
      // interpolation location will be fully drawn.
      float segmentT = (t - tParams[i - 1]) / (tParams[i] - tParams[i - 1]);
      Vector3 point = Vector3.Lerp(points[i - 1], points[i], segmentT);
      lineRenderer.SetPosition(i, point);
    }

    // Set any remaining, unused points
    for (; i < tParams.Length; i++)
    {
      lineRenderer.SetPosition(i, lineRenderer.GetPosition(i - 1));
    }
  }

  private IEnumerator DrawTextCoroutine()
  {
    if (textMesh.text.Length == 0)
      yield break;

    Vector2 textSize = Footprint.Measure(textMesh);

    Vector3 xDir = Vector3.right;
    Vector3 yDir = Vector3.up;

    LineRenderer line3 = lineRenderers[2];
    line3.positionCount = 3;

    Vector3[] points = new Vector3[3];
    points[0] = xDir * 0.5f * m_width * 1.25f + 0 * m_height * yDir;
    points[1] = points[0] + (xDir + yDir).normalized * 0.5f * m_width;
    points[2] = points[1] + xDir * textSize.x;

    float[] tParams = ComputePiecewiseInterpolationParams(points);

    float startTime = Time.time;
    float t = 0;
    do
    {
      yield return null;
      line3.gameObject.SetActive(true);
      t = textLineDrawTime == 0 ? 1 : (Time.time - startTime) / textLineDrawTime;
      InterpolateLineSegments(line3, points, tParams, t);
    } while (t < 1);

    // Draw text
    textMesh.gameObject.transform.localPosition = line3.GetPosition(1) + yDir * textHeight * textSize.y;
    textMesh.gameObject.SetActive(true);
  }
  
  private void LateUpdate()
  {
    if (target == null)
      return;

    float timeElapsed = Time.time - m_startTime;
    float t = (zoomTime == 0) ? 1 : timeElapsed / zoomTime;

    // Draw text if it's time to do so
    if (m_coroutine == null && timeElapsed >= textLineStartTime)
    {
      m_coroutine = DrawTextCoroutine();
      StartCoroutine(m_coroutine);
    }

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
    width = maxSize.x == 0 ? width : Mathf.Min(maxSize.x, width);
    height = maxSize.y == 0 ? height : Mathf.Min(maxSize.y, height);
    m_width = width;
    m_height = height;
    Vector3 xDir = Vector3.right;
    Vector3 yDir = Vector3.up;
    Vector3 xDirBend = Mathf.Cos(bendAngle * Mathf.Deg2Rad) * Vector3.right;
    Vector3 yDirBend = Mathf.Sin(bendAngle * Mathf.Deg2Rad) * Vector3.up;

    LineRenderer line1 = lineRenderers[0];
    LineRenderer line2 = lineRenderers[1];
    line1.positionCount = 4;
    line2.positionCount = 4;

    // Left side
    line1.SetPosition(0, -xDir * 0.5f * width + 0.5f * height * yDir + (xDirBend + yDirBend).normalized * bendLength * width);
    line1.SetPosition(1, -xDir * 0.5f * width + 0.5f * height * yDir);
    line1.SetPosition(2, -xDir * 0.5f * width - 0.5f * height * yDir);
    line1.SetPosition(3, -xDir * 0.5f * width - 0.5f * height * yDir + (xDirBend - yDirBend).normalized * bendLength * width);

    // Right side
    line2.SetPosition(0, xDir * 0.5f * width + 0.5f * height * yDir + (-xDirBend + yDirBend).normalized * bendLength * width);
    line2.SetPosition(1, xDir * 0.5f * width + 0.5f * height * yDir);
    line2.SetPosition(2, xDir * 0.5f * width - 0.5f * height * yDir);
    line2.SetPosition(3, xDir * 0.5f * width - 0.5f * height * yDir + (-xDirBend - yDirBend).normalized * bendLength * width);

    // Text line disabled for now
    LineRenderer line3 = lineRenderers[2];
    line3.gameObject.SetActive(false);
    textMesh.gameObject.SetActive(false);

    m_startPosition = Camera.main.transform.position + zoomPosition;
    m_localScale = transform.localScale;
    m_startTime = Time.time;
    ResetCoroutine();
  }

  private void OnDisable()
  {
    // Restore actual scale
    if (m_localScale != Vector3.zero)
      transform.localScale = m_localScale;

    ResetCoroutine();
  }
}
