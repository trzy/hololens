using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VR.WSA;
using UnityEngine.EventSystems;
using UnityEngine.AI;
using HoloToolkit.Unity.InputModule;

public class GameController: MonoBehaviour, IInputClickHandler
{
  public GameObject agentPrefab;

  enum State
  {
    Init,
    Scanning,
    FinalizeScan,
    GenerateLevel,
    Playing
  }

  private State m_state = State.Init;

  private void OnScanComplete()
  {
    SetState(State.GenerateLevel);
  }

  private void SetState(State state)
  {
    m_state = state;
    switch (state)
    {
      case State.Scanning:
        Debug.Log("State: Scanning");
        PlayspaceManager.Instance.StartScanning();
        break;
      case State.FinalizeScan:
        Debug.Log("State: FinalizeScan");
        PlayspaceManager.Instance.OnScanComplete += OnScanComplete;
        PlayspaceManager.Instance.StopScanning();
        break;
      case State.GenerateLevel:
        LevelManager.Instance.GenerateLevel(() => SetState(State.Playing) );
        Debug.Log("State: GenerateLevel");
        break;
      case State.Playing:
        Debug.Log("State: Playing");
        break;
    }
  }

  public void OnInputClicked(InputClickedEventData eventData)
  {
    Debug.Log("CLICK!");
    switch (m_state)
    {
      case State.Scanning:
        SetState(State.FinalizeScan);
        break;
      case State.Playing:
        break;
    }
  }

#if UNITY_EDITOR
  private void Update()
  {
    // Simulate air tap
    if (Input.GetKeyDown(KeyCode.Return))
    {
      Ray headRay = new Ray(transform.position, transform.forward);
      OnInputClicked(new InputClickedEventData(EventSystem.current));
    }
  }
#endif

  private void Start()
  {
    InputManager.Instance.AddGlobalListener(this.gameObject);
    SetState(State.Scanning);
  }
}
