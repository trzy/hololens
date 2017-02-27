using UnityEngine;
using HoloToolkit.Unity.SpatialMapping;
using System.Collections;
using System.Collections.Generic;

public interface IMissionHandler
{
  void OnEnemyHitByPlayer(MonoBehaviour enemy);
  void Update();
}

class Mission1: IMissionHandler
{
  private int m_numHitsUntilArmorHint = 2;
  public void OnEnemyHitByPlayer(MonoBehaviour enemy)
  {
    if (m_numHitsUntilArmorHint > 0)
    {
      if (enemy is Tank)
      {
        if (0 == --m_numHitsUntilArmorHint)
          VoiceManager.Instance.Play(VoiceManager.Voice.ArmorHint);
      }
    }
  }
  public void Update()
  {
  }
}

public class LevelManager : HoloToolkit.Unity.Singleton<LevelManager>
{
  [Tooltip("Flat-shaded material.")]
  public Material flatMaterial = null;

  [Tooltip("Factory complex prefab.")]
  public GameObject factoryComplexPrefab = null;

  [Tooltip("Tank enemy prefab.")]
  public GameObject tankPrefab = null;

  public IMissionHandler currentMission { get { return this.m_currentMission;} }

  private IMissionHandler m_currentMission = null;

  //TODO: refactor. too much duplication
  private List<GameObject> GetFloorsInDescendingAreaOrder()
  {
    List<GameObject> floors = PlayspaceManager.Instance.GetFloors();
    floors.Sort((plane1, plane2) =>
    {
      BoundedPlane bp1 = plane1.GetComponent<SurfacePlane>().Plane;
      BoundedPlane bp2 = plane2.GetComponent<SurfacePlane>().Plane;
      // Sort descending
      return bp2.Area.CompareTo(bp1.Area);
    });
    return floors;
  }

  private List<GameObject> GetPlatformsInDescendingAreaOrder()
  {
    List<GameObject> floors = PlayspaceManager.Instance.GetPlatforms();
    floors.Sort((plane1, plane2) =>
    {
      BoundedPlane bp1 = plane1.GetComponent<SurfacePlane>().Plane;
      BoundedPlane bp2 = plane2.GetComponent<SurfacePlane>().Plane;
      // Sort descending
      return bp2.Area.CompareTo(bp1.Area);
    });
    return floors;
  }

  private void PlaceCube(float width, float height, float depth, Material material, Color color, SurfacePlane plane, float localX, float localZ)
  {
    //
    // Plane coordinate system is different from Unity world convention:
    //  x,y (right,up) -> plane surface
    //  z (forward)    -> perpendicular to plane surface
    // To further add to confusion, the forward direction is not necessarily
    // the same as the normal, and can be its inverse. We must test for this
    // situation and rotate 180 degrees about (local) x, y.
    //

    // World xz -> plane xy
    float x = localX;
    float y = localZ;
    float sizeX = width;
    float sizeY = depth;
    float sizeZ = height;

    // Construct rotation from plane to world space
    Vector3 origin = plane.transform.position;
    Quaternion rotation = plane.transform.rotation;
    if (plane.transform.forward.y < 0)  // plane is oriented upside down 
      rotation = Quaternion.LookRotation(-plane.transform.forward, plane.transform.up);

    // Place object in plane-local coordinate system; rotate to world system
    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
    cube.transform.parent = gameObject.transform; // level manager will be parent
    cube.transform.localScale = new Vector3(sizeX, sizeY, sizeZ);
    cube.transform.position = origin + rotation * new Vector3(x, y, 0.5f * sizeZ);
    cube.transform.transform.rotation = rotation;
    cube.GetComponent<Renderer>().material = material;
    cube.GetComponent<Renderer>().material.color = color; // equivalent to SetColor("_Color", color)
    cube.SetActive(true);
  }

  private void SpawnObject(GameObject prefab, SurfacePlane plane, float localX, float localZ)
  {
    // Rotation from a coordinate system where y is up into one where z is up.
    // This is used to orient objects in a plane-local system before applying
    // the plane's own transform. Without this, objects end up aligned along
    // the world's xz axes, not the plane's local xy axes.
    Quaternion toPlaneOrientation = Quaternion.FromToRotation(Vector3.up, Vector3.forward);
    // World xz -> Plane xy
    float x = localX;
    float y = localZ;
    Vector3 origin = plane.transform.position;
    Quaternion rotation = plane.transform.rotation;
    if (plane.transform.forward.y < 0)  // plane is oriented upside down
      rotation = rotation * Quaternion.FromToRotation(-Vector3.forward, Vector3.forward);
    // Spawn object in plane-local coordinate system (rotation brings it into world system)
    GameObject obj = Instantiate(prefab) as GameObject;
    obj.transform.parent = gameObject.transform;
    obj.transform.rotation = rotation * toPlaneOrientation;
    obj.transform.position = origin + rotation * new Vector3(x, y, 0);
    obj.SetActive(true);
  }

  private List<Vector2> FindFloorSpawnPoints(Vector3 requiredSize, Vector2 stepSize, float clearance, SurfacePlane plane)
  {
    List<Vector2> places = new List<Vector2>();
    OrientedBoundingBox bb = plane.Plane.Bounds;
    Quaternion orientation = (plane.transform.forward.y < 0) ? (plane.transform.rotation * Quaternion.FromToRotation(-Vector3.forward, Vector3.forward)) : plane.transform.rotation;
    // Note that xy in local plane coordinate system correspond to what would be xz in global space
    Vector3 halfExtents = new Vector3(requiredSize.x, requiredSize.z, requiredSize.y) * 0.5f;
    // We step over the plane in step size increments but check for the
    // required size, which ought to be larger (thereby creating overlapping points)
    for (float y = -bb.Extents.y + halfExtents.y; y <= bb.Extents.y - halfExtents.y; y += stepSize.y)
    {
      for (float x = -bb.Extents.x + halfExtents.x; x <= bb.Extents.x - halfExtents.x; x += stepSize.x)
      {
        Vector3 center = plane.transform.position + orientation * new Vector3(x, y, halfExtents.z + clearance);
        Collider[] colliders = Physics.OverlapBox(center, halfExtents, orientation, Layers.Instance.spatialMeshLayerMask);
        if (colliders.Length == 0)
        {
          /*
          GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
          cube.transform.parent = gameObject.transform; // level manager will be parent
          cube.transform.localScale = 2 * halfExtents;
          cube.transform.position = center;
          cube.transform.transform.rotation = orientation;
          cube.GetComponent<Renderer>().material = flatMaterial;
          cube.GetComponent<Renderer>().material.color = Color.green;
          cube.SetActive(true);
          */
          places.Add(new Vector2(x, y));
        }
      }
    }
    return places;
  }

  private void DrawFloorSpawnPoints(Vector3 requiredSize, float clearance, SurfacePlane plane)
  {
    OrientedBoundingBox bb = plane.Plane.Bounds;
    Quaternion orientation = (plane.transform.forward.y < 0) ? (plane.transform.rotation * Quaternion.FromToRotation(-Vector3.forward, Vector3.forward)) : plane.transform.rotation;
    // Note that xy in local plane coordinate system correspond to what would be xz in global space
    Vector3 halfExtents = new Vector3(requiredSize.x, requiredSize.z, requiredSize.y) * 0.5f;
    for (float y = -bb.Extents.y + halfExtents.y; y <= bb.Extents.y - halfExtents.y; y += 2 * halfExtents.y)
    {
      for (float x = -bb.Extents.x + halfExtents.x; x <= bb.Extents.x - halfExtents.x; x += 2 * halfExtents.x)
      {
        Vector3 center = plane.transform.position + orientation * new Vector3(x, y, halfExtents.z + clearance);
        Collider[] colliders = Physics.OverlapBox(center, halfExtents, orientation, Layers.Instance.spatialMeshLayerMask);
        if (colliders.Length == 0)
        {
          GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
          cube.transform.parent = gameObject.transform; // level manager will be parent
          cube.transform.localScale = 2 * halfExtents;
          cube.transform.position = center;
          cube.transform.transform.rotation = orientation;
          cube.GetComponent<Renderer>().material = flatMaterial;
          cube.GetComponent<Renderer>().material.color = Color.green;
          cube.SetActive(true);
        }
      }
    }
  }

