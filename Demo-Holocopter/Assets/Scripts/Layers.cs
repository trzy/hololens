// Manages both layers *and* tags

using UnityEngine;
using System.Collections;

public class Layers : HoloToolkit.Unity.Singleton<Layers>
{
  [Tooltip("Tag to apply to each SurfacePlane. Must be a tag predefined in project.")]
  public string surfacePlaneTag = "SurfacePlane";

  // Spatial meshes and surface planes
  public int spatial_mesh_layer
  {
    get { return HoloToolkit.Unity.SpatialMapping.SpatialMappingManager.Instance.PhysicsLayer; }
  }

  public int spatial_mesh_layer_mask
  {
    get { return 1 << spatial_mesh_layer; }
  }

  // Solid, physical game objects (vehicles, buildings, etc.)
  public int object_layer
  {
    get { return m_object_layer; }
  }

  public int object_layer_mask
  {
    get { return 1 << object_layer; }
  }

  // Layers containing physical objects and surfaces that can collide (i.e.,
  // objects that can physically interact)
  public int collidable_layers_mask
  {
    get { return spatial_mesh_layer_mask | object_layer_mask; }
  }

  public bool IsSpatialMeshLayer(int layer)
  {
    return layer == spatial_mesh_layer;
  }

  public bool IsObjectLayer(int layer)
  {
    return layer == object_layer;
  }

  public bool IsCollidableLayer(int layer)
  {
    int layer_mask = 1 << layer;
    return (layer_mask & collidable_layers_mask) != 0;
  }

  private int m_object_layer;

  private new void Awake()
  {
    base.Awake();
    m_object_layer = LayerMask.NameToLayer("Default");
  }
}