using UnityEngine;
using UnityEngine.VR.WSA;
using System.Collections;

public class SpatialMap : MonoBehaviour
{
  public SpatialMappingRenderer m_renderer = null;

  void Start()
  {
    m_renderer = GetComponent<SpatialMappingRenderer>();
  }

  void Update()
  {
    if (m_renderer.FreezeMeshUpdates)
      return;
    if (Time.time > 10.0f)
      m_renderer.FreezeMeshUpdates = true;
  }
}
