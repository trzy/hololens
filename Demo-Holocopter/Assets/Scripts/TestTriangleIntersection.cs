using UnityEngine;
using System.Collections;

public class TestTriangleIntersection: MonoBehaviour
{
  public GameObject obbParent;
  public GameObject meshParent;

  void Start()
  {
    BoxCollider obb = obbParent.GetComponent<BoxCollider>();
    Mesh mesh = meshParent.GetComponent<MeshFilter>().sharedMesh;
    Debug.Log("OBB-Mesh Intersection Result: " + (OBBMeshIntersection.FindTriangles(obb, mesh, meshParent.transform).Count > 0));
  }
}