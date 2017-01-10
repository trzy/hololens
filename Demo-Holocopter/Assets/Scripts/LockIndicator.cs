/*
 * TODO:
 * -----
 * - Implement a vector class so we can reuse the same array each frame. See
 *   ResizableArray here: http://stackoverflow.com/questions/4972951/listt-to-t-without-copying
 * - Investigate garbage collection and memory usage when generating new mesh
 *   each frame.
 * - Move mesh building functions (DrawArc(), etc.) into their own class
 *   and have scripts for different kinds of reticles.
 * - A thickness of .0025m looks good for line drawing at z=2m from camera.
 * - Rather than generating meshes on the fly, these things could probably be
 *   drawn using a shader program and a quad (two triangles).
 */
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LockIndicator: MonoBehaviour
{
  public Material material = null;
  public float zDistance = 2f;
  public LockIndicatorHelper targetObject = null;

  private Mesh m_mesh = null;
  private float m_viewportRadius; // distance from center to extreme right/left of viewport at z distance

  /*
   * Draws a triangle that is parameterized by polar angles and radial 
   * distances from the coordinate system origin point.
   *
   *       t
   *      /|
   *     / |
   *    /  | <-- base
   *   /   |
   * v<--c-|w
   *   \ L |
   *    \  |
   *     \ |
   *      \|
   *       u
   * 
   * Vertices t and u are at the base of the triangle and v is at its apex.
   * w is a point on the base such that the line segment v-w (with length L)
   * bisects the triangle, is perpendicular to the base.
   * 
   * Point c is the midpoint along the line segment v-w and therefore also the
   * center point of the triangle. It is paramterized by a polar angle and 
   * radial distance from the coordinate system origin, where angle 0
   * corresponds to a line along the +X axis.
   * 
   * Point v is simply c - 0.5 * L along the radial line and point w is c + 
   * 0.5 * L.
   * 
   * Points t and u, which determine the width of the triangle base, are
   * determined by polar angles from the origin of the coordinate system (*not*
   * point v). The polar angle of the arc from the origin through t-u needs to
   * be given.
   * 
   * The triangle points are initially computed at a polar angle of 0, so that
   * v and w are on the x axis. This makes the angles t-v-w and u-v-w simply
   * half the arc that describes points t and u. The three vertices are rotated
   * about the origin by the polar angle as a final step.
   * 
   * Required parameters:
   * 
   *  angleDeg             = The polar angle (where 0 is along the +X axis) of
   *                         the center line (v-w) of the triangle.
   *  centerRadialDistance = Radial distance to the triangle's center point.
   *  centerLength         = The length of the line segment v-w and the
   *                         "height", or shortest distance, from apex (v) to
   *                         base (point w).
   *  polarArcDeg          = The polar angle of the arc formed by the base 
   *                         (radially outermost) vertices. Defines the "width"
   *                         of the triangle in terms of polar angles.
   */
  private void DrawArcTriangle(List<Vector3> verts, List<int> triangles, List<Color32> colors, Color32 color, float polarAngleDeg, float centerRadialDistance, float centerLength, float polarArcDeg)
  {
    // Generate points at polar angle = 0
    float halfAngle = 0.5f * polarArcDeg * Mathf.Deg2Rad;
    float side = (centerRadialDistance + 0.5f * centerLength) / Mathf.Cos(halfAngle);
    Vector3 p1 = new Vector3(centerRadialDistance - 0.5f * centerLength, 0, 0);
    Vector3 p2 = p1 + new Vector3(centerLength, side * Mathf.Sin(halfAngle), 0);
    Vector3 p3 = new Vector3(p2.x, -p2.y, 0);
    
    // Rotate them into position
    Quaternion rotation = Quaternion.Euler(0, 0, polarAngleDeg);
    p1 = rotation * p1;
    p2 = rotation * p2;
    p3 = rotation * p3;

    // Store the triangle
    int vertIdx = verts.Count;
    verts.Add(p1);
    verts.Add(p2);
    verts.Add(p3);
    triangles.Add(vertIdx++);
    triangles.Add(vertIdx++);
    triangles.Add(vertIdx++);
    colors.Add(color);
    colors.Add(color);
    colors.Add(color);
  }

  private void DrawArc(List<Vector3> verts, List<int> triangles, List<Color32> colors, Color32 fromColor, Color32 toColor, float innerRadius, float outerRadius, float fromPolarDeg, float toPolarDeg, int numSegments)
  {
    //TODO: special case 360 degree case?
    float step = Mathf.Deg2Rad * (toPolarDeg - fromPolarDeg) / numSegments;
    float tStep = 1.0f / numSegments;
    float fromAngle = Mathf.Deg2Rad * fromPolarDeg;
    float toAngle = Mathf.Deg2Rad * toPolarDeg;
    float angle = fromAngle;
    float t = 0;
    int vertIdx = verts.Count;
    for (int i = 0; i < numSegments + 1; i++)
    {
      Vector3 components = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle));
      Vector3 innerPoint = innerRadius * components;
      Vector3 outerPoint = outerRadius * components;
      angle += step;
      Color32 color = Color32.Lerp(fromColor, toColor, t);
      t += tStep;
      verts.Add(innerPoint);
      verts.Add(outerPoint);
      colors.Add(color);
      colors.Add(color);
      if (i > 0)
      {
        triangles.Add(vertIdx - 1);
        triangles.Add(vertIdx - 2);
        triangles.Add(vertIdx - 0);
        triangles.Add(vertIdx - 0);
        triangles.Add(vertIdx + 1);
        triangles.Add(vertIdx - 1);
      }
      vertIdx += 2;
    }
  }

  private void GenerateReticle(float radius)
  {
    float thickness = .02f;
    //float thickness = .0025f; // looks great on actual device at z=2m
    float innerRadius = radius - 0.5f * thickness;
    float outerRadius = radius + 0.5f * thickness;
    int segments = 10;
    List<Vector3> verts = new List<Vector3>();
    List<int> triangles = new List<int>();
    List<Color32> colors = new List<Color32>();
    Color32 black = new Color32(0xff, 0, 0, 0xc0);
    Color32 red = new Color32(0xff, 0, 0, 0xc0);
    Color32 blue = new Color32(101, 215, 252, 0xc0);
    Color32 orange = new Color32(0xff, 0x8c, 0, 0xff);
    Color32 gold = new Color32(0xff, 0xd7, 0x00, 0xff);

    Color32 startColor = red;
    Color32 endColor = red;

    // Inner
    DrawArc(verts, triangles, colors, startColor, endColor, innerRadius, outerRadius, 55f, 125f, segments);
    DrawArc(verts, triangles, colors, startColor, endColor, innerRadius, outerRadius, 145f, 215f, segments);
    DrawArc(verts, triangles, colors, startColor, endColor, innerRadius, outerRadius, 235f, 305f, segments);
    DrawArc(verts, triangles, colors, startColor, endColor, innerRadius, outerRadius, 325f, 395f, segments);

    m_mesh.vertices = verts.ToArray();
    m_mesh.triangles = triangles.ToArray();
    m_mesh.colors32 = colors.ToArray();
    m_mesh.RecalculateBounds();  // absolutely needed because Unity may erroneously think we are off-screen at times
  }

  private void Update()
  {
    if (targetObject == null)
      return;
    //TODO: onlyneed to re-generate position, not all bound
    LockIndicatorHelper.ProjectedBoundingBox pbb = targetObject.ComputeCameraSpaceBounds(zDistance);
    transform.position = pbb.center;/*Camera.main.transform.position + new Vector3(pbb.center.x, pbb.center.y, zDistance);*/
    transform.rotation = Camera.main.transform.rotation;
    //Debug.Log("Distance = " + Vector3.Magnitude(transform.position - Camera.main.transform.position) + ", center=" + pbb.center);
  }

  private void Start()
  {
    // Set up reticle mesh
    m_mesh = gameObject.AddComponent<MeshFilter>().mesh;
    MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
    renderer.material = material;
    renderer.material.color = Color.white;
    LockIndicatorHelper.ProjectedBoundingBox pbb = targetObject.ComputeCameraSpaceBounds(zDistance);
    float radius = 0.5f * Mathf.Max(pbb.size.x, pbb.size.y);
    GenerateReticle(radius);

    // Precompute some values
    m_viewportRadius = zDistance * Mathf.Tan(Mathf.Deg2Rad * Camera.main.fieldOfView);
  }
}
