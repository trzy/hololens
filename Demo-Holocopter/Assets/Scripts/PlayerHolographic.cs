using UnityEngine;
using UnityEngine.VR.WSA.Input;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class PlayerHolographic : MonoBehaviour
{
  public Helicopter g_helicopter;
  public GameObject g_waypoints;
  public GameObject g_waypoint_prefab;
  private List<GameObject> m_waypoints = new List<GameObject>();
  private GestureRecognizer m_gesture_recognizer = null;
  private bool m_music_played = false;
  private GameObject m_gaze_target = null;
  //TODO: move to own class
  private Mesh m_reticle_mesh;
  public Material g_reticle_material;
  private Plane m_hud_plane;

  private void SetRenderEnable(GameObject obj, bool on)
  {
    Component[] renderers = obj.GetComponentsInChildren<Renderer>();
    foreach (Component renderer in renderers)
      renderer.GetComponent<Renderer>().enabled = on;
  }

  private IEnumerator BlinkGazeTargetCoroutine()
  {
    bool on = true;
    while (true)
    {
      on = !on;
      if (m_gaze_target)
        SetRenderEnable(m_gaze_target, on);
      yield return new WaitForSeconds(0.2f);
    }
  }

  private void OnTapEvent(InteractionSourceKind source, int tap_count, Ray head_ray)
  {
    if (m_gaze_target == null)
    {
      GameObject waypoint = Instantiate(g_waypoint_prefab, transform.position + transform.forward * 1, Quaternion.identity) as GameObject;
      m_waypoints.Add(waypoint);
    }
    else if (m_gaze_target == g_helicopter.gameObject)
    {
      if (!m_music_played && m_waypoints.Any())
      {
        GetComponent<AudioSource>().Play();
        m_music_played = true;
      }
      g_helicopter.TraverseWaypoints(m_waypoints);
    }
  }

  private void BuildReticleMesh()
  {
    float size = 3e-2f; // reticle size is defined at near clip plane
    m_reticle_mesh = new Mesh();
    m_reticle_mesh.vertices = new Vector3[3]
    {
      size * new Vector3(-0.5f, 0.5f, 0),
      size * new Vector3(0.5f, 0.5f, 0),
      size * new Vector3(-0.5f, -0.5f, 0)
    };
    m_reticle_mesh.triangles = new int[] { 0, 1, 2 };
  }

  void Start()
  {
    m_gesture_recognizer = new GestureRecognizer();
    m_gesture_recognizer.SetRecognizableGestures(GestureSettings.Tap);
    m_gesture_recognizer.TappedEvent += OnTapEvent;
    m_gesture_recognizer.StartCapturingGestures();
    BuildReticleMesh();
    //StartCoroutine(BlinkGazeTargetCoroutine());
  }

  void DrawReticleAround(Bounds bounds)
  {
    // Define the HUD plane in camera space
    Vector3 origin = Vector3.zero;
    Vector3 forward = Vector3.forward;
    //Plane hud_plane = new Plane(forward, origin + forward * Camera.main.nearClipPlane);
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
    //float hud_z = Camera.main.nearClipPlane + 0.05f;
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
    g_reticle_material.SetPass(0);

    /*
    float scale = hud_z / Camera.main.nearClipPlane;
    Vector3 scale_vector = new Vector3(scale, scale, scale);
    Graphics.DrawMeshNow(m_reticle_mesh, Matrix4x4.TRS(Camera.main.transform.TransformPoint(top_left), Quaternion.LookRotation(Camera.main.transform.forward, Camera.main.transform.up), scale_vector));
    Graphics.DrawMeshNow(m_reticle_mesh, Matrix4x4.TRS(Camera.main.transform.TransformPoint(top_right), Quaternion.LookRotation(Camera.main.transform.forward, Camera.main.transform.right), scale_vector));
    Graphics.DrawMeshNow(m_reticle_mesh, Matrix4x4.TRS(Camera.main.transform.TransformPoint(bottom_right), Quaternion.LookRotation(Camera.main.transform.forward, -Camera.main.transform.up), scale_vector));
    Graphics.DrawMeshNow(m_reticle_mesh, Matrix4x4.TRS(Camera.main.transform.TransformPoint(bottom_left), Quaternion.LookRotation(Camera.main.transform.forward, -Camera.main.transform.right), scale_vector));
    */
    Graphics.DrawMeshNow(m_reticle_mesh, Camera.main.transform.TransformPoint(top_left), Quaternion.LookRotation(Camera.main.transform.forward, Camera.main.transform.up));
    Graphics.DrawMeshNow(m_reticle_mesh, Camera.main.transform.TransformPoint(top_right), Quaternion.LookRotation(Camera.main.transform.forward, Camera.main.transform.right));
    Graphics.DrawMeshNow(m_reticle_mesh, Camera.main.transform.TransformPoint(bottom_right), Quaternion.LookRotation(Camera.main.transform.forward, -Camera.main.transform.up));
    Graphics.DrawMeshNow(m_reticle_mesh, Camera.main.transform.TransformPoint(bottom_left), Quaternion.LookRotation(Camera.main.transform.forward, -Camera.main.transform.right));
  }

  void DrawTargetReticle()
  {
    if (m_gaze_target == null)
      return;
    Bounds bounds = new Bounds(m_gaze_target.transform.position, Vector3.zero);
    foreach (Collider collider in m_gaze_target.GetComponentsInChildren<Collider>())
    {
      bounds.Encapsulate(collider.bounds);
    }
    DrawReticleAround(bounds);
  }

  void Update()
  {
    /*
    GameObject old_gaze_target = m_gaze_target;
    m_gaze_target = null;
    RaycastHit hit;
    if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, 20.0f, Physics.DefaultRaycastLayers))
    {
      GameObject gaze_target = hit.collider.transform.parent.gameObject;
      if (gaze_target.activeSelf)
        m_gaze_target = gaze_target;
    }
    if (old_gaze_target && old_gaze_target != m_gaze_target)
      SetRenderEnable(old_gaze_target, true);
    */
    RaycastHit hit;
    if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, 20.0f, Physics.DefaultRaycastLayers))
    {
      GameObject gaze_target = hit.collider.transform.parent.gameObject;
      if (gaze_target.activeSelf)
        m_gaze_target = gaze_target;
    }
    else
      m_gaze_target = null;
  }

  void OnPostRender()
  {
    DrawTargetReticle();
  }
}
