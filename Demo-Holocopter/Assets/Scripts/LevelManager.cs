using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public interface IMissionHandler
{
  void OnEnemyHitByPlayer(MonoBehaviour enemy);
  void Update();
}

class Mission1: IMissionHandler
{
  private int m_num_hits_until_armor_hint = 2;
  public void OnEnemyHitByPlayer(MonoBehaviour enemy)
  {
    if (m_num_hits_until_armor_hint > 0)
    {
      if (enemy is Tank)
      {
        if (0 == --m_num_hits_until_armor_hint)
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
  [Tooltip("Used to obtain surface meshes.")]
  public PlayspaceManager m_playspace_manager;

  [Tooltip("Flat-shaded material.")]
  public Material m_flat_material = null;

  [Tooltip("Tank enemy prefab.")]
  public GameObject m_tank_prefab = null;

  public IMissionHandler currentMission { get { return this.m_current_mission;} }

  private IMissionHandler m_current_mission = null;

  private List<GameObject> GetTablesInDescendingAreaOrder()
  {
    List<GameObject> floors = m_playspace_manager.GetTables();
    floors.Sort((plane1, plane2) =>
    {
      HoloToolkit.Unity.BoundedPlane bp1 = plane1.GetComponent<HoloToolkit.Unity.SurfacePlane>().Plane;
      HoloToolkit.Unity.BoundedPlane bp2 = plane2.GetComponent<HoloToolkit.Unity.SurfacePlane>().Plane;
      // Sort descending
      return bp2.Area.CompareTo(bp1.Area);
    });
    return floors;
  }

  private void PlaceCube(float width, float height, float depth, Material material, Color color, HoloToolkit.Unity.SurfacePlane plane, float local_x, float local_z)
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
    float x = local_x;
    float y = local_z;
    float size_x = width;
    float size_y = depth;
    float size_z = height;

    // Construct rotation from plane to world space
    Vector3 origin = plane.transform.position;
    Quaternion rotation = plane.transform.rotation;
    if (plane.transform.forward.y < 0)  // plane is oriented upside down 
      rotation = Quaternion.LookRotation(-plane.transform.forward, plane.transform.up);

    // Place object in plane-local coordinate system; rotate to world system
    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
    cube.transform.parent = gameObject.transform; // level manager will be parent
    cube.transform.localScale = new Vector3(size_x, size_y, size_z);
    cube.transform.position = origin + rotation * new Vector3(x, y, 0.5f * size_z);
    cube.transform.transform.rotation = rotation;
    cube.GetComponent<Renderer>().material = material;
    cube.GetComponent<Renderer>().material.color = color; // equivalent to SetColor("_Color", color)
    cube.SetActive(true);
  }

  private void SpawnObject(GameObject prefab, HoloToolkit.Unity.SurfacePlane plane, float local_x, float local_z)
  {
    // World xz -> Plane xy
    float x = local_x;
    float y = local_z;
    Vector3 origin = plane.transform.position;
    Quaternion rotation = plane.transform.rotation;
    if (plane.transform.forward.y < 0)  // plane is oriented upside down
      rotation = Quaternion.LookRotation(-plane.transform.forward, plane.transform.up);
    // Spawn object in plane-local coordinate system (rotation brings it into world system)
    GameObject obj = Instantiate(prefab) as GameObject;
    obj.transform.parent = gameObject.transform;
    obj.transform.position = origin + rotation * new Vector3(x, y, -0.06f);
    obj.SetActive(true);
  }

  public void GenerateLevel()
  {
    List<GameObject> tables = GetTablesInDescendingAreaOrder();
    Debug.Log("GenerateLevel(): number of tables=" + tables.Capacity.ToString());
    //TODO: check if empty and take corrective action
    if (tables.Capacity == 0)
    {
      // For now, place a big ugly gray cube if no tables found
      /*
      GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
      cube.transform.parent = gameObject.transform; // level manager will be parent
      cube.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
      cube.transform.position = new Vector3(0, 0, 2);
      cube.GetComponent<Renderer>().material = m_flat_material;
      cube.GetComponent<Renderer>().material.color = Color.grey;
      cube.SetActive(true);
      */
      return;
    }
    // Until we develop some real assets, just place cubes :)
    HoloToolkit.Unity.SurfacePlane city_plane = tables[0].GetComponent<HoloToolkit.Unity.SurfacePlane>();
    Debug.Log("up=" + city_plane.transform.up.ToString("F2"));
    Debug.Log("forward=" + city_plane.transform.forward.ToString("F2"));
    Debug.Log("right=" + city_plane.transform.right.ToString("F2"));
    Debug.Log("normal=" + city_plane.Plane.Plane.normal.ToString("F2"));
    PlaceCube(0.25f, 0.75f, 0.25f, m_flat_material, Color.red, city_plane, -0.25f + 0.0f, 0.1f);
    PlaceCube(0.25f, 0.30f, 0.25f, m_flat_material, Color.green, city_plane, -0.50f - 0.1f, 0.1f);
    PlaceCube(0.25f, 0.40f, 0.25f, m_flat_material, Color.blue, city_plane, 0.0f + 0.1f, 0.25f + 0.1f);

    // Place tank
    if (tables.Count >= 2)
    {
      HoloToolkit.Unity.SurfacePlane plane = tables[1].GetComponent<HoloToolkit.Unity.SurfacePlane>();
      SpawnObject(m_tank_prefab, plane, 0, 0.1f);
    }
  }

  void Start()
  {
    m_current_mission = new Mission1();
    //
    // Single-color, flat-shaded material. HoloToolkit/Vertex Lit Configurable
    // appears optimized to compile down to only the variant defined by
    // keywords. I'm not sure how Unity translates e.g. the editor color
    // "Enabled" tick box to _USECOLOR_ON but apparently it somehow does. Unity
    // manual seems to imply that "#pragma shader_feature X" will not include 
    // unused variants of the shader in a build. If this code stops working,
    // may need to use a dummy asset with these material settings somewhere?
    //
    //m_flat_material = new Material(Shader.Find("HoloToolkit/Vertex Lit Configurable"));
    //m_flat_material.EnableKeyword("_USECOLOR_ON");
    //m_flat_material.DisableKeyword("_USEMAINTEX_ON");
    //m_flat_material.DisableKeyword("_USEEMISSIONTEX_ON");
  }
	
	void Update()
  {
    if (m_current_mission != null)
      m_current_mission.Update();
  }
}
