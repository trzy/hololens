using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity.SpatialMapping;
using HoloLensXboxController;

public class GeoMaker: MonoBehaviour
{
  [Tooltip("Material to render selected patches with")]
  public Material selectedMaterial = null;

  private ControllerInput m_xboxController = null;
  private GameObject m_gameObject = null;
  private Mesh m_mesh = null;
  private MeshRenderer m_meshRenderer = null;
  private PlanarTileSelection m_selection = null;
  private MeshExtruder m_meshExtruder = null;
  private enum State
  {
    Select,
    Extrude,
    Finished
  }
  private State m_state = State.Select;

  private void Update()
  {
    if (!PlayspaceManager.Instance.IsScanningComplete())
      return;

    // Get current joypad axis values
#if UNITY_EDITOR
    float hor = Input.GetAxis("Horizontal");
    float ver = Input.GetAxis("Vertical");
    bool buttonA = Input.GetKeyDown(KeyCode.Joystick1Button0);
    bool buttonB = Input.GetKeyDown(KeyCode.Joystick1Button1);
#else
    m_xboxController.Update();
    float hor = m_xboxController.GetAxisLeftThumbstickX();
    float ver = m_xboxController.GetAxisLeftThumbstickY();
    bool buttonA = m_xboxController.GetButtonDown(ControllerButton.A);
    bool buttonB = m_xboxController.GetButtonDown(ControllerButton.B);
#endif

    float delta = (Mathf.Abs(ver) > 0.25f ? ver : 0) * Time.deltaTime;

    if (m_state == State.Select)
    {
      m_selection.Raycast(Camera.main.transform.position, Camera.main.transform.forward);
      Tuple<Vector3[], int[]> meshData = m_selection.GenerateMeshData();
      if (meshData.first.Length > 0)
      {
        m_mesh.Clear();
        m_mesh.vertices = meshData.first;
        m_mesh.triangles = meshData.second;
        m_mesh.RecalculateBounds();
        //TODO: make a GetTransform function
        m_gameObject.transform.rotation = m_selection.rotation;
        m_gameObject.transform.position = m_selection.position;
        m_gameObject.transform.localScale = m_selection.scale;
        if (buttonA)
        {
          m_state = State.Extrude;
          m_meshExtruder = new MeshExtruder(m_selection);
        }
      }
    }
    else if (m_state == State.Extrude)
    {
      m_meshExtruder.extrudeLength += delta;
      Tuple<Vector3[], int[]> meshData = m_meshExtruder.GetMeshData();
      m_mesh.Clear();
      m_mesh.vertices = meshData.first;
      m_mesh.triangles = meshData.second;
      m_mesh.RecalculateBounds();
      m_mesh.RecalculateNormals();
    }
  }

  private void Awake()
  {
    // Create reticle game object and mesh
    m_gameObject = new GameObject("Selected-Patch");
    m_gameObject.transform.parent = null;
    m_mesh = m_gameObject.AddComponent<MeshFilter>().mesh;
    m_meshRenderer = m_gameObject.AddComponent<MeshRenderer>();
    m_meshRenderer.material = selectedMaterial;
    m_meshRenderer.material.color = Color.white;
    m_meshRenderer.enabled = true;

    // Selection surface
    m_selection = new PlanarTileSelection(70);
#if !UNITY_EDITOR
    m_xboxController = new ControllerInput(0, 0.19f);
#endif
  }
}
