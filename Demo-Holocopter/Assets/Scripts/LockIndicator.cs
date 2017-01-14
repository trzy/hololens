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

  private GameObject m_reticleObject = null;
  private MeshRenderer m_reticleRenderer = null;
  private Mesh m_reticleMesh = null;

  private GameObject m_timerObject = null;
  private MeshRenderer m_timerRenderer = null;
  private Mesh m_timerMesh = null;

  private class StepwiseAnimator
  {
    private float[] m_values = null;
    private float[] m_timeDeltas = null;
    private TimeScaleFunction[] m_timeScale = null;
    private int m_step = 0;
    private bool m_finished = true;
    private float m_tStart = 0;     // beginning of animation
    private float m_tSegmentStart;  // beginning of current animation segment

    public float currentValue = 0;
    public float previousValue = 0;

    public void Update()
    {
      if (m_timeDeltas == null || m_finished)
      {
        return;
      }

      // Is it time to advance to next animation step?
      float deltaTSeg = Time.time - m_tSegmentStart;
      while (m_step < (m_timeDeltas.Length - 1) && deltaTSeg > m_timeDeltas[m_step])
      {
        m_tSegmentStart += m_timeDeltas[m_step];
        deltaTSeg -= m_timeDeltas[m_step];
        ++m_step;
      }

      // Interpolate between animation frames
      int i = m_step;
      float t = deltaTSeg / m_timeDeltas[i];
      float tScaled = (m_timeScale == null || m_timeScale[i] == null) ? t : m_timeScale[i](t);
      float value = Mathf.Lerp(m_values[i + 0], m_values[i + 1], tScaled);
      previousValue = currentValue;
      currentValue = value;

      // Finished?
      if (m_step == m_timeDeltas.Length - 1 && deltaTSeg >= m_timeDeltas[m_step])
      {
        m_finished = false;
      }
    }

    public void Reset()
    {
      m_step = 0;
      m_finished = false;
      m_tStart = Time.time;
      m_tSegmentStart = m_tStart;
      currentValue = 0;
      // Retain previous value
    }

    public void Reset(float[] values, float[] timeDeltas, TimeScaleFunction[] timeScale)
    {
      m_values = (float[])values.Clone();
      m_timeDeltas = (float[])timeDeltas.Clone();
      m_timeScale = timeScale != null ? (TimeScaleFunction[])timeScale.Clone() : null;
      m_step = 0;
      m_finished = false;
      m_tStart = Time.time;
      m_tSegmentStart = m_tStart;
      currentValue = 0;
      // Retain previous value
    }

    public StepwiseAnimator(float[] values, float[] timeDeltas, TimeScaleFunction[] timeScale)
    {
      Reset(values, timeDeltas, timeScale);
    }
  }

  private StepwiseAnimator m_radiusAnimation = null;
  private StepwiseAnimator m_rotationAnimation = null;
  private StepwiseAnimator m_timerAnimation = null; // TEMPORARY: use an API for setting this and not an animation
  
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

  private void GenerateReticle(float radius, float thickness)
  {
    //float thickness = .02f;
    //float thickness = .0025f; // looks great on actual device at z=2m
    //thickness = .01f;
    float innerRadius = radius - 0.5f * thickness;
    float outerRadius = radius + 0.5f * thickness;
    int segments = 10;
    List<Vector3> verts = new List<Vector3>();
    List<int> triangles = new List<int>();
    List<Color32> colors = new List<Color32>();
    Color32 black = new Color32(0xff, 0, 0, 0xc0);
    //Color32 red = new Color32(0xff, 0, 0, 0xc0);
    Color32 blue = new Color32(101, 215, 252, 0xc0);
    Color32 orange = new Color32(0xff, 0x8c, 0, 0xff);
    Color32 gold = new Color32(0xff, 0xd7, 0x00, 0xff);

    Color32 red = new Color32(244, 66, 66, 0xe0);

    Color32 startColor = red;
    Color32 endColor = red;

    // Inner
    DrawArc(verts, triangles, colors, startColor, endColor, innerRadius, outerRadius, 55f, 125f, segments);
    DrawArc(verts, triangles, colors, startColor, endColor, innerRadius, outerRadius, 145f, 215f, segments);
    DrawArc(verts, triangles, colors, startColor, endColor, innerRadius, outerRadius, 235f, 305f, segments);
    DrawArc(verts, triangles, colors, startColor, endColor, innerRadius, outerRadius, 325f, 395f, segments);

    m_reticleMesh.vertices = verts.ToArray();
    m_reticleMesh.triangles = triangles.ToArray();
    m_reticleMesh.colors32 = colors.ToArray();
    m_reticleMesh.RecalculateBounds();  // absolutely needed because Unity may erroneously think we are off-screen at times
  }

  private void GenerateTimer(float radius, float thickness, float pctComplete)
  {
    //float thickness = .02f;
    //float thickness = .0025f; // looks great on actual device at z=2m
    float innerRadius = radius - 0.5f * thickness;
    float outerRadius = radius + 0.5f * thickness;
    int segments = 4;
    List<Vector3> verts = new List<Vector3>();
    List<int> triangles = new List<int>();
    List<Color32> colors = new List<Color32>();
    Color32 black = new Color32(0xff, 0, 0, 0xc0);
    //Color32 red = new Color32(0xff, 0, 0, 0xc0);
    Color32 blue = new Color32(101, 215, 252, 0xc0);
    Color32 orange = new Color32(0xff, 0x8c, 0, 0xff);
    Color32 gold = new Color32(0xff, 0xd7, 0x00, 0xff);

    Color32 red = new Color32(244, 66, 66, 0x20);
    

    Color32 startColor = red;
    Color32 endColor = red;

    Color32 colorOff = new Color32(244, 66, 66, 0x20);
    Color32 colorOn = new Color32(244, 66, 66, 0xe0);


    int numTicks = 16;
    float tickPitch = 360.0f / numTicks;
    float tickWidth = tickPitch * 0.9f;
    float theta = 90f;
    for (int i = 0; i < numTicks; i++)
    {
      float height = 1 - (1 - Mathf.Sin(Mathf.Deg2Rad * theta)) * 0.5f; // how high up from bottom are we?
      Color32 color = height <= pctComplete ? colorOn : colorOff;
      DrawArc(verts, triangles, colors, color, color, innerRadius, outerRadius, theta - 0.5f * tickWidth, theta + 0.5f * tickWidth, segments);
      theta += tickPitch;
    }
    m_timerMesh.vertices = verts.ToArray();
    m_timerMesh.triangles = triangles.ToArray();
    m_timerMesh.colors32 = colors.ToArray();
    m_timerMesh.RecalculateBounds();  // absolutely needed because Unity may erroneously think we are off-screen at times
  }

  private void OnDestroy()
  {
    //TODO: clean up material? (what about mesh?)
  }

  private float Sigmoid01(float x)
  {
    // f(x) = x / (1 + |x|), f(x): [-0.5, 0.5] for x: [-1, 1]
    // To get [0, 1] for [0, 1]: f'(x) = 0.5 + f(2 * (x - 0.5))
    float y = 2 * (x - 0.5f);
    float f = 0.5f + y / (1 + Mathf.Abs(y));
    return f;
  }

  private delegate float TimeScaleFunction(float t01);

  private float[] m_animationRadius = null;
  private float[] m_animationRotation = null;
  private TimeScaleFunction[] m_animationTimeScale = null;
  private float[] m_animationTimeDelta = null;
  private int m_animationStep = 0;

  private void UpdateReticleTransform()
  {
    // Update the local reticle transform
    m_radiusAnimation.Update();
    m_rotationAnimation.Update();
    m_timerAnimation.Update();
    if (m_radiusAnimation.currentValue != m_radiusAnimation.previousValue || m_timerAnimation.currentValue != m_timerAnimation.previousValue)
    {
      float reticleThickness = .0025f;
      float timerThickness = .005f;
      GenerateReticle(m_radiusAnimation.currentValue, reticleThickness);
      GenerateTimer(m_radiusAnimation.currentValue - (.5f * reticleThickness + 2*timerThickness + 0.5f * timerThickness), timerThickness, m_timerAnimation.currentValue);
    }
    m_reticleObject.transform.localRotation = Quaternion.Euler(0, 0, m_rotationAnimation.currentValue);
    m_timerObject.transform.localRotation = Quaternion.Euler(0, 0, -m_rotationAnimation.currentValue);
  }

  private void Update()
  {
    if (targetObject == null)
    {
      return;
    }
    UpdateReticleTransform();
    m_reticleRenderer.enabled = GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(Camera.main), targetObject.GetComponent<BoxCollider>().bounds);
    transform.position = targetObject.ComputeCameraSpaceCentroidAt(zDistance);
    transform.rotation = Camera.main.transform.rotation;
    //Debug.Log("Distance = " + Vector3.Magnitude(transform.position - Camera.main.transform.position) + ", center=" + pbb.center);
  }

  private void Start()
  {
    // Create reticle game object and mesh
    m_reticleObject = new GameObject("LockIndicator-Reticle");
    m_reticleObject.transform.parent = transform;
    m_reticleMesh = m_reticleObject.AddComponent<MeshFilter>().mesh;
    m_reticleRenderer = m_reticleObject.AddComponent<MeshRenderer>();
    m_reticleRenderer.material = material;
    m_reticleRenderer.material.color = Color.white;
    //GenerateReticle(targetObject.ComputeCameraSpaceRadiusAt(zDistance));

    // Create wn game object and mesh
    m_timerObject = new GameObject("LockIndicator-Timer");
    m_timerObject.transform.parent = transform;
    m_timerMesh = m_timerObject.AddComponent<MeshFilter>().mesh;
    m_timerRenderer = m_timerObject.AddComponent<MeshRenderer>();
    m_timerRenderer.material = material;
    m_timerRenderer.material.color = Color.white;

    // Set up initial animation
    float viewportRadius = zDistance * Mathf.Tan(Mathf.Deg2Rad * Camera.main.fieldOfView);
    float radius = targetObject.ComputeCameraSpaceRadiusAt(zDistance);
    float[] radii                 = new float[]             { viewportRadius, radius, radius, radius, radius        };
    float[] rotations             = new float[]             { 0,              0,    45,        -45,       0         };
    float[] timeDeltas            = new float[]             {                 1,    1,         1,         1         };
    TimeScaleFunction[] timeScale = new TimeScaleFunction[] {                 null, Sigmoid01, Sigmoid01, Sigmoid01 };
    m_radiusAnimation = new StepwiseAnimator(radii, timeDeltas, timeScale);
    m_rotationAnimation = new StepwiseAnimator(rotations, timeDeltas, timeScale);
    float[] countdown = new float[] { -1, -1, -1, -1, 0, 1 };
    float[] timeDeltas2 = new float[] { 1, 1, 1, 1, 10 };
    m_timerAnimation = new StepwiseAnimator(countdown, timeDeltas2, null);
  }
}
