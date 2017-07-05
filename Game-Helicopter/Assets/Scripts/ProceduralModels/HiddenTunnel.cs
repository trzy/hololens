/*
 * TODO:
 * -----
 * - Attach SharedMaterialHelper to objects.
 * - Make navigation obstacle around opening.
 */
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity.SpatialMapping;

public class HiddenTunnel : MonoBehaviour
{
  public enum Type
  {
    StraightRamp,
    SmoothRamp,
    RampOnly
  }

  [Tooltip("Prefab of vehicle that tunnel must accomodate.")]
  public GameObject vehiclePrefab;

  [Tooltip("Maximum number of vehicles in tunnel (used to size the tunnel).")]
  public int maxVehiclesInTunnel = 2;

  [Tooltip("Number of vehicles to spawn (clamped to maximum number).")]
  public int numVehiclesToSpawn = 1;

  [Tooltip("Tunnel geometry type.")]
  public Type type = Type.StraightRamp;

  [Tooltip("Ramp incline in degrees.")]
  public int rampAngle = 30;

  [Tooltip("Number of segments for smooth ramps only.")]
  public int numRampSegments = 3;

  [Tooltip("Empty space above vehicles in tunnel expressed as percentage of vehicle height.")]
  [Range(0, 1f)]
  public float tunnelHeadroom;

  [Tooltip("Empty space on each vehicle side expressed as a percentage of vehicle width.")]
  [Range(0, 1f)]
  public float tunnelWiggleroom;

  [Tooltip("Spacing between vehicles in tunnel expressed as a percentage of vehicle length.")]
  [Range(0, 1f)]
  public float tunnelVehicleSpacing;

  [Tooltip("Physics layer to use for tunnel geometry.")]
  public string layerName = "Tunnel";

  [Tooltip("Material for tunnel interior, which is visible.")]
  public Material tunnelMaterial;

  [Tooltip("Matterial for occluding hull.")]
  public Material occlusionMaterial;

  [Tooltip("Allows tunnel to be instantiated in the scene hierarchy immediately for debugging purposes. Does not use SurfacePlaneDeformationManager.")]
  public bool debugMode = false;

  private const float CEILING_THICKNESS = 0.05f;

  private Vector3 m_vehicleDimensions = Vector3.zero;

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

  private interface TunnelBuilderInterface
  {
    Vector3 OpeningDimensions();
    void CreateMeshes();
    OrientedBoundingBox CreateEmbeddableOBB(GameObject topLevelObject);
    void SpawnVehicles(GameObject vehiclePrefab, string layerName, int numVehicles, float vehicleSpacingFactor);
  }

  private class TunnelWithStraightRamp : TunnelBuilderInterface
  {
    private ObjectData m_tunnelObject;
    private ObjectData m_occluderObject;
    private Vector3 m_tunnelDimensions;
    private Vector3 m_vehicleDimensions;
    private float m_rampAngle;

    public Vector3 OpeningDimensions()
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
      float length = tunnelFloorToSurfaceHeight / Mathf.Tan(m_rampAngle * Mathf.Deg2Rad);
      return new Vector3(m_tunnelDimensions.x, 0, length);
    }

    public void CreateMeshes()
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
        new Vector3(+0.5f * tunnelWidth, -CEILING_THICKNESS - tunnelHeight, 0.5f * openingLength)   // 15 right
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

    public OrientedBoundingBox CreateEmbeddableOBB(GameObject topLevelObject)
    {
      // Create bounding box in the local coordinate system of the tunnel as if
      // for a BoxCollider
      Vector3 openingDimensions = OpeningDimensions();
      Vector3 size = m_tunnelDimensions + CEILING_THICKNESS * Vector3.up + openingDimensions.z * Vector3.forward;

      // Pivot point is at center of opening but box needs to be at center of
      // object
      Vector3 center = new Vector3(0, -0.5f * (m_tunnelDimensions.y + CEILING_THICKNESS), +0.5f * openingDimensions.z - 0.5f * size.z);

      // Create OBB suitable for embedding object into the floor
      return CreateEmbeddableOBBFromLocalOBB(center, size, topLevelObject.transform);
    }

