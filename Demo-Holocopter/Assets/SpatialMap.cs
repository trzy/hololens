using UnityEngine;
using UnityEngine.VR.WSA;
using System.Collections;

public class SpatialMap : MonoBehaviour
{
  public Material m_occlusion_material;
  public Material m_rendering_material;
  private SpatialMappingRenderer m_renderer;

  public void Disable()
  {
    m_renderer.FreezeMeshUpdates = true;
    m_renderer.enabled = false;
  }

  public void Scan()
  {
    m_renderer.FreezeMeshUpdates = false;
    m_renderer.CurrentRenderingSetting = SpatialMappingRenderer.RenderingSetting.Material;
    m_renderer.enabled = true;
  }

  public void Occlude()
  {
    m_renderer.FreezeMeshUpdates = true;
    m_renderer.CurrentRenderingSetting = SpatialMappingRenderer.RenderingSetting.Occlusion;
    m_renderer.enabled = true;
  }

  void Awake()
  {
    m_renderer = gameObject.AddComponent<SpatialMappingRenderer>();
    m_renderer.OcclusionMaterial = m_occlusion_material;
    m_renderer.RenderingMaterial = m_rendering_material;
    m_renderer.CurrentRenderingSetting = SpatialMappingRenderer.RenderingSetting.Material;
    m_renderer.FreezeMeshUpdates = false;
    m_renderer.LevelOfDetail = SMBaseAbstract.MeshLevelOfDetail.Medium;
    m_renderer.TimeBetweenUpdates = 2.5f;
    m_renderer.NumUpdatesBeforeRemoval = 10;
    m_renderer.UseSphereBounds = false;
    m_renderer.Extents = new Vector3(10, 10, 10);
    Disable();
  }  
}
