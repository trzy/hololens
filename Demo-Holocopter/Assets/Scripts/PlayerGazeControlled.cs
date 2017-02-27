using UnityEngine;
using UnityEngine.VR.WSA;
using UnityEngine.VR.WSA.Input;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class PlayerGazeControlled: MonoBehaviour
{
  public Helicopter helicopter;
  public GameObject cursor1;
  public GameObject cursor2;
  public GameObject testHole;

  enum State
  {
    Scanning,
    FinalizeScan,
    Playing
  };

  private GestureRecognizer m_gestureRecognizer = null;
  private GameObject        m_gazeTarget = null;
  private RaycastHit        m_hit;
  private State             m_state;

  private void OnTapEvent(InteractionSourceKind source, int tapCount, Ray headRay)
  {
    switch (m_state)
    {
    case State.Scanning:
      SetState(State.FinalizeScan);
      break;
    case State.Playing:
      /*
      //TEST: place a bullet hole wherever we are looking
      float distance = 5.0f;
      int layerMask = Layers.Instance.collidableLayersMask;
      RaycastHit hit;
      if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, distance, layerMask))
      {
        Debug.Log("hit point=" + hit.point);
        ParticleEffectsManager.Instance.CreateBulletHole(hit.point, hit.normal, null);
      }
      if (m_gazeTarget == null)
      {
      }
      else if (m_gazeTarget == helicopter.gameObject)
      {
      }
      else
      {
      }
      */
      break;
    }
  }

  void SetState(State state)
  {
    m_state = state;
    switch (state)
    {
    case State.Scanning:
      PlayspaceManager.Instance.StartScanning();
      break;
    case State.FinalizeScan:
      System.Action OnScanComplete = () => 
      {
        LevelManager.Instance.GenerateLevel();
        SetState(State.Playing);
      };
      PlayspaceManager.Instance.OnScanComplete += OnScanComplete;
      PlayspaceManager.Instance.StopScanning();
      break;
    case State.Playing:
      helicopter.SetControlMode(Helicopter.ControlMode.Player);
      break;
    }
  }

  void Start()
  {
    m_gestureRecognizer = new GestureRecognizer();
    m_gestureRecognizer.SetRecognizableGestures(GestureSettings.Tap);
    m_gestureRecognizer.TappedEvent += OnTapEvent;
    m_gestureRecognizer.StartCapturingGestures();
    SetState(State.Scanning);
  }

  private GameObject FindGazeTarget(out RaycastHit hit, float distance, int layerMask)
  {
    //TODO: This code assumes that the collider is in a child object. If this is not the case,
    //      the code fails.
    if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, distance, layerMask))
    {
      if (hit.collider.gameObject.CompareTag(Layers.Instance.surfacePlaneTag))
      {
        HoloToolkit.Unity.SpatialMapping.SurfacePlane p = hit.collider.gameObject.GetComponent<HoloToolkit.Unity.SpatialMapping.SurfacePlane>();
        if (p.PlaneType == HoloToolkit.Unity.SpatialMapping.PlaneTypes.Wall && Input.GetButtonDown("Fire2"))
        {
          Debug.Log("hit point=" + hit.point);
          ParticleEffectsManager.Instance.CreateBulletHole(hit.point, hit.normal, p);
        }
      }
      /*
      GameObject target = hit.collider.transform.parent.gameObject;
      m_gazeTarget = target;
      if (target == null)
      {
        Debug.Log("ERROR: CANNOT IDENTIFY RAYCAST OBJECT");
        return null;
      }
      cursor1.transform.position = hit.point + hit.normal * 0.01f;
      cursor1.transform.forward = hit.normal;
      return target.activeSelf ? target : null;
      */
    }
    return null;
  }

  private void UnityEditorUpdate()
  {
#if UNITY_EDITOR
    // Simulate air tap
    if (Input.GetKeyDown(KeyCode.Return))
    {
      Ray headRay = new Ray(transform.position, transform.forward);
      OnTapEvent(InteractionSourceKind.Hand, 1, headRay);
    }
#endif
  }

  void Update()
  {
    if (m_state == State.Playing)
    {
      int layerMask = Layers.Instance.collidableLayersMask;
      GameObject target = FindGazeTarget(out m_hit, 5.0f, layerMask);
      if (target != null && target != helicopter.gameObject)
      {
        int targetLayerMask = 1 << target.layer;
        if ((targetLayerMask & layerMask) != 0)
        {
          cursor2.transform.position = m_hit.point + m_hit.normal * 0.5f;
          cursor2.transform.forward = -transform.forward;
          //helicopter.FlyToPosition(m_hit.point + m_hit.normal * 0.5f);
        }
      }
      else if (target == null)
      {
        // Hit nothing -- move toward point on ray, 2m out
        //helicopter.FlyToPosition(transform.position + transform.forward * 2.0f);
      }
    }
    UnityEditorUpdate();
  }
}
