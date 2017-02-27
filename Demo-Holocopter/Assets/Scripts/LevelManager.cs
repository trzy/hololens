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
  [Tooltip("Flat-shaded material")]
  public Material flatMaterial = null;

  [Tooltip("Factory complex prefab")]
  public GameObject factoryComplexPrefab = null;

  [Tooltip("Tank enemy prefab")]
  public GameObject tankPrefab = null;

  public IMissionHandler currentMission { get { return this.m_currentMission;} }

  private PlayspaceManager m_playspaceManager = null;
  private IMissionHandler m_currentMission = null;

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

  public void GenerateLevel()
  {
    List<GameObject> tables = m_playspaceManager.GetPlatforms(PlayspaceManager.SortOrder.Descending);
    List<GameObject> floors = m_playspaceManager.GetFloors(PlayspaceManager.SortOrder.Descending);
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
    m_playspaceManager.SpawnObject(tankPrefab, largestTable, 0, 0.2f);
    m_playspaceManager.SpawnObject(tankPrefab, largestTable, -0.25f, -0.2f);
    for (int i = 1; i < tables.Count; i++)
    {
      SurfacePlane table = tables[i].GetComponent<SurfacePlane>();
      m_playspaceManager.SpawnObject(tankPrefab, table, 0, 0);
    }

    // Place some tanks on the floor!
    foreach (GameObject floorObj in floors)
    {
      SurfacePlane floor = floorObj.GetComponent<SurfacePlane>();
      List<Vector2> spawnPoints = m_playspaceManager.FindFloorSpawnPoints(new Vector3(0.25f, 0.5f, 0.25f), new Vector2(0.25f, 0.25f), 0.5f, floor);
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
        m_playspaceManager.SpawnObject(tankPrefab, floor, spawnPoints[idx].x, spawnPoints[idx].y);
      }
    }
  }

  void Start()
  {
    m_playspaceManager = PlayspaceManager.Instance;
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
