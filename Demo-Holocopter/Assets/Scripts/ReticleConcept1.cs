/*
 * TODO:
 * -----
 * - Implement a vector class so we can reuse the same array each frame. See
 *   ResizableArray here: http://stackoverflow.com/questions/4972951/listt-to-t-without-copying
 * - Investigate garbage collection and memory usage when generating new mesh
 *   each frame.
 * - Move mesh building functions (DrawArc(), etc.) into their own class
 *   and have scripts for different kinds of reticles.
 */
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ReticleConcept1: MonoBehaviour
{
  public GameObject reticle = null;
  public GameObject lockIndicator = null;
  public Material material = null;

  private Mesh m_reticleMesh = null;
  private Mesh m_lockIndicatorMesh = null;
  private const float m_zDistance = 2f;
  private float m_viewportRadius; // distance from center to extreme right/left of viewport at z distance

  private struct ReticleAnimation
  {
    public delegate void AnimationCompleteCallback();

    public float startRadius;
    public float endRadius;
    public Quaternion startRotation;
    public Quaternion endRotation;
    public float animationDuration;
    public float startTime;
    public float timeElapsed;
    public float t;
    public bool animate;
    public AnimationCompleteCallback OnComplete;

    public void Update(out Quaternion extraRotation)
    {
      if (animate)
      {
        float now = Time.time;
        timeElapsed = now - startTime;
        t = timeElapsed / animationDuration;
        extraRotation = Quaternion.Lerp(startRotation, endRotation, t);
        if (now >= (startTime + animationDuration))
        {
          animate = false;
          if (null != OnComplete)
          {
            OnComplete();
          }
        }
      }
      else
      {
        extraRotation = endRotation;
      }
    }

    public void StartAnimation(float pStartRadius, float pEndRadius, float startRotationDeg, float endRotationDeg, float seconds, AnimationCompleteCallback pOnComplete = null)
    {
      startRadius = pStartRadius;
      endRadius = pEndRadius;
      startRotation = Quaternion.Euler(0, 0, startRotationDeg);
      endRotation = Quaternion.Euler(0, 0, endRotationDeg);
      animationDuration = seconds;
      startTime = Time.time;
      timeElapsed = 0;
      t = 0;
      animate = true;
      OnComplete = pOnComplete;
    }

    public ReticleAnimation(bool pAnimate)
    {
      startRadius = 0;
      endRadius = 0;
      startRotation = Quaternion.identity;
      endRotation = Quaternion.identity;
      animationDuration = 0;
      startTime = 0;
      timeElapsed = 0;
      t = 0;
      animate = pAnimate;
      OnComplete = null;
    }
  }

  private ReticleAnimation m_reticleAnimation = new ReticleAnimation(false);
  private ReticleAnimation m_lockIndicatorAnimation = new ReticleAnimation(false);


  private void DrawArcTriangle(List<Vector3> verts, List<int> triangles, List<Color32> colors, Color32 color, float angleDeg, float centerRadialDistance, float centerLength, float radialArcDeg)
  {
    // Generate points at radial angle = 0
    float halfAngle = 0.5f * radialArcDeg * Mathf.Deg2Rad;
    float side = (centerRadialDistance + 0.5f * centerLength) / Mathf.Cos(halfAngle);
    Vector3 p1 = new Vector3(centerRadialDistance - 0.5f * centerLength, 0, 0);
    Vector3 p2 = p1 + new Vector3(centerLength, side * Mathf.Sin(halfAngle), 0);
    Vector3 p3 = new Vector3(p2.x, -p2.y, 0);
    
    // Rotate them into position
    Quaternion rotation = Quaternion.Euler(0, 0, angleDeg);
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

  private void DrawArc(List<Vector3> verts, List<int> triangles, List<Color32> colors, Color32 fromColor, Color32 toColor, float innerRadius, float outerRadius, float fromDeg, float toDeg, int numSegments)
  {
    //TODO: special case 360 degree case?
    float step = Mathf.Deg2Rad * (toDeg - fromDeg) / numSegments;
    float tStep = 1.0f / numSegments;
    float fromAngle = Mathf.Deg2Rad * fromDeg;
    float toAngle = Mathf.Deg2Rad * toDeg;
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
    float thickness = .03f;
    float innerRadius = radius - 0.5f * thickness;
    float outerRadius = radius + 0.5f * thickness;
    List<Vector3> verts = new List<Vector3>();
    List<int> triangles = new List<int>();
    List<Color32> colors = new List<Color32>();
    Color32 black = new Color32(0xff, 0, 0, 0xc0);
    Color32 red = new Color32(0xff, 0, 0, 0xc0);
    Color32 blue = new Color32(101, 215, 252, 0xc0);
    Color32 orange = new Color32(0xff, 0x8c, 0, 0xff);
    Color32 gold = new Color32(0xff, 0xd7, 0x00, 0xff);

    DrawArc(verts, triangles, colors, black, red, innerRadius, outerRadius, 55f, 125f, 5);
    DrawArc(verts, triangles, colors, black, red, innerRadius, outerRadius, 145f, 215f, 5);
    DrawArc(verts, triangles, colors, black, red, innerRadius, outerRadius, 235f, 305f, 5);
    DrawArc(verts, triangles, colors, black, red, innerRadius, outerRadius, 325f, 395f, 5);
    m_reticleMesh.vertices = verts.ToArray();
    m_reticleMesh.triangles = triangles.ToArray();
    m_reticleMesh.colors32 = colors.ToArray();
  }

  private void GenerateLockingIndicator(float radius, Color32 color)
  {
    float length = .1f;
    float arcDeg = 10f;
    List<Vector3> verts = new List<Vector3>();
    List<int> triangles = new List<int>();
    List<Color32> colors = new List<Color32>();
    /*
    Color32 black = new Color32(0xff, 0, 0, 0xff);
    Color32 red = new Color32(0xff, 0, 0, 0xff);
    Color32 orange = new Color32(0xff, 0x8c, 0, 0xff);
    Color32 gold = new Color32(0xff, 0xd7, 0x00, 0xff);
    */

    DrawArcTriangle(verts, triangles, colors, color, 90f + 45f, radius, length, arcDeg);
    DrawArcTriangle(verts, triangles, colors, color, 180f + 45f, radius, length, arcDeg);
    DrawArcTriangle(verts, triangles, colors, color, 270f + 45f, radius, length, arcDeg);
    DrawArcTriangle(verts, triangles, colors, color, 360f + 45f, radius, length, arcDeg);
    m_lockIndicatorMesh.vertices = verts.ToArray();
    m_lockIndicatorMesh.triangles = triangles.ToArray();
    m_lockIndicatorMesh.colors32 = colors.ToArray();
  }

  private void StartLockAnimation()
  {
    Debug.Log("Starting lock animation");
    m_lockIndicatorAnimation.StartAnimation(0, 0, 0, -45f, 3f);
  }

  private float Blink(float hz, float time)
  {
    float a = Mathf.PI * 2 * hz;
    return 0.5f * (1 + Mathf.Cos(a * time));
  }

  private void Update()
  {
    Quaternion reticleRotation;
    m_reticleAnimation.Update(out reticleRotation);
    float radius = Mathf.Lerp(m_reticleAnimation.startRadius, m_reticleAnimation.endRadius, m_reticleAnimation.t);
    GenerateReticle(radius);

    Quaternion lockIndicatorRotation;
    m_lockIndicatorAnimation.Update(out lockIndicatorRotation);
    Color32 redHigh = new Color32(0xff, 0, 0, 0xff);
    Color32 redLow = new Color32(0xff, 0, 0, 0xa0);
    float intensity = Blink(4, m_lockIndicatorAnimation.timeElapsed);
    GenerateLockingIndicator(radius, Color32.Lerp(redLow, redHigh, intensity));

    // All reticle components are at same position but have differing rotations
    transform.position = Camera.main.transform.position + 2f * Camera.main.transform.forward;
    reticle.transform.rotation = Camera.main.transform.rotation * reticleRotation;
    lockIndicator.transform.rotation = Camera.main.transform.rotation * lockIndicatorRotation;
  }

  private void Start()
  {
    // Set up the game objects
    m_reticleMesh = reticle.AddComponent<MeshFilter>().mesh;
    MeshRenderer reticleRenderer = reticle.AddComponent<MeshRenderer>();
    reticleRenderer.material = material;
    reticleRenderer.material.color = Color.white;
    m_lockIndicatorMesh = lockIndicator.AddComponent<MeshFilter>().mesh;
    MeshRenderer lockIndicatorRenderer = lockIndicator.AddComponent<MeshRenderer>();
    lockIndicatorRenderer.material = material;
    lockIndicatorRenderer.material.color = Color.white;
    lockIndicator.transform.position -= new Vector3(0, 0, 1e-5f);

    // Precompute some values
    m_viewportRadius = m_zDistance * Mathf.Tan(Mathf.Deg2Rad * Camera.main.fieldOfView);
    m_reticleAnimation.StartAnimation(m_viewportRadius, .2f, 90f, 0f, .4f, () => StartLockAnimation());
  }
}
