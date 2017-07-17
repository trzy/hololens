using System;
using UnityEngine;

public class GuidanceArrowMesh: MonoBehaviour
{
  public Material material;

  private void CreateMesh(Mesh mesh)
  {
    mesh.vertices = new Vector3[]
    {
      // Just a simple triangle for now, pointing toward "up" (y) with
      // "forward" being normal to visible face. Pivot point is at the
      // pointing vertex.
      new Vector3(0, 0, 0),
      new Vector3(1, -1, 0),
      new Vector3(-1, -1, 0)
    };

    mesh.triangles = new int[]
    {
      0, 1, 2
    };

    mesh.RecalculateBounds();
    mesh.RecalculateNormals();
  }

  private void Awake()
  {
    Mesh mesh = gameObject.AddComponent<MeshFilter>().mesh;
    MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
    meshRenderer.material = material;
    CreateMesh(mesh);
  }
}
