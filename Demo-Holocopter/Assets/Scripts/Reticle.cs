using UnityEngine;
using System.Collections;
using System.Linq;

public class Reticle
{
  private Mesh      m_mesh;
  private Material  m_material;

  private void DrawReticleAround(Bounds bounds)
  {
    // Define the HUD plane in camera space
    Vector3 origin = Vector3.zero;
    Vector3 forward = Vector3.forward;
    float centroid_distance = Vector3.Magnitude(Camera.main.transform.worldToLocalMatrix.MultiplyPoint(bounds.center) - origin);
    Plane hud_plane = new Plane(forward, origin + forward * centroid_distance);

    // AABB corners in world space
    Vector3[] corners = new Vector3[8]
    {
      bounds.center + new Vector3(bounds.extents.x, bounds.extents.y, bounds.extents.z),
      bounds.center + new Vector3(bounds.extents.x, bounds.extents.y, -bounds.extents.z),
      bounds.center + new Vector3(bounds.extents.x, -bounds.extents.y, bounds.extents.z),
      bounds.center + new Vector3(bounds.extents.x, -bounds.extents.y, -bounds.extents.z),
      bounds.center + new Vector3(-bounds.extents.x, bounds.extents.y, bounds.extents.z),
      bounds.center + new Vector3(-bounds.extents.x, bounds.extents.y, -bounds.extents.z),
      bounds.center + new Vector3(-bounds.extents.x, -bounds.extents.y, bounds.extents.z),
      bounds.center + new Vector3(-bounds.extents.x, -bounds.extents.y, -bounds.extents.z)
    };

    // Transform to local (camera) space and project onto HUD plane. Save x, y
    // coordinates so we construct a local (HUD plane) AABB from them.
    float[] hud_x = new float[8];
    float[] hud_y = new float[8];
    float hud_z = centroid_distance;
    for (int i = 0; i < corners.Length; i++)
    {
      Vector3 corner = Camera.main.transform.worldToLocalMatrix.MultiplyPoint(corners[i]);
      Ray to_corner = new Ray(origin, Vector3.Normalize(corner - origin));
      float d = 0;
      hud_plane.Raycast(to_corner, out d);
      Vector3 hud_point = to_corner.GetPoint(d);
      hud_x[i] = hud_point.x;
      hud_y[i] = hud_point.y;
    }

    // Construct AABB in HUD space
    Vector3 top_left = new Vector3(hud_x.Min(), hud_y.Max(), hud_z);
    Vector3 top_right = new Vector3(hud_x.Max(), hud_y.Max(), hud_z);
    Vector3 bottom_left = new Vector3(hud_x.Min(), hud_y.Min(), hud_z);
    Vector3 bottom_right = new Vector3(hud_x.Max(), hud_y.Min(), hud_z);

    // Draw in world space
    m_material.SetPass(0);
    Graphics.DrawMeshNow(m_mesh, Camera.main.transform.TransformPoint(top_left), Quaternion.LookRotation(Camera.main.transform.forward, Camera.main.transform.up));
    Graphics.DrawMeshNow(m_mesh, Camera.main.transform.TransformPoint(top_right), Quaternion.LookRotation(Camera.main.transform.forward, Camera.main.transform.right));
    Graphics.DrawMeshNow(m_mesh, Camera.main.transform.TransformPoint(bottom_right), Quaternion.LookRotation(Camera.main.transform.forward, -Camera.main.transform.up));
    Graphics.DrawMeshNow(m_mesh, Camera.main.transform.TransformPoint(bottom_left), Quaternion.LookRotation(Camera.main.transform.forward, -Camera.main.transform.right));
  }

  public void Draw(GameObject target)
  {
    if (target == null)
      return;
    Bounds bounds = new Bounds(target.transform.position, Vector3.zero);
    foreach (Collider collider in target.GetComponentsInChildren<Collider>())
    {
      bounds.Encapsulate(collider.bounds);
    }
    DrawReticleAround(bounds);
  }

  private void BuildReticleMesh()
  {
    float size = 3e-2f; // reticle size is defined at near clip plane
    m_mesh = new Mesh();
    m_mesh.vertices = new Vector3[3]
    {
      size * new Vector3(-0.5f, 0.5f, 0),
      size * new Vector3(0.5f, 0.5f, 0),
      size * new Vector3(-0.5f, -0.5f, 0)
    };
    m_mesh.triangles = new int[] { 0, 1, 2 };
  }

  public Reticle(Material material)
  {
    m_material = material;
    BuildReticleMesh();
  }
}
