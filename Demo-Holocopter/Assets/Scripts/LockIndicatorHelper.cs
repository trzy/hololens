/*
 * TODO:
 * 
 * - Create a targeting base class?
 * - Compute camera space bounds only once per frame and retain (use frame time
 *   to determine when to recompute)
 * 
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LockIndicatorHelper: MonoBehaviour
{
  [Tooltip("Lock indicator to create when object enters view")]
  public LockIndicator m_lockIndicatorPrefab = null;

  public LockIndicator lockIndicator
  {
    get { return m_lockIndicator; }
  }

  private LockIndicator m_lockIndicator = null;

  private struct ProjectedBoundingBox
  {
    public Vector3 center;
    public Vector2 size;
    public ProjectedBoundingBox(Vector3 pCenter, Vector2 pSize)
    {
      center = pCenter;
      size = pSize;
    }
  }

  private ProjectedBoundingBox ComputeCameraSpaceBoundsAt(float z)
  {
    //TODO: optimize by computing projection manually rather than w/ raycast?
    //TODO: can precompute the 8 world points because that shouldn't change (unless
    //      collider is dynamically resized at run-time)
    float minX = float.PositiveInfinity;
    float minY = float.PositiveInfinity;
    float maxX = float.NegativeInfinity;
    float maxY = float.NegativeInfinity;
    Transform cameraXform = Camera.main.transform;
    Plane projectionPlane = new Plane(-Vector3.forward, z); // plane normal facing camera
    Vector3[] worldPoints = new Vector3[8];
    Vector3[] cameraPoints = new Vector3[8];
    foreach (BoxCollider collider in GetComponentsInChildren<BoxCollider>())
    {
      Transform colliderXform = collider.transform;

      // Compute all 8 points of bounding box in camera-space coordinates
      worldPoints[0] = colliderXform.TransformPoint(collider.center + 0.5f * new Vector3(collider.size.x, collider.size.y, collider.size.z));
      worldPoints[1] = colliderXform.TransformPoint(collider.center + 0.5f * new Vector3(collider.size.x, collider.size.y, -collider.size.z));
      worldPoints[2] = colliderXform.TransformPoint(collider.center + 0.5f * new Vector3(collider.size.x, -collider.size.y, collider.size.z));
      worldPoints[3] = colliderXform.TransformPoint(collider.center + 0.5f * new Vector3(collider.size.x, -collider.size.y, -collider.size.z));
      worldPoints[4] = colliderXform.TransformPoint(collider.center + 0.5f * new Vector3(-collider.size.x, collider.size.y, collider.size.z));
      worldPoints[5] = colliderXform.TransformPoint(collider.center + 0.5f * new Vector3(-collider.size.x, collider.size.y, -collider.size.z));
      worldPoints[6] = colliderXform.TransformPoint(collider.center + 0.5f * new Vector3(-collider.size.x, -collider.size.y, collider.size.z));
      worldPoints[7] = colliderXform.TransformPoint(collider.center + 0.5f * new Vector3(-collider.size.x, -collider.size.y, -collider.size.z));
      for (int i = 0; i < 8; i++)
      {
        cameraPoints[i] = cameraXform.InverseTransformPoint(worldPoints[i]);
      }

      // Project them all and search for min/max X/Y
      for (int i = 0; i < 8; i++)
      {
        Ray toPoint = new Ray(Vector3.zero, cameraPoints[i]);
        float d = 0;
        projectionPlane.Raycast(toPoint, out d);
        Vector3 projectedPoint = toPoint.GetPoint(d);
        minX = Mathf.Min(minX, projectedPoint.x);
        maxX = Mathf.Max(maxX, projectedPoint.x);
        minY = Mathf.Min(minY, projectedPoint.y);
        maxY = Mathf.Max(maxY, projectedPoint.y);
      }
    }

    Vector3 cameraSpaceCenter = new Vector3(0.5f * (minX + maxX), 0.5f * (minY + maxY), z);  // in camera space, forward z becomes negative
    return new ProjectedBoundingBox(Camera.main.transform.TransformPoint(cameraSpaceCenter), new Vector2(maxX - minX, maxY - minY));
  }

  public Vector3 ComputeCameraSpaceCentroidAt(float z)
  {
    ProjectedBoundingBox pbb = ComputeCameraSpaceBoundsAt(z);
    return pbb.center;
  }

  public float ComputeCameraSpaceRadiusAt(float z)
  {
    ProjectedBoundingBox pbb = ComputeCameraSpaceBoundsAt(z);
    float radius = 0.5f * Mathf.Max(pbb.size.x, pbb.size.y);
    return radius;
  }

  public bool InViewFrustum(Bounds bounds)
  {
    Plane[] planes = GeometryUtility.CalculateFrustumPlanes(Camera.main);
    return GeometryUtility.TestPlanesAABB(planes, bounds);
  }

  private void OnDestroy()
  {
    // Must manually destroy because we are not parented
    if (m_lockIndicator != null)
    {
      Destroy(m_lockIndicator.gameObject);
    }
  }

  private void Awake()
  {
    // Lock indicator is not parented to target object so as not to be affected
    // by its transform
    m_lockIndicator = Instantiate(m_lockIndicatorPrefab) as LockIndicator;
    m_lockIndicator.transform.parent = null;
    m_lockIndicator.targetObject = this;  // pass pointer to target game object
  }
}
