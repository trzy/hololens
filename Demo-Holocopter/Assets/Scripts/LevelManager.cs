using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class LevelManager : MonoBehaviour
{
  [Tooltip("Used to obtain surface meshes.")]
  public PlayspaceManager m_playspace_manager;

  [Tooltip("Flat-shaded material.")]
  public Material m_flat_material = null;

  //
  // SurfacePlane local coordinate system is perpendicular to Z axis (surface
  // face is in XY plane with up being -Z). We want our local coordinate 
  // system for object placement to lie in XZ plane (i.e., the floor of the
  // plane with up being +Y in world space). To rotate our local system onto
  // the surface plane in world space, we first rotate into the SurfacePlane
  // local system and then apply SurfacePlane's transformation.
  //
  // Unfortunately, after plane transform, it's unclear where +X and +Z are
  // because of rotation about the Y axis, so we cannot be quite sure using
  // the simple scheme here how objects will be oriented, except that they
  // will be in a local coordinate system lined up with plane edges.
  //
  private Quaternion m_rotate_to_plane_basis = Quaternion.FromToRotation(new Vector3(0, 1, 0), new Vector3(0, 0, -1));

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
    Vector3 origin = plane.transform.position;
    Quaternion rotation = plane.transform.rotation * m_rotate_to_plane_basis; // 1) rotate into plane-local basis, then 2) rotate into world orientation
    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
    cube.transform.parent = gameObject.transform; // level manager will be parent
    cube.transform.localScale = new Vector3(width, height, depth);
    cube.transform.position = origin + rotation * new Vector3(local_x, 0.5f * height, local_z);
    cube.transform.transform.rotation = rotation;
    cube.GetComponent<Renderer>().material = material;
    cube.GetComponent<Renderer>().material.color = color; // equivalent to SetColor("_Color", color)
    cube.SetActive(true);
  }

  public void GenerateLevel()
  {
    List<GameObject> floors = GetTablesInDescendingAreaOrder();
    Debug.Log("GenerateLevel(): number of floors=" + floors.Capacity.ToString());
    //TODO: check if empty and take corrective action
    if (floors.Capacity == 0)
      return;
    // Until we develop some real assets, just place cubes :)
    HoloToolkit.Unity.SurfacePlane city_plane = floors[0].GetComponent<HoloToolkit.Unity.SurfacePlane>();
    PlaceCube(0.25f, 0.75f, 0.25f, m_flat_material, Color.red, city_plane, -0.25f + 0.0f, 0.1f);
    PlaceCube(0.25f, 0.30f, 0.25f, m_flat_material, Color.green, city_plane, -0.50f - 0.1f, 0.1f);
    PlaceCube(0.25f, 0.40f, 0.25f, m_flat_material, Color.blue, city_plane, 0.0f + 0.1f, 0.25f + 0.1f);
  }

  void Start()
  {
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
  }
}
