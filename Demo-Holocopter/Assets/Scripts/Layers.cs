// Manages both layers *and* tags

using UnityEngine;
using System.Collections;

public class Layers : HoloToolkit.Unity.Singleton<Layers>
{
  [Tooltip("Tag to apply to each SurfacePlane. Must be a tag predefined in project.")]
  public string surfacePlaneTag = "SurfacePlane";

  // Spatial meshes and surface planes
  public int spatialMeshLayer
  {
    get { return HoloToolkit.Unity.SpatialMapping.SpatialMappingManager.Instance.PhysicsLayer; }
  }

  public int spatialMeshLayerMask
  {
    get { return 1 << spatialMeshLayer; }
  }

  // Solid, physical game objects (vehicles, buildings, etc.)
  public int objectLayer
  {
    get { return m_objectLayer; }
  }

  public int objectLayerMask
  {
    get { return 1 << objectLayer; }
  }

  // Layers containing physical objects and surfaces that can collide (i.e.,
  // objects that can physically interact)
  public int collidableLayersMask
  {
    get { return spatialMeshLayerMask | objectLayerMask; }
  }

  public bool IsSpatialMeshLayer(int layer)
  {
    return layer == spatialMeshLayer;
  }

  public bool IsObjectLayer(int layer)
  {
    return layer == objectLayer;
  }

  public bool IsCollidableLayer(int layer)
  {
    int layerMask = 1 << layer;
    return (layerMask & collidableLayersMask) != 0;
  }

  private int m_objectLayer;

  private new void Awake()
  {
    base.Awake();
    m_objectLayer = LayerMask.NameToLayer("Default");
  }
}