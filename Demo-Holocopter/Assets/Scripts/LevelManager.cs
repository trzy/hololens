using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class LevelManager : MonoBehaviour
{
  [Tooltip("Used to obtain surface meshes.")]
  public PlayspaceManager m_playspace_manager;

  private List<GameObject> GetFloorsInDescendingAreaOrder()
  {
    List<GameObject> floors = m_playspace_manager.GetFloors();
    floors.Sort((plane1, plane2) =>
    {
      HoloToolkit.Unity.BoundedPlane bp1 = plane1.GetComponent<HoloToolkit.Unity.SurfacePlane>().Plane;
      HoloToolkit.Unity.BoundedPlane bp2 = plane2.GetComponent<HoloToolkit.Unity.SurfacePlane>().Plane;
      // Sort descending
      return bp2.Area.CompareTo(bp1.Area);
    });
    return floors;
  }

  public void GenerateLevel()
  {
    List<GameObject> floors = GetFloorsInDescendingAreaOrder();
    Debug.Log("GenerateLevel(): number of floors=" + floors.Capacity.ToString());
    //TODO: check if empty and take corrective action
    if (floors.Capacity == 0)
      return;
    HoloToolkit.Unity.SurfacePlane city_plane = floors[0].GetComponent<HoloToolkit.Unity.SurfacePlane>();
    Vector3 center = city_plane.Plane.Bounds.Center;
    // Generate a test object (cube) and set it atop the plane surface
    GameObject test = GameObject.CreatePrimitive(PrimitiveType.Cube);
    test.transform.parent = gameObject.transform; // level manager will be parent
    test.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
    test.transform.position = center + new Vector3(0, 0.5f * test.transform.localScale.z, 0);
    test.transform.rotation = gameObject.transform.rotation;
    //test.GetComponent<Renderer>().sharedMaterial = Resources.Load("Models/Reticle/Materials/Reticle.mat") as Material;
    test.SetActive(true);
  }

  void Start()
  {
  }
	
	void Update()
  {
  }
}
