using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TestSharedMaterialHelper: MonoBehaviour
{
  [Tooltip("Objects whose materials we will change then restore.")]
  public GameObject[] things;

  private bool m_prompt = true;
  private int m_state = 0;

  private static int s_initial_shared_materials = 0;

  private void PrintSharedMaterials()
  {
    foreach (GameObject obj in things)
    {
      foreach (Renderer renderer in obj.GetComponentsInChildren<Renderer>())
      {
        foreach (Material material in renderer.sharedMaterials)
        {
          Debug.Log(obj.name + " shared: " + material.GetInstanceID());
        }
      }
    }
  }

  private void PrintMaterials()
  {
    foreach (GameObject obj in things)
    {
      foreach (Renderer renderer in obj.GetComponentsInChildren<Renderer>())
      {
        foreach (Material material in renderer.materials)
        {
          Debug.Log(obj.name + " instanced: " + material.GetInstanceID());
        }
      }
    }
  }

  // Unity apparently does not track shared materials so initially, 
  // GetNumberOfMaterials() returns 0
  private int GetInitialNumberOfSharedMaterials()
  {
    HashSet<int> unique_materials = new HashSet<int>();
    foreach (GameObject obj in things)
    {
      foreach (Renderer renderer in obj.GetComponentsInChildren<Renderer>())
      {
        foreach (Material material in renderer.sharedMaterials)
        {
          unique_materials.Add(material.GetInstanceID());
        }
      }
    }
    return unique_materials.Count;
  }

  // This will count instantiated materials 
  private int GetNumberOfMaterials()
  {
    return GameObject.FindObjectsOfType(typeof(Material)).Length;
  }

  private int ChangeMaterialProperties()
  {
    int num_materials_changed = 0;
    foreach (GameObject obj in things)
    {
      foreach (Renderer renderer in obj.GetComponentsInChildren<Renderer>())
      {
        Material[] materials = renderer.materials;
        foreach (Material material in materials)
        {
          material.color = Color.green;
          num_materials_changed++;
        }
      }
    }
    return num_materials_changed;
  }

  void Start ()
  {
	}
	
	void Update ()
  {
    if (m_prompt)
    {
      Debug.Log("Press ENTER to continue...");
      m_prompt = false;
    }
    if (!Input.GetKeyDown(KeyCode.Return))
      return;      
    switch (m_state)
    {
      case 0:
        {
          s_initial_shared_materials = GetInitialNumberOfSharedMaterials();
          Debug.Log("Number of materials initially: " + s_initial_shared_materials + " (shared), " + GetNumberOfMaterials() + " (instanced)");
          int expected_num = ChangeMaterialProperties();
          Debug.Log("Number of materials after modification of properties: " + GetNumberOfMaterials() + " (expected " + expected_num + ")");
          break;
        }
      case 1:
        {
          Debug.Log("Applying clones...");
          foreach (GameObject thing in things)
          {
            thing.GetComponent<SharedMaterialHelper>().ApplySharedMaterialClones();
          }
          Debug.Log("Number of materials *immediately* after clone application: " + GetNumberOfMaterials());
          break;
        }
      case 2:
        {
          Debug.Log("Number of instanced materials at next Update(): " + GetNumberOfMaterials() + " (expected " + s_initial_shared_materials + ")");
          break;
        }
      case 3:
        {
          Debug.Log("Restoring shared materials...");
          foreach (GameObject thing in things)
          {
            thing.GetComponent<SharedMaterialHelper>().RestoreSharedMaterials();
          }
          Debug.Log("Number of materials *immediately* after shared material restoration: " + GetNumberOfMaterials());
          break;
        }
      case 4:
        {
          Debug.Log("Number of instanced materials at next Update(): " + GetNumberOfMaterials());
          break;
        }
      case 5:
        {
          Debug.Log("Destroying objects...");
          foreach (GameObject thing in things)
          {
            Object.Destroy(thing);
          }
          break;
        }
      case 6:
        {
          Debug.Log("Number of instanced materials at next Update(): " + GetNumberOfMaterials() + " (expected 0)");
          break;
        }
      default:
        break;
    }
    ++m_state;
    m_prompt = true;
  }
}
