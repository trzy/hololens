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

  [Tooltip("Line renderers that must be attached as sub-objects.")]
  public LineRenderer[] lineRenderers;

  private void LateUpdate()
  {
    // Track the target and face the camera
    transform.position = target.position;
    transform.forward = (Camera.main.transform.position - transform.position).normalized;
  }

  private void OnEnable()
  {
    Vector3 targetSize = Footprint.Measure(target.gameObject);
    float width = margin * Mathf.Sqrt(targetSize.x * targetSize.x + targetSize.z * targetSize.z);
    float height = margin * targetSize.y;
    Vector3 xDir = Vector3.right;   //(target.right + target.forward).normalized;
    Vector3 yDir = Vector3.up;      //target.up.normalized;
    Vector3 xDirBend = Mathf.Cos(bendAngle * Mathf.Deg2Rad) * Vector3.right;
    Vector3 yDirBend = Mathf.Sin(bendAngle * Mathf.Deg2Rad) * Vector3.up;

    LineRenderer line1 = lineRenderers[0];
    LineRenderer line2 = lineRenderers[1];
    line1.positionCount = 4;
    line2.positionCount = 4;

    line1.SetPosition(0, -xDir * 0.5f * width + 0.5f * height * yDir + (xDirBend + yDirBend).normalized * bendLength * width);
    line1.SetPosition(1, -xDir * 0.5f * width + 0.5f * height * yDir);
    line1.SetPosition(2, -xDir * 0.5f * width - 0.5f * height * yDir);
    line1.SetPosition(3, -xDir * 0.5f * width - 0.5f * height * yDir + (xDirBend - yDirBend).normalized * bendLength * width);

    line2.SetPosition(0, xDir * 0.5f * width + 0.5f * height * yDir + (-xDirBend + yDirBend).normalized * bendLength * width);
    line2.SetPosition(1, xDir * 0.5f * width + 0.5f * height * yDir);
    line2.SetPosition(2, xDir * 0.5f * width - 0.5f * height * yDir);
    line2.SetPosition(3, xDir * 0.5f * width - 0.5f * height * yDir + (-xDirBend - yDirBend).normalized * bendLength * width);
  }
}
