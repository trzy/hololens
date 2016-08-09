using UnityEngine;
using UnityEngine.VR.WSA.Input;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class PlayerHolographic : MonoBehaviour
{
  public Helicopter         g_helicopter;
  public GameObject         g_waypoints;
  public GameObject         g_waypoint_prefab;
  public Material           g_reticle_material;

  private GestureRecognizer m_gesture_recognizer = null;
  private List<GameObject>  m_waypoints = new List<GameObject>();
  private bool              m_music_played = false;
  private GameObject        m_gaze_target = null;
  private Reticle           m_reticle;

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

  void Start()
  {
    m_gesture_recognizer = new GestureRecognizer();
    m_gesture_recognizer.SetRecognizableGestures(GestureSettings.Tap);
    m_gesture_recognizer.TappedEvent += OnTapEvent;
    m_gesture_recognizer.StartCapturingGestures();
    m_reticle = new Reticle(g_reticle_material);
    //StartCoroutine(BlinkGazeTargetCoroutine());
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
    m_reticle.Draw(m_gaze_target);
  }
}
