using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestOBBMeshIntersection: MonoBehaviour
{
  [Tooltip("Triangle center point.")]
  public Vector3 triangleCenter = Vector3.zero;

  [Tooltip("Triangle base width.")]
  public float triangleWidth = 2;

  [Tooltip("Triangle height.")]
  public float triangleHeight = 2;

  private Mesh m_triangleMesh;
  private Transform m_triangleXform;

  private void Update()
  {
    HoloToolkit.Unity.SpatialMapping.OrientedBoundingBox obb = OBBMeshIntersection.CreateWorldSpaceOBB(GetComponent<BoxCollider>());
    List<int> intersecting = OBBMeshIntersection.FindTriangles(obb, m_triangleMesh.vertices, m_triangleMesh.GetTriangles(0), m_triangleXform);
    GetComponent<Renderer>().material.color = intersecting.Count > 0 ? Color.red : Color.green;
  }

  private void CreateTriangle()
  {
    GameObject obj = new GameObject("Triangle");
    obj.transform.parent = null;
    obj.transform.position = triangleCenter;
    obj.transform.rotation = Quaternion.identity;
    obj.transform.localScale = Vector3.one;
    Mesh mesh = obj.AddComponent<MeshFilter>().mesh;
    MeshRenderer meshRenderer = obj.AddComponent<MeshRenderer>();
    //meshRenderer.material = material;
    meshRenderer.enabled = true;

    Vector3[] vertices = new Vector3[]
    {
      new Vector3(-0.5f * triangleWidth, -0.5f * triangleHeight, 0),
      new Vector3(0, 0.5f * triangleHeight, 0),
      new Vector3(0.5f * triangleWidth, -0.5f * triangleHeight, 0)
    };

    int[] triangles = new int[]
    {
      0, 1, 2
    };

    mesh.vertices = vertices;
    mesh.triangles = triangles;
    mesh.RecalculateBounds();
    mesh.RecalculateNormals();

    m_triangleMesh = mesh;
    m_triangleXform = obj.transform;
  }

  private void Awake()
  {
    CreateTriangle();
  }
}
