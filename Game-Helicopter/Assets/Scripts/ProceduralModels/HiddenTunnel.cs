/*
 * TODO:
 * -----
 * - 
  * * - Attach SharedMaterialHelper to objects.
 * - Make navigation obstacle around opening.
 */
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HiddenTunnel: MonoBehaviour
{
  [Tooltip("Prefab of vehicle that tunnel must accomodate.")]
  public GameObject vehiclePrefab;

  [Tooltip("Number of vehicles in tunnel.")]
  public int vehiclesInTunnel = 2;

  [Tooltip("Ramp incline in degrees.")]
  public int rampAngle = 30;

  [Tooltip("Empty space above vehicles in tunnel expressed as percentage of vehicle height.")]
  [Range(0, 1f)]
  public float tunnelHeadroom;

  [Tooltip("Empty space on each vehicle side expressed as a percentage of vehicle width.")]
  [Range(0, 1f)]
  public float tunnelWiggleroom;

  [Tooltip("Spacing between vehicles in tunnel expressed as a percentage of vehicle length.")]
  [Range(0, 1f)]
  public float tunnelVehicleSpacing;

  [Tooltip("Material for tunnel interior, which is visible.")]
  public Material tunnelMaterial;

  [Tooltip("Matterial for occluding hull.")]
  public Material occlusionMaterial;

  private Vector3 m_vehicleDimensions;
  private Vector3 m_tunnelDimensions; // excluding ceiling thickness
  private const float CEILING_THICKNESS = 0.05f;

  private struct ObjectData
  {
    public GameObject obj;
    public MeshCollider collider;
    public Mesh mesh;

    public ObjectData(GameObject o, MeshCollider c, Mesh m)
    {
      obj = o;
      collider = c;
      mesh = m;
    }
  };

  private ObjectData m_tunnelObject;
  private ObjectData m_occluderObject;

  private void CreateBoxCollider()
  {
    Vector3 size = OpeningDimensions();
    size.y = m_tunnelDimensions.y;  // just use tunnel dimensions for height

    BoxCollider box = gameObject.AddComponent<BoxCollider>();
    box.size = size;
    box.center = new Vector3(0, 0.5f * size.y, 0);
  }

  private Vector3 OpeningDimensions()
  {
    /*
     * Gets the width and length (where length is along ramp direction) of the
     * opening above the ramp.
     * 
     *   +-- opening
     *   |
     *   V
     *  <-----> opening length
     * +....... <-- floor level
     * |      / 
     * +     /
     *      /
     *     /
     *    /
     *   /
     *  /
     */

    float tunnelFloorToSurfaceHeight = m_tunnelDimensions.y + CEILING_THICKNESS;
    float length = tunnelFloorToSurfaceHeight / Mathf.Tan(rampAngle * Mathf.Deg2Rad);
    return new Vector3(m_tunnelDimensions.x, 0, length);
  }

  private void CreateMeshes()
  {
    /*
     * Side cross-sectional view of tunnel:
     * ------------------------------------
     * 
     * ...........................+........... <-- floor level
     *  <------ tunnel length --->| <-- spacer quad for ceiling thickness
     * +--------------------------+
     * |  ^
     * |  |
     * |  +-- ceiling quad (unlikely to be visible to player)
     * | 
     * |  <-- rear wall quad (unlikely to be visible to player)
     * +--------------------------+
     *    ^
     *    |
     *    +-- floor quad
     * 
     * Top-down view of tunnel:
     * ------------------------
     * 
     * +--------------------------+
     * |  ^                       | 
     * |  |                       | <-- spacer quad
     * |  +-- side wall quad      |
     * |                          |
     * |                          |
     * +--------------------------+
     *    ^
     *    |
     *    +-- side wall quad
     *
     * Axes and pivot:
     * ---------------
     * 
     * Forward: along length of tunnel
     * Up: toward ceiling
     * Pivot point: center point of hole, above ramp (constructed separately)
     *
     *                                   +-- pivot point
     *                                   |
     *      forward -->                  V
     * ...........................+.............+............
     *                            |            /
     * +--------------------------+          /
     * |                                   /
     * |                                 /
     * |                               /
     * |                             /   <-- ramp
     * |                           /
     * +--------------------------+
     */

    float openingLength = OpeningDimensions().z;
    float tunnelWidth = m_tunnelDimensions.x;
    float tunnelHeight = m_tunnelDimensions.y;  // floor to ceiling (not floor to surface)
    float tunnelLength = m_tunnelDimensions.z;

    Vector3[] vertices = new Vector3[]
    {
      // Ceiling thickness and ceiling vertices, looking down on tunnel with
      // forward being up
      new Vector3(-0.5f * tunnelWidth, 0, -0.5f * openingLength),                   // 0 upper left of ceiling cutaway
      new Vector3(+0.5f * tunnelWidth, 0, -0.5f * openingLength),                   // 1 upper right
      new Vector3(+0.5f * tunnelWidth, -CEILING_THICKNESS, -0.5f * openingLength),  // 2 lower right
      new Vector3(-0.5f * tunnelWidth, -CEILING_THICKNESS, -0.5f * openingLength),  // 3 lower left

      // Back of ceiling and rear wall top
      new Vector3(-0.5f * tunnelWidth, -CEILING_THICKNESS, -0.5f * openingLength - tunnelLength), // 4 left
      new Vector3(+0.5f * tunnelWidth, -CEILING_THICKNESS, -0.5f * openingLength - tunnelLength), // 5 right

      // Rear wall bottom and back of tunnel floor
      new Vector3(-0.5f * tunnelWidth, -CEILING_THICKNESS - tunnelHeight, -0.5f * openingLength - tunnelLength), // 6 left
      new Vector3(+0.5f * tunnelWidth, -CEILING_THICKNESS - tunnelHeight, -0.5f * openingLength - tunnelLength), // 7 right

      // Front of tunnel floor
      new Vector3(-0.5f * tunnelWidth, -CEILING_THICKNESS - tunnelHeight, -0.5f * openingLength), // 8 left
      new Vector3(+0.5f * tunnelWidth, -CEILING_THICKNESS - tunnelHeight, -0.5f * openingLength), // 9 right

      // End (top) of ramp, level with actual floor
      new Vector3(-0.5f * tunnelWidth, 0, 0.5f * openingLength),  // 10 left
      new Vector3(+0.5f * tunnelWidth, 0, 0.5f * openingLength),  // 11 right

      // Back of tunnel, at actual floor level (for occluder)
      new Vector3(-0.5f * tunnelWidth, 0, - 0.5f * openingLength - tunnelLength), // 12 left
      new Vector3(+0.5f * tunnelWidth, 0, - 0.5f * openingLength - tunnelLength), // 13 right

      // Front and bottom of occluder (extends out as far as ramp)
      new Vector3(-0.5f * tunnelWidth, -CEILING_THICKNESS - tunnelHeight, 0.5f * openingLength),  // 14 left
      new Vector3(+0.5f * tunnelWidth, -CEILING_THICKNESS - tunnelHeight, 0.5f * openingLength),  // 15 right
    };

    int[] tunnelTriangles = new int[]
    {
      // Ceiling cutaway 
      1, 0, 3,
      3, 2, 1,
      
      // Ceiling
      2, 3, 4,
      4, 5, 2,
      
      // Back wall
      5, 4, 6,
      6, 7, 5,

      // Floor
      7, 6, 8,
      8, 9, 7,

      // Left wall
      4, 3, 8,
      8, 6, 4,

      // Right wall
      2, 5, 7,
      7, 9, 2,

      // Ramp
      10, 11, 8,
      8, 11, 9,

      // Left wall, ramp
      0, 10, 8,

      // Right wall, ramp
      11, 1, 9
    };

    int[] occluderTriangles = new int[]
    {
      // Top
      0, 1, 13,
      13, 12, 0,

      // Left
      10, 12, 6,
      6, 14, 10,

      // Right
      13, 11, 15,
      15, 7, 13,

      // Bottom
      15, 14, 6,
      6, 7, 15,
      
      // Back of tunnel
      12, 13, 7,
      7, 6, 12,

      // Bottom of ramp
      8, 11, 10,
      9, 11, 8
    };

    // Tunnel interior
    m_tunnelObject.mesh.vertices = vertices;
    m_tunnelObject.mesh.triangles = tunnelTriangles;
    m_tunnelObject.mesh.RecalculateBounds();
    m_tunnelObject.mesh.RecalculateNormals();
    m_tunnelObject.collider.sharedMesh = null;
    m_tunnelObject.collider.sharedMesh = m_tunnelObject.mesh;

    // Occluding hull
    m_occluderObject.mesh.vertices = vertices;
    m_occluderObject.mesh.triangles = occluderTriangles;
    m_occluderObject.mesh.RecalculateBounds();
    m_occluderObject.mesh.RecalculateNormals();
  }

  private ObjectData CreateNewObject(string name, Material material, bool createCollider)
  {
    GameObject obj = new GameObject(name);
    obj.transform.parent = gameObject.transform;
    MeshCollider collider = createCollider ? obj.AddComponent<MeshCollider>() : null;
    Mesh mesh = obj.AddComponent<MeshFilter>().mesh;
    MeshRenderer meshRenderer = obj.AddComponent<MeshRenderer>();
    meshRenderer.material = material;
    meshRenderer.enabled = true;
    return new ObjectData(obj, collider, mesh);
  }

  private void Awake()
  {
    m_vehicleDimensions = Footprint.Measure(vehiclePrefab);
    m_tunnelDimensions = new Vector3(m_vehicleDimensions.x * (1 + tunnelWiggleroom), m_vehicleDimensions.y * (1 + tunnelHeadroom), vehiclesInTunnel * m_vehicleDimensions.z * (1 + tunnelVehicleSpacing));
    m_tunnelObject = CreateNewObject("TunnelInterior", tunnelMaterial, true);
    m_occluderObject = CreateNewObject("TunnelOccluder", occlusionMaterial, false);
    CreateMeshes();
    CreateBoxCollider();
  }
}
