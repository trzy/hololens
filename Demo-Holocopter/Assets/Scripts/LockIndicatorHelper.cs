/*
 * TODO:
 * 
 * Rename this to LockIndicatorHelper.cs
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LockIndicatorHelper: MonoBehaviour
{
  [Tooltip("Lock indicator to create when object enters view")]
  public LockIndicator m_lockIndicatorPrefab = null;

  [Tooltip("Whether to render bounds")]
  public bool renderBounds = false;

  [Tooltip("Material to render bounds")]
  public Material boundsMaterial = null;

  private float m_boundingRadius = 0;

  private struct Circle
  {
    public Vector2 center;
    public float radius;
    public Circle(Vector2 pCenter, float pRadius)
    {
      center = pCenter;
      radius = pRadius;
    }
  }

  public struct ProjectedBoundingBox
  {
    public Vector3 center;
    public Vector2 size;
    public ProjectedBoundingBox(Vector3 pCenter, Vector2 pSize)
    {
      center = pCenter;
      size = pSize;
    }
  }

  public ProjectedBoundingBox ComputeCameraSpaceBounds(float z)
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
    Vector3[] cameraPoints = new Vector3[8];
    foreach (BoxCollider collider in GetComponentsInChildren<BoxCollider>())
    {
      Transform colliderXform = collider.transform;

      // Compute all 8 points of bounding box in camera-space coordinates
      Vector3 worldPoint;
      worldPoint = colliderXform.TransformPoint(collider.center + 0.5f * new Vector3(collider.size.x, collider.size.y, collider.size.z));
      cameraPoints[0] = cameraXform.InverseTransformPoint(worldPoint);
      worldPoint = colliderXform.TransformPoint(collider.center + 0.5f * new Vector3(collider.size.x, collider.size.y, -collider.size.z));
      cameraPoints[1] = cameraXform.InverseTransformPoint(worldPoint);
      worldPoint = colliderXform.TransformPoint(collider.center + 0.5f * new Vector3(collider.size.x, -collider.size.y, collider.size.z));
      cameraPoints[2] = cameraXform.InverseTransformPoint(worldPoint);
      worldPoint = colliderXform.TransformPoint(collider.center + 0.5f * new Vector3(collider.size.x, -collider.size.y, -collider.size.z));
      cameraPoints[3] = cameraXform.InverseTransformPoint(worldPoint);
      worldPoint = colliderXform.TransformPoint(collider.center + 0.5f * new Vector3(-collider.size.x, collider.size.y, collider.size.z));
      cameraPoints[4] = cameraXform.InverseTransformPoint(worldPoint);
      worldPoint = colliderXform.TransformPoint(collider.center + 0.5f * new Vector3(-collider.size.x, collider.size.y, -collider.size.z));
      cameraPoints[5] = cameraXform.InverseTransformPoint(worldPoint);
      worldPoint = colliderXform.TransformPoint(collider.center + 0.5f * new Vector3(-collider.size.x, -collider.size.y, collider.size.z));
      cameraPoints[6] = cameraXform.InverseTransformPoint(worldPoint);
      worldPoint = colliderXform.TransformPoint(collider.center + 0.5f * new Vector3(-collider.size.x, -collider.size.y, -collider.size.z));
      cameraPoints[7] = cameraXform.InverseTransformPoint(worldPoint);

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

  private Circle ComputeCameraSpaceBoundingCircle()
  {
    ProjectedBoundingBox pbb = ComputeCameraSpaceBounds(2);
    // Inscribed radius (as opposed to circumscribed, which would require
    // computing diagonal size)
    float diameter = Mathf.Max(pbb.size.x, pbb.size.y);
    return new Circle(pbb.center, 0.5f * diameter);
  }

  private Vector3 ComponentMult(Vector3 a, Vector3 b)
  {
    return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
  }

  private void Awake()
  {
    Vector3[] directions =
    {
      new Vector3(1, 1, 1),
      new Vector3(1, 1, -1),
      new Vector3(1, -1, 1),
      new Vector3(1, -1, -1),
      new Vector3(-1, 1, 1),
      new Vector3(-1, 1, -1),
      new Vector3(-1, -1, 1),
      new Vector3(-1, -1, -1)
    };

    foreach (BoxCollider collider in GetComponentsInChildren<BoxCollider>())
    {
      // For proper scale, transform to world coordinates but subtract parent
      // object position to center the object at origin
      float[] distances = new float[directions.Length];
      for (int i = 0; i < directions.Length; i++)
      {
        Vector3 point = collider.transform.TransformPoint(collider.center + ComponentMult(directions[i], 0.5f * collider.size)) - transform.position;
        distances[i] = Vector3.Magnitude(point);
      }
      System.Array.Sort(distances, (float a, float b) => (int)Mathf.Sign(b - a)); // sort descending
      m_boundingRadius = Mathf.Max(m_boundingRadius, distances[0]);
      Debug.Log(collider.gameObject.name + " collider radius=" + m_boundingRadius);
    }
  }

  private void Start()
  {
    if (renderBounds)
    {
      GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      sphere.transform.parent = null;// gameObject.transform;
      sphere.transform.localScale = 2 * new Vector3(m_boundingRadius, m_boundingRadius, m_boundingRadius);
      sphere.transform.position = transform.position;
      sphere.GetComponent<Renderer>().material = boundsMaterial;
      sphere.GetComponent<Renderer>().material.color = new Color(1, 0, 0, 0.5f); // equivalent to SetColor("_Color", color)
      sphere.GetComponent<SphereCollider>().enabled = false;
      sphere.SetActive(true);
    }


    LockIndicator lockIndicator = Instantiate(m_lockIndicatorPrefab) as LockIndicator;
    lockIndicator.transform.parent = null;
    lockIndicator.targetObject = this;
    Debug.Log("Instantiated LockIndicator");
  }
}
