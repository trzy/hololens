/*
 * Notes on TextMesh
 * -----------------
 * For lack of a better place to put this, a description of how to precisely
 * control text size follows here. The discussion assumes character size is
 * fixed to 1.
 * 
 * There are 2835 points per meter but Unity's default text height is 1 Unity
 * unit (1 m) with the default font size of 13 pt (see 
 * https://github.com/Microsoft/HoloToolkit-Unity/issues/331). Therefore, there
 * should be
 * 
 *    1 / 2835 = 3.53e-4 m/pt 
 *    
 * in real-world units. But Unity's TextMesh by default is sized as
 * 
 *    1 / 13 = 7.69e-2 m/pt
 *
 * To scale down Unity's text to real-world units, apply the scale factor
 * 
 *    (1 / 2835) / (1 / 13) = 0.004585537918871252 ~= .005 = scale
 *
 * Additionally, for reasons unknown, it appears that each line occupies 1.5x
 * the height that would be computed from font point size alone. 
 * 
 * To compute the height of a given line in meters:
 * 
 *    height = scale * (1 / 13) * (font size in points) * 1.5
 */

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
