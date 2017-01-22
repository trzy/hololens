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
  [Tooltip("Material to render with.")]
  public Material material = null;

  [Tooltip("Distance from camera at which to render.")]
  public float zDistance = 2f;

  public LockIndicatorHelper targetObject
  {
    get { return m_targetObject; }
    set { m_targetObject = value; }
  }

  private LockIndicatorHelper m_targetObject = null;

  private GameObject m_reticleObject = null;
  private MeshRenderer m_reticleRenderer = null;
  private Mesh m_reticleMesh = null;

  private GameObject m_timerObject = null;
  private MeshRenderer m_timerRenderer = null;
  private Mesh m_timerMesh = null;

  private StepwiseAnimator m_radiusAnimation = null;
  private StepwiseAnimator m_rotationAnimation = null;
  private StepwiseAnimator m_timerAnimation = null; // TEMPORARY: use an API for setting this and not an animation
  private bool m_visible = false;

  public bool IsActive()
  {
    return m_visible;
  }

  public void StartLockOnSequence()
  {
    // Set up initial animation
    float viewportRadius = zDistance * Mathf.Tan(Mathf.Deg2Rad * Camera.main.fieldOfView);
    float radius = m_targetObject.ComputeCameraSpaceRadiusAt(zDistance);
    float[] radii = new float[] { viewportRadius, radius, radius, radius, radius };
    float[] rotations = new float[] { 0, 0, 45, -45, 0 };
    float[] timeDeltas = new float[] { 1, 1, 1, 1 };
    StepwiseAnimator.TimeScaleFunction[] timeScale =
            new StepwiseAnimator.TimeScaleFunction[] { null, StepwiseAnimator.Sigmoid01, StepwiseAnimator.Sigmoid01, StepwiseAnimator.Sigmoid01 };
    m_radiusAnimation = new StepwiseAnimator(radii, timeDeltas, timeScale);
    m_rotationAnimation = new StepwiseAnimator(rotations, timeDeltas, timeScale);
    float[] countdown = new float[] { -1, -1, -1, -1, 0, 1 };
    float[] timeDeltas2 = new float[] { 1, 1, 1, 1, 10 };
    m_timerAnimation = new StepwiseAnimator(countdown, timeDeltas2, null);

    SetVisible(true);
  }

  private void SetVisible(bool visible)
  {
    m_reticleRenderer.enabled = visible;
    m_timerRenderer.enabled = visible;
    m_visible = visible;
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
    ProceduralMeshUtils.DrawArc(verts, triangles, colors, startColor, endColor, innerRadius, outerRadius, 55f, 125f, segments);
    ProceduralMeshUtils.DrawArc(verts, triangles, colors, startColor, endColor, innerRadius, outerRadius, 145f, 215f, segments);
    ProceduralMeshUtils.DrawArc(verts, triangles, colors, startColor, endColor, innerRadius, outerRadius, 235f, 305f, segments);
    ProceduralMeshUtils.DrawArc(verts, triangles, colors, startColor, endColor, innerRadius, outerRadius, 325f, 395f, segments);

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
      ProceduralMeshUtils.DrawArc(verts, triangles, colors, color, color, innerRadius, outerRadius, theta - 0.5f * tickWidth, theta + 0.5f * tickWidth, segments);
      theta += tickPitch;
    }
    m_timerMesh.vertices = verts.ToArray();
    m_timerMesh.triangles = triangles.ToArray();
    m_timerMesh.colors32 = colors.ToArray();
    m_timerMesh.RecalculateBounds();  // absolutely needed because Unity may erroneously think we are off-screen at times
  }

  private Vector3 ProjectVectorOntoPlane(Vector3 u, Vector3 planeNormal)
  {
    return u - (Vector3.Dot(u, planeNormal) / Vector3.SqrMagnitude(planeNormal)) * planeNormal;
  }

  private void OnDestroy()
  {
    //TODO: clean up material? (what about mesh?) (need to test whether number of materials increases and leaks)
  }

  private void UpdateReticleTransform()
  {
    if (!m_visible)
      return;
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
    if (m_targetObject == null)
    {
      return;
    }

    UpdateReticleTransform();
    bool visible = GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(Camera.main), m_targetObject.GetComponent<BoxCollider>().bounds);
    m_reticleRenderer.enabled = visible;
    m_timerRenderer.enabled = visible;
    transform.position = m_targetObject.ComputeCameraSpaceCentroidAt(zDistance);

    // Project world-space up vector onto current camera-local xy plane so that
    // reticle orientation stays fixed even when user tilts head left or right
    transform.up = ProjectVectorOntoPlane(Vector3.up, Camera.main.transform.forward);
    transform.forward = Camera.main.transform.forward;
  }

  private void Awake()
  {
    // Create reticle game object and mesh
    m_reticleObject = new GameObject("LockIndicator-Reticle");
    m_reticleObject.transform.parent = transform;
    m_reticleMesh = m_reticleObject.AddComponent<MeshFilter>().mesh;
    m_reticleRenderer = m_reticleObject.AddComponent<MeshRenderer>();
    m_reticleRenderer.material = material;
    m_reticleRenderer.material.color = Color.white;
    //GenerateReticle(m_targetObject.ComputeCameraSpaceRadiusAt(zDistance));

    // Create wn game object and mesh
    m_timerObject = new GameObject("LockIndicator-Timer");
    m_timerObject.transform.parent = transform;
    m_timerMesh = m_timerObject.AddComponent<MeshFilter>().mesh;
    m_timerRenderer = m_timerObject.AddComponent<MeshRenderer>();
    m_timerRenderer.material = material;
    m_timerRenderer.material.color = Color.white;

    SetVisible(false);
  }
}