    public void SpawnVehicles(GameObject vehiclePrefab, string layerName, int numVehicles, float vehicleSpacingFactor)
    {
      float tunnelRearWall = -(m_tunnelDimensions.z + 0.5f * OpeningDimensions().z);
      float vehiclePitch = m_vehicleDimensions.z * (1 + vehicleSpacingFactor);
      float z = tunnelRearWall + 0.5f * vehiclePitch;
      float x = 0;
      float y = -CEILING_THICKNESS - m_tunnelDimensions.y;
      for (int i = 0; i < numVehicles; i++, z += vehiclePitch)
      {
        Vector3 localPosition = new Vector3(x, y, z);
        GameObject vehicle = Instantiate(vehiclePrefab, m_tunnelObject.obj.transform.TransformPoint(localPosition), m_tunnelObject.obj.transform.rotation);
        SetVehicleLayer(vehicle, layerName);
      }
    }

    public TunnelWithStraightRamp(ObjectData tunnelObject, ObjectData occluderObject, Vector3 tunnelDimensions, Vector3 vehicleDimensions, float rampAngle)
    {
      m_tunnelObject = tunnelObject;
      m_occluderObject = occluderObject;
      m_tunnelDimensions = tunnelDimensions;
      m_vehicleDimensions = vehicleDimensions;
      m_rampAngle = rampAngle;
    }
  }

  private class TunnelWithSmoothRamp : TunnelBuilderInterface
  {
    private ObjectData m_tunnelObject;
    private ObjectData m_occluderObject;
    private Vector3 m_tunnelDimensions;
    private Vector3 m_vehicleDimensions;
    private float m_rampAngle;
    private float m_numRampSegments;

    public Vector3 OpeningDimensions()
    {
      /*
       * Each of the ramp segments has the same angle. We choose the base length
       * to be variable. The height is known and to solve for the base, we can
       * use the tangent of each successive segment:
       * 
       * theta = rampAngle / numSegments
       * height = base / (tan(theta) + tan(2*theta) + ... + tan(numSegments*theta))
       */

      float theta = (m_rampAngle * Mathf.Deg2Rad) / m_numRampSegments;
      float rampHeight = CEILING_THICKNESS + m_tunnelDimensions.y;
      float tanSum = 0;
      for (int i = 0; i < m_numRampSegments; i++)
      {
        tanSum += Mathf.Tan((i + 1) * theta);
      }
      float segmentBaseLength = rampHeight / tanSum;
      return new Vector3(m_tunnelDimensions.x, 0, segmentBaseLength * m_numRampSegments);
    }

    public void CreateMeshes()
    {
      /*
       * Almost identical to the normal tunnel with straight ramp except that the
       * ramp here is broken into a number of segments that together sum to the
       * total ramp angle. This is in case the ramp angle is too steep for
       * vehicle colliders to interact with. Each segment has the same angle and
       * length. This of course means that the total ramp length will be longer
       * than an equivalent straight ramp.
       */

      float openingLength = OpeningDimensions().z;
      float tunnelWidth = m_tunnelDimensions.x;
      float tunnelHeight = m_tunnelDimensions.y;  // floor to ceiling (not floor to surface)
      float tunnelLength = m_tunnelDimensions.z;

      List<Vector3> vertices = new List<Vector3>()
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

        // Front of tunnel floor <---TODO: these are just the first two ramp verts. Adjust accordingly
        new Vector3(-0.5f * tunnelWidth, -CEILING_THICKNESS - tunnelHeight, -0.5f * openingLength), // 8 left
        new Vector3(+0.5f * tunnelWidth, -CEILING_THICKNESS - tunnelHeight, -0.5f * openingLength), // 9 right

        // End (top) of ramp, level with actual floor <-- TODO these are the last two ramp vertices
        new Vector3(-0.5f * tunnelWidth, 0, 0.5f * openingLength),  // 10 left
        new Vector3(+0.5f * tunnelWidth, 0, 0.5f * openingLength),  // 11 right

        // Back of tunnel, at actual floor level (for occluder)
        new Vector3(-0.5f * tunnelWidth, 0, - 0.5f * openingLength - tunnelLength), // 12 left
        new Vector3(+0.5f * tunnelWidth, 0, - 0.5f * openingLength - tunnelLength), // 13 right

        // Front and bottom of occluder (extends out as far as ramp)
        new Vector3(-0.5f * tunnelWidth, -CEILING_THICKNESS - tunnelHeight, 0.5f * openingLength),  // 14 left
        new Vector3(+0.5f * tunnelWidth, -CEILING_THICKNESS - tunnelHeight, 0.5f * openingLength),  // 15 right

        // Ramp
        // ... generated dynamically ...
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

        // In front of ramp
        11, 10, 14,
        14, 15, 11
      };

      List<int> tunnelTriangles = new List<int>()
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

        // Left wall, ramp
        0, 10, 8,
        8, 10, 14,

        // Right wall, ramp
        11, 1, 9,
        9, 15, 11,

        // Ramp
        // ... generated dynamically ...
      };

      // Build the ramp
      float segmentBaseLength = openingLength / m_numRampSegments;
      float theta = m_rampAngle * Mathf.Deg2Rad / m_numRampSegments;
      int triIdx = vertices.Count;
      vertices.Add(vertices[8]);  // left
      vertices.Add(vertices[9]);  // right
      for (int i = 0; i < m_numRampSegments; i++)
      {
        Vector3 lastLeftVert = vertices[vertices.Count - 2];
        Vector3 lastRightVert = vertices[vertices.Count - 1];

        // Vertical climb
        float deltaY = segmentBaseLength * Mathf.Tan((i + 1) * theta);

        // Add next two vertices, left then right 
        vertices.Add(lastLeftVert + deltaY * Vector3.up + segmentBaseLength * Vector3.forward);
        vertices.Add(lastRightVert + deltaY * Vector3.up + segmentBaseLength * Vector3.forward);

        // Triangle 1
        tunnelTriangles.Add(triIdx + 0);  // lower left
        tunnelTriangles.Add(triIdx + 2);  // upper left
        tunnelTriangles.Add(triIdx + 3);  // upper right

        // Triangle 2
        tunnelTriangles.Add(triIdx + 3);  // upper right
        tunnelTriangles.Add(triIdx + 1);  // lower right
        tunnelTriangles.Add(triIdx + 0);  // lower left

        triIdx += 2;
      }

      // Tunnel interior
      m_tunnelObject.mesh.vertices = vertices.ToArray();
      m_tunnelObject.mesh.triangles = tunnelTriangles.ToArray();
      m_tunnelObject.mesh.RecalculateBounds();
      m_tunnelObject.mesh.RecalculateNormals();
      m_tunnelObject.collider.sharedMesh = null;
      m_tunnelObject.collider.sharedMesh = m_tunnelObject.mesh;

      // Occluding hull
      m_occluderObject.mesh.vertices = vertices.ToArray();
      m_occluderObject.mesh.triangles = occluderTriangles;
      m_occluderObject.mesh.RecalculateBounds();
      m_occluderObject.mesh.RecalculateNormals();
    }

    public OrientedBoundingBox CreateEmbeddableOBB(GameObject topLevelObject)
    {
      // Create bounding box in the local coordinate system of the tunnel as if
      // for a BoxCollider
      Vector3 openingDimensions = OpeningDimensions();
      Vector3 size = m_tunnelDimensions + CEILING_THICKNESS * Vector3.up + openingDimensions.z * Vector3.forward;

      // Pivot point is at center of opening but we want box to be at center of
      // object
      Vector3 center = new Vector3(0, -0.5f * (m_tunnelDimensions.y + CEILING_THICKNESS), +0.5f * openingDimensions.z - 0.5f * size.z);

      // Create OBB suitable for placing object into the floor
      return CreateEmbeddableOBBFromLocalOBB(center, size, topLevelObject.transform);
    }

    public void SpawnVehicles(GameObject vehiclePrefab, string layerName, int numVehicles, float vehicleSpacingFactor)
    {
      float tunnelRearWall = -(m_tunnelDimensions.z + 0.5f * OpeningDimensions().z);
      float vehiclePitch = m_vehicleDimensions.z * (1 + vehicleSpacingFactor);
      float z = tunnelRearWall + 0.5f * vehiclePitch;
      float x = 0;
      float y = -CEILING_THICKNESS - m_tunnelDimensions.y;
      for (int i = 0; i < numVehicles; i++, z += vehiclePitch)
      {
        Vector3 localPosition = new Vector3(x, y, z);
        GameObject vehicle = Instantiate(vehiclePrefab, m_tunnelObject.obj.transform.TransformPoint(localPosition), m_tunnelObject.obj.transform.rotation);
        SetVehicleLayer(vehicle, layerName);
      }
    }

    public TunnelWithSmoothRamp(ObjectData tunnelObject, ObjectData occluderObject, Vector3 tunnelDimensions, Vector3 vehicleDimensions, float rampAngle, int numRampSegments)
    {
      m_tunnelObject = tunnelObject;
      m_occluderObject = occluderObject;
      m_tunnelDimensions = tunnelDimensions;
      m_vehicleDimensions = vehicleDimensions;
      m_rampAngle = rampAngle;
      m_numRampSegments = numRampSegments;
    }
  }

  private class AngledTunnel : TunnelBuilderInterface
  {
    private ObjectData m_tunnelObject;
    private ObjectData m_occluderObject;
    private Vector3 m_tunnelDimensions;
    private Vector3 m_vehicleDimensions;
    private float m_rampAngle;
    private Vector3[] m_vertices;
    private int[] m_tunnelTriangles;
    private int[] m_occluderTriangles;

    public Vector3 OpeningDimensions()
    {
      /*
       * Tunnel opening along the z (length) axis is larger than implied by the
       * headroom if ceiling thickness is non-zero.
       * 
       * ........................................
       *                | <-- opening --> /
       *                |                /
       *                |               /
       *               /               /
       *               <---- width --->
       *               
       */

      float baseLength = m_tunnelDimensions.y / Mathf.Sin(m_rampAngle * Mathf.Deg2Rad);
      float length = baseLength + CEILING_THICKNESS / Mathf.Tan(m_rampAngle * Mathf.Deg2Rad);
      return new Vector3(m_tunnelDimensions.x, 0, length);
    }

    private void GenerateVertices()
    {
      /*
       * Side cross-sectional view of tunnel:
       * ------------------------------------
       * 
       * .................................. <-- floor level
       * ceiling thickness -->  |      /
       *                        /<--->/ base length
       *                       /     /  tunnel "height" is defined as normal to
       *                      /     /   floor where vehicles are positioned
       *                     /     /
       *                    /     /
       *                   /     /  <-- tunnel length
       *                  /     /
       *                 /     /
       *                /     /
       *               +-----+ <-----> 
       *                         horizontal offset from surface point to
       *                         deepest point
       *
       * base length = tunnelHeight / sin(rampAngle)                
       */

      float baseLength = m_tunnelDimensions.y / Mathf.Sin(m_rampAngle * Mathf.Deg2Rad);
      float openingLength = OpeningDimensions().z;

      // All of these are in the local rotation of the tunnel itself
      float tunnelWidth = m_tunnelDimensions.x;
      float tunnelHeight = m_tunnelDimensions.y;  // tunnel floor to tunnel ceiling
      float tunnelLength = m_tunnelDimensions.z;

      // Compute depth along actual y axis and the horizontal offset to get
      float verticalOffset = tunnelLength * Mathf.Sin(m_rampAngle * Mathf.Deg2Rad);
      float horizontalOffset = tunnelLength * Mathf.Cos(m_rampAngle * Mathf.Deg2Rad);

      // Left and right here are along global x axis
      m_vertices = new Vector3[]
      {
        // Tunnel opening
        new Vector3(-0.5f * tunnelWidth, 0, -0.5f * openingLength), // 0 left of tunnel entrance / ceiling cutaway
        new Vector3(+0.5f * tunnelWidth, 0, -0.5f * openingLength), // 1 right
        new Vector3(-0.5f * tunnelWidth, 0, +0.5f * openingLength), // 2 left of tunnel entrance / ramp floor
        new Vector3(+0.5f * tunnelWidth, 0, +0.5f * openingLength), // 3 right

        // Bottom of ceiling cutaway
        new Vector3(-0.5f * tunnelWidth, -CEILING_THICKNESS, -0.5f * openingLength),  // 4 left side
        new Vector3(+0.5f * tunnelWidth, -CEILING_THICKNESS, -0.5f * openingLength),  // 5 right side

        // Bottom of tunnel
        new Vector3(-0.5f * tunnelWidth, -verticalOffset, +0.5f * openingLength - horizontalOffset - baseLength), // 6 left (tunnel ceiling)
        new Vector3(+0.5f * tunnelWidth, -verticalOffset, +0.5f * openingLength - horizontalOffset - baseLength), // 7 right (tunnel ceiling)
        new Vector3(-0.5f * tunnelWidth, -verticalOffset, +0.5f * openingLength - horizontalOffset),              // 8 left (tunnel floor)
        new Vector3(+0.5f * tunnelWidth, -verticalOffset, +0.5f * openingLength - horizontalOffset),              // 9 right (tunnel floor)

        // Points on tunnel floor that align with bottom of ceiling cutaway
        new Vector3(-0.5f * tunnelWidth, -CEILING_THICKNESS, -0.5f * openingLength + baseLength), // 10 left side
        new Vector3(+0.5f * tunnelWidth, -CEILING_THICKNESS, -0.5f * openingLength + baseLength), // 11 right side

        // Landing in front of tunnel
        new Vector3(-0.5f * tunnelWidth, 0, +0.5f * openingLength + 2*m_vehicleDimensions.z), // 12 left of tunnel entrance / ramp floor
        new Vector3(+0.5f * tunnelWidth, 0, +0.5f * openingLength + 2*m_vehicleDimensions.z)  // 13 right
      };

      m_tunnelTriangles = new int[]
      {
        // Ceiling cutaway
        1, 0, 4,
        4, 5, 1,

        // Tunnel floor
        9, 8, 2,
        2, 3, 9,

        // Tunnel ceiling
        5, 4, 6,
        6, 7, 5,

        // Bottom of tunnel
        7, 6, 8,
        8, 9, 7,

        // Left wall, ceiling cutaway segment
        0, 2, 10,
        10, 4, 0,

        // Right wall, ceiling cutway segment
        3, 1, 5,
        5, 11, 3,

        // Left wall
        4, 10, 6,
        6, 10, 8,

        // Right wall
        11, 5, 7,
        7, 9, 11,

        // Landing pad
        //TODO: placement volume needs to be adjusted to safely use this
        //TODO: this should be put
        //2, 12, 13,
        //13, 3, 2
      };

      m_occluderTriangles = new int[]
      {
        // Left
        2, 0, 6,
        6, 8, 2,

        // Right
        1, 3, 9,
        9, 7, 1,

        // Top (tunnel ceiling side)
        0, 1, 7,
        7, 6, 0,

        // Bottom (tunnel floor side)
        3, 2, 8,
        8, 9, 3,

        // Bottom (rear of tunnel)
        6, 7, 8,
        8, 7, 9
      };
    }

    public void CreateMeshes()
    {
      // Tunnel interior
      m_tunnelObject.mesh.vertices = m_vertices;
      m_tunnelObject.mesh.triangles = m_tunnelTriangles;
      m_tunnelObject.mesh.RecalculateBounds();
      m_tunnelObject.mesh.RecalculateNormals();
      m_tunnelObject.collider.sharedMesh = null;
      m_tunnelObject.collider.sharedMesh = m_tunnelObject.mesh;

      // Occluding hull
      m_occluderObject.mesh.vertices = m_vertices;
      m_occluderObject.mesh.triangles = m_occluderTriangles;
      m_occluderObject.mesh.RecalculateBounds();
      m_occluderObject.mesh.RecalculateNormals();
    }

    public OrientedBoundingBox CreateEmbeddableOBB(GameObject topLevelObject)
    {
      // Create bounding box in the local coordinate system of the tunnel as if
      // for a BoxCollider
      Vector3 openingDimensions = OpeningDimensions();
      Vector3 size = m_vertices[3] - m_vertices[6];

      // Pivot point is at center of opening but box is defined at center of
      // object
      Vector3 center = 0.5f * (m_vertices[3] + m_vertices[6]);

      // Create OBB suitable for embedding object into the floor
      return CreateEmbeddableOBBFromLocalOBB(center, size, topLevelObject.transform);
    }

    public void SpawnVehicles(GameObject vehiclePrefab, string layerName, int numVehicles, float vehicleSpacingFactor)
    {
      // Orientation is perpendicular to ramp
      Quaternion rampRotation = Quaternion.Euler(new Vector3(-m_rampAngle, 0, 0));

      // Starting point is at the rear, centerpoint of tunnel floor
      Vector3 startingPoint = m_vertices[8];
      startingPoint.x = 0;

      float vehiclePitch = m_vehicleDimensions.z * (1 + vehicleSpacingFactor);
      float distanceUpRamp = 0.5f * vehiclePitch;
      for (int i = 0; i < numVehicles; i++, distanceUpRamp += vehiclePitch)
      {
        float y = distanceUpRamp * Mathf.Sin(m_rampAngle * Mathf.Deg2Rad);
        float z = distanceUpRamp * Mathf.Cos(m_rampAngle * Mathf.Deg2Rad);
        Vector3 localPosition = startingPoint + y * Vector3.up + z * Vector3.forward;
        GameObject vehicle = Instantiate(vehiclePrefab, m_tunnelObject.obj.transform.TransformPoint(localPosition), m_tunnelObject.obj.transform.rotation * rampRotation);
        SetVehicleLayer(vehicle, layerName);
      }
    }

    public AngledTunnel(ObjectData tunnelObject, ObjectData occluderObject, Vector3 tunnelDimensions, Vector3 vehicleDimensions, float rampAngle)
    {
      m_tunnelObject = tunnelObject;
      m_occluderObject = occluderObject;
      m_tunnelDimensions = tunnelDimensions;
      m_vehicleDimensions = vehicleDimensions;
      m_rampAngle = rampAngle;
      GenerateVertices();
    }
  }

  private TunnelBuilderInterface m_tunnelBuilder = null;

  private static OrientedBoundingBox CreateEmbeddableOBBFromLocalOBB(Vector3 localCenter, Vector3 localSize, Transform transform)
  {
    // Objects embeddable with SurfacePlanDeformationManager must provide an
    // OBB with the z axis normal to the surface into which the object will be
    // embedded. The tunnel's y axis is normal to the floor, so we create an
    // OBB with a z axis corresponding to the tunnel's y axis.
    Quaternion yToZ = Quaternion.FromToRotation(Vector3.forward, Vector3.up);
    return new OrientedBoundingBox()
    {
      Center = transform.TransformPoint(localCenter),
      Rotation = transform.rotation * yToZ,
      Extents = 0.5f * new Vector3(transform.lossyScale.x * localSize.x, transform.lossyScale.z * localSize.z, transform.lossyScale.y * localSize.y)
    };
  }

  private static void SetVehicleLayer(GameObject vehicle, string layerName)
  {
    int layer = LayerMask.NameToLayer(layerName);
    vehicle.layer = layer;
    foreach (Transform transform in vehicle.GetComponentsInChildren<Transform>())
    {
      transform.gameObject.layer = layer;
    }
  }

  private ObjectData CreateNewObject(string name, Material material, bool createCollider)
  {
    GameObject obj = new GameObject(name);
    obj.layer = LayerMask.NameToLayer(layerName);
    obj.transform.parent = gameObject.transform;
    obj.transform.localPosition = Vector3.zero;
    obj.transform.localRotation = Quaternion.identity;
    obj.transform.localScale = Vector3.one;
    MeshCollider collider = createCollider ? obj.AddComponent<MeshCollider>() : null;
    Mesh mesh = obj.AddComponent<MeshFilter>().mesh;
    MeshRenderer meshRenderer = obj.AddComponent<MeshRenderer>();
    meshRenderer.material = material;
    meshRenderer.enabled = true;
    return new ObjectData(obj, collider, mesh);
  }

  private void OnEmbedComplete()
  {
    m_tunnelObject.obj.SetActive(true);
    m_occluderObject.obj.SetActive(true);
    m_tunnelBuilder.SpawnVehicles(vehiclePrefab, layerName, numVehiclesToSpawn, tunnelVehicleSpacing);
  }

  public void Embed(Vector3 position, Quaternion rotation)
  {
    transform.position = position;
    transform.rotation = rotation;

    // Even with spatial understanding, floor is not always even, so we need to
    // extend the OBB used for mesh deformation above the level of the floor
    Vector3 placementBox = GetPlacementDimensions();
    OrientedBoundingBox obb = m_tunnelBuilder.CreateEmbeddableOBB(gameObject);
    obb.Extents += Vector3.forward * placementBox.y;  // remember that OBB z axis corresponds to tunnel's y axis

    SurfacePlaneDeformationManager.Instance.Embed(gameObject, obb, transform.position, () => { OnEmbedComplete(); }, false);
  }

  public Vector3 GetPlacementDimensions()
  {
    Vector3 placementBox = m_tunnelBuilder.OpeningDimensions();
    placementBox.y = 2 * m_vehicleDimensions.y;
    return placementBox;
  }

  public void Init()
  {
    gameObject.layer = LayerMask.NameToLayer(layerName);

    // Tunnel dimensions exclude ceiling thickness and any additional ramp
    // structure
    m_vehicleDimensions = Footprint.Measure(vehiclePrefab);
    Vector3 tunnelDimensions = new Vector3(m_vehicleDimensions.x * (1 + tunnelWiggleroom), m_vehicleDimensions.y * (1 + tunnelHeadroom), maxVehiclesInTunnel * m_vehicleDimensions.z * (1 + tunnelVehicleSpacing));

    m_tunnelObject = CreateNewObject("TunnelInterior", tunnelMaterial, true);
    m_occluderObject = CreateNewObject("TunnelOccluder", occlusionMaterial, false);

    switch (type)
    {
      case Type.StraightRamp:
        m_tunnelBuilder = new TunnelWithStraightRamp(m_tunnelObject, m_occluderObject, tunnelDimensions, m_vehicleDimensions, rampAngle);
        break;
      case Type.SmoothRamp:
        m_tunnelBuilder = new TunnelWithSmoothRamp(m_tunnelObject, m_occluderObject, tunnelDimensions, m_vehicleDimensions, rampAngle, numRampSegments);
        break;
      case Type.RampOnly:
        m_tunnelBuilder = new AngledTunnel(m_tunnelObject, m_occluderObject, tunnelDimensions, m_vehicleDimensions, rampAngle);
        break;
    }

    m_tunnelBuilder.CreateMeshes();

    // Disable sub-objects. They will be re-enabled upon being embedded.
    m_tunnelObject.obj.SetActive(false);
    m_occluderObject.obj.SetActive(false);
  }

  private void Awake()
  {
    if (debugMode)
    {
      Init();
      OnEmbedComplete();
    }
  }
}