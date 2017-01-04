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

public class ReticleConcept2: MonoBehaviour
{
  public GameObject reticle = null;
  public GameObject alert = null;
  public Material material = null;

  private struct Xform
  {
    public float radius;
    public Quaternion rotation;
    public Vector3 position;

    public Xform(float r, Quaternion rot, Vector3 pos)
    {
      radius = r;
      rotation = rot;
      position = pos;
    }
  }

  private struct XformAnimator
  {
    public delegate void OnCompleteCallback();
    public delegate float TimeScaleFunction(float t01);
    public Xform current;

    private Xform m_start;
    private Xform m_end;
    private float m_duration;
    private float m_startTime;
    private float m_timeElapsed;
    private float m_t;
    private bool m_animate;
    private TimeScaleFunction m_RescaleTime;
    private OnCompleteCallback m_OnComplete;

    public void Update()
    {
      if (m_animate)
      {
        float now = Time.time;
        m_timeElapsed = now - m_startTime;
        m_t = m_timeElapsed / m_duration;
        if (null != m_RescaleTime)
        {
          m_t = m_RescaleTime(m_t);
        }
        current.rotation = Quaternion.Lerp(m_start.rotation, m_end.rotation, m_t);
        current.radius = Mathf.Lerp(m_start.radius, m_end.radius, m_t);
        current.position = Vector3.Lerp(m_start.position, m_end.position, m_t);
        if (now >= (m_startTime + m_duration))
        {
          m_animate = false;
          m_RescaleTime = null; // always reset time scale function after each animation
          if (null != m_OnComplete)
          {
            m_OnComplete();
          }
        }
      }
    }

    public void StartRotation(float toDegZ, float duration, OnCompleteCallback OnComplete = null, TimeScaleFunction RescaleTime = null)
    {
      m_start = current;
      m_end = current;
      m_duration = duration;
      m_startTime = Time.time;
      m_timeElapsed = 0;
      m_t = 0;
      m_animate = true;
      m_OnComplete = OnComplete;
      m_RescaleTime = RescaleTime;
      m_end.rotation = Quaternion.Euler(0, 0, toDegZ);
    }

    public void StartAnimation(Xform startState, Xform endState, float duration, OnCompleteCallback OnComplete = null, TimeScaleFunction RescaleTime = null)
    {
      m_start = startState;
      m_end = endState;
      current = startState;
      m_duration = duration;
      m_startTime = Time.time;
      m_timeElapsed = 0;
      m_t = 0;
      m_animate = true;
      m_OnComplete = OnComplete;
      m_RescaleTime = RescaleTime;
    }
  }

  private Mesh m_reticleMesh = null;
  private Mesh m_alertMesh = null;
  private const float m_zDistance = 2f;
  private float m_viewportRadius; // distance from center to extreme right/left of viewport at z distance
  private XformAnimator m_reticleAnimator = new XformAnimator();
  private XformAnimator m_alertAnimator = new XformAnimator();
  private bool m_fireTimerActive = false;
  private float m_fireTimerStart = 0;
  private const float m_fireTimerDuration = 1;

  private float Sigmoid01(float x)
  {
    // f(x) = x / (1 + |x|), f(x): [-0.5, 0.5] for x: [-1, 1]
    // To get [0, 1] for [0, 1]: f'(x) = 0.5 + f(2 * (x - 0.5))
    float y = 2 * (x - 0.5f);
    float f = 0.5f + y / (1 + Mathf.Abs(y));
    return f;
  }

  private float Blink(float hz, float time)
  {
    float a = Mathf.PI * 2 * hz;
    return 0.5f * (1 + Mathf.Cos(a * time));
  }

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

    m_reticleMesh.vertices = verts.ToArray();
    m_reticleMesh.triangles = triangles.ToArray();
    m_reticleMesh.colors32 = colors.ToArray();
    m_reticleMesh.RecalculateBounds();  // absolutely needed because Unity may erroneously think we are off-screen at times
  }

  private void GenerateAlert(float radius, float readyToFire, float blink)
  {
    float thickness = .02f;
    //float thickness = .0025f; // looks great on actual device at z=2m
    float innerRadius = radius - 0.5f * thickness;
    float outerRadius = radius + 0.5f * thickness;
    int segments = 10;
    List<Vector3> verts = new List<Vector3>();
    List<int> triangles = new List<int>();
    List<Color32> colors = new List<Color32>();
    Color32 redLow = new Color32(0xff, 0, 0, 0xc0);
    Color32 redHigh = new Color32(0xff, 0, 0, 0xff);
    Color32 blue = new Color32(101, 215, 252, 0xc0);
    Color32 orange = new Color32(0xff, 0x8c, 0, 0xff);
    Color32 gold = new Color32(0xff, 0xd7, 0x00, 0xff);

    Color32 startColor = Color32.Lerp(redLow, redHigh, blink);
    Color32 endColor = startColor;

    float outerThickness = .03f;
    float innerRadius2 = outerRadius + .005f;
    float outerRadius2 = innerRadius2 + outerThickness;
    float minArc = 45f;
    float maxArc = 90f;
    float currentArc = Mathf.Lerp(minArc, maxArc, readyToFire); //Mathf.Lerp(minArc, 0, readyToFire);
    DrawArc(verts, triangles, colors, startColor, endColor, innerRadius2, outerRadius2, 45f - 0.5f * currentArc, 45f + 0.5f * currentArc, segments);
    DrawArc(verts, triangles, colors, startColor, endColor, innerRadius2, outerRadius2, 135f - 0.5f * currentArc, 135f + 0.5f * currentArc, segments);
    DrawArc(verts, triangles, colors, startColor, endColor, innerRadius2, outerRadius2, 225f - 0.5f * currentArc, 225f + 0.5f * currentArc, segments);
    DrawArc(verts, triangles, colors, startColor, endColor, innerRadius2, outerRadius2, 315f - 0.5f * currentArc, 315f + 0.5f * currentArc, segments);
    m_alertMesh.vertices = verts.ToArray();
    m_alertMesh.triangles = triangles.ToArray();
    m_alertMesh.colors32 = colors.ToArray();
    m_alertMesh.RecalculateBounds();
  }

  private void Update()
  {
    float readyToFire = m_fireTimerActive ? (Time.time - m_fireTimerStart) / m_fireTimerDuration : 0;
    m_reticleAnimator.Update();
    m_alertAnimator.Update();
    GenerateReticle(m_reticleAnimator.current.radius);
    GenerateAlert(m_alertAnimator.current.radius, readyToFire, Blink(4, Time.time));
    transform.position = Camera.main.transform.position + m_zDistance * Camera.main.transform.forward;
    reticle.transform.rotation = Camera.main.transform.rotation * m_reticleAnimator.current.rotation;
    alert.transform.rotation = Camera.main.transform.rotation * m_alertAnimator.current.rotation;
  }

  private void Start()
  {
    // Set up reticle game object
    m_reticleMesh = reticle.AddComponent<MeshFilter>().mesh;
    MeshRenderer reticleRenderer = reticle.AddComponent<MeshRenderer>();
    reticleRenderer.material = material;
    reticleRenderer.material.color = Color.white;

    // Set up fire alert game object
    m_alertMesh = alert.AddComponent<MeshFilter>().mesh;
    MeshRenderer alertRenderer = alert.AddComponent<MeshRenderer>();
    alertRenderer.material = material;
    alertRenderer.material.color = Color.white;

    // Precompute some values
    m_viewportRadius = m_zDistance * Mathf.Tan(Mathf.Deg2Rad * Camera.main.fieldOfView);

    // Kick off initial animation
    XformAnimator.OnCompleteCallback RRecenter3 = () => m_reticleAnimator.StartRotation(0, 1, null, Sigmoid01);
    XformAnimator.OnCompleteCallback RTiltLeft2 = () => m_reticleAnimator.StartRotation(-45, 1, RRecenter3, Sigmoid01);
    XformAnimator.OnCompleteCallback RTiltRight1 = () => m_reticleAnimator.StartRotation(45, 1, RTiltLeft2, Sigmoid01);
    m_reticleAnimator.StartAnimation(new Xform(m_viewportRadius, Quaternion.Euler(0, 0, 90f), Vector3.zero), new Xform(0.2f, Quaternion.identity, Vector3.zero), 1f, RTiltRight1);
    XformAnimator.OnCompleteCallback AStartFireTimer = () => { m_fireTimerActive = true; m_fireTimerStart = Time.time; };
    XformAnimator.OnCompleteCallback ARecenter3 = () => m_alertAnimator.StartRotation(0, 1, AStartFireTimer, Sigmoid01);
    XformAnimator.OnCompleteCallback ATiltLeft2 = () => m_alertAnimator.StartRotation(45, 1, ARecenter3, Sigmoid01);
    XformAnimator.OnCompleteCallback ATiltRight1 = () => m_alertAnimator.StartRotation(-45, 1, ATiltLeft2, Sigmoid01);
    m_alertAnimator.StartAnimation(new Xform(m_viewportRadius, Quaternion.Euler(0, 0, 90f), Vector3.zero), new Xform(0.2f, Quaternion.identity, Vector3.zero), 1f, ATiltRight1);
  }
}