  private Vector2 FindNearestToPlayer(List<Vector2> places)
  {
    Vector2 playerPosition = new Vector2(Camera.main.transform.position.x, Camera.main.transform.position.z);
    Vector2 bestCandidate = Vector2.zero;
    float best_distance = float.PositiveInfinity;
    foreach (Vector2 place in places)
    {
      float distance = Vector2.SqrMagnitude(playerPosition - place);
      if (distance < best_distance)
      {
        best_distance = distance;
        bestCandidate = place;
      }
    }
    return bestCandidate;
  }

  public void GenerateLevel()
  {
    List<GameObject> tables = GetPlatformsInDescendingAreaOrder();
    List<GameObject> floors = GetFloorsInDescendingAreaOrder();
    Debug.Log("GenerateLevel(): number of floors=" + floors.Count + ", number of tables=" + tables.Count);

    //TODO: check if empty and take corrective action
    if (tables.Count == 0 || floors.Count == 0)
    {
      // For now, place a big ugly gray cube if no tables found
      /*
      GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
      cube.transform.parent = gameObject.transform; // level manager will be parent
      cube.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
      cube.transform.position = new Vector3(0, 0, 2);
      cube.GetComponent<Renderer>().material = flatMaterial;
      cube.GetComponent<Renderer>().material.color = Color.grey;
      cube.SetActive(true);
      */
      return;
    }

    // Place blocks representing a building on the largest table surface (for
    // now, until we have better assets, just place cubes :))
    /*
    SurfacePlane cityPlane = floors[0].GetComponent<SurfacePlane>();
    PlaceCube(0.25f, 0.75f, 0.25f, flatMaterial, Color.red, cityPlane, -0.25f + 0.0f, 0.1f);
    PlaceCube(0.25f, 0.30f, 0.25f, flatMaterial, Color.green, cityPlane, -0.50f - 0.1f, 0.1f);
    PlaceCube(0.25f, 0.40f, 0.25f, flatMaterial, Color.blue, cityPlane, 0.0f + 0.1f, 0.25f + 0.1f);
    */

    // Place two tanks on largest table and one more tank on each table
    SurfacePlane largestTable = tables[0].GetComponent<SurfacePlane>();
    SpawnObject(tankPrefab, largestTable, 0, 0.2f);
    SpawnObject(tankPrefab, largestTable, -0.25f, -0.2f);
    for (int i = 1; i < tables.Count; i++)
    {
      SurfacePlane table = tables[i].GetComponent<SurfacePlane>();
      SpawnObject(tankPrefab, table, 0, 0);
    }

    // Place some tanks on the floor!
    foreach (GameObject floorObj in floors)
    {
      SurfacePlane floor = floorObj.GetComponent<SurfacePlane>();
      List<Vector2> spawnPoints = FindFloorSpawnPoints(new Vector3(0.25f, 0.5f, 0.25f), new Vector2(0.25f, 0.25f), 0.5f, floor);
      int numSpawnPoints = spawnPoints.Count;
      List<int> indexes = new List<int>(numSpawnPoints);
      for (int i = 0; i < numSpawnPoints; i++)
      {
        indexes.Add(i);
      }
      int numTanks = System.Math.Min(3, numSpawnPoints);
      for (int i = 0; i < numTanks; i++)
      {
        int rand = Random.Range(0, indexes.Count);
        int idx = indexes[rand];
        indexes.Remove(idx);
        SpawnObject(tankPrefab, floor, spawnPoints[idx].x, spawnPoints[idx].y);
      }
    }
  }

  void Start()
  {
    m_currentMission = new Mission1();
    //
    // Single-color, flat-shaded material. HoloToolkit/Vertex Lit Configurable
    // appears optimized to compile down to only the variant defined by
    // keywords. I'm not sure how Unity translates e.g. the editor color
    // "Enabled" tick box to _USECOLOR_ON but apparently it somehow does. Unity
    // manual seems to imply that "#pragma shader_feature X" will not include 
    // unused variants of the shader in a build. If this code stops working,
    // may need to use a dummy asset with these material settings somewhere?
    //
    //flatMaterial = new Material(Shader.Find("HoloToolkit/Vertex Lit Configurable"));
    //flatMaterial.EnableKeyword("_USECOLOR_ON");
    //flatMaterial.DisableKeyword("_USEMAINTEX_ON");
    //flatMaterial.DisableKeyword("_USEEMISSIONTEX_ON");
  }
	
	void Update()
  {
    if (m_currentMission != null)
      m_currentMission.Update();
  }
}
