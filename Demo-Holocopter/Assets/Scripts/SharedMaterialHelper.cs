using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SharedMaterialHelper: MonoBehaviour
{
  [Tooltip("Automatically initialize by calling SaveSharedMaterials() in Awake()")]
  public bool initializeOnAwake = true;

  private class Clone
  {
    public Material sharedMaterial;     // original
    public Material cloneMaterial;      // clone
    public int numObjectsReferencedBy;  // number of objects referencing this material

    public Clone(Material sharedMat)
    {
      sharedMaterial = sharedMat;
      cloneMaterial = new Material(sharedMaterial);
      numObjectsReferencedBy = 0;
    }
  }

  private static Dictionary<Material, Clone> s_clonesBySharedMaterial = null;
  private static Dictionary<Renderer, Material[]> s_sharedMaterialsByRenderer = null;
  private List<Clone> m_ourClones = null;

  private bool SetInsert<T>(List<T> list, T item)
  {
    if (!list.Contains(item))
    {
      list.Add(item);
      return true;  // we inserted a new item
    }
    return false;   // item was already there
  }

  private void LazyInitStatics()
  {
    if (null == s_clonesBySharedMaterial)
    {
      s_clonesBySharedMaterial = new Dictionary<Material, Clone>();
    }
    if (null == s_sharedMaterialsByRenderer)
    {
      s_sharedMaterialsByRenderer = new Dictionary<Renderer, Material[]>();
    }
  }

  private void LazyInitAll()
  {
    LazyInitStatics();
    if (null == m_ourClones)
    {
      int maxClones = 0;
      foreach (Renderer renderer in gameObject.GetComponentsInChildren<Renderer>())
      {
        maxClones += renderer.sharedMaterials.Length;
      }
      m_ourClones = new List<Clone>(maxClones);
      //Debug.Log("Our clones list has capacity " + maxClones);
    }
  }

  private void IncrementReferenceCount(Clone clone)
  {
    ++clone.numObjectsReferencedBy;
  }

  private void DecrementReferenceCount(Clone clone)
  {
    if (0 == --clone.numObjectsReferencedBy)
    {
      // Time to destroy!
      Object.Destroy(clone.cloneMaterial);
      s_clonesBySharedMaterial.Remove(clone.sharedMaterial);
      m_ourClones.Remove(clone);
      //Debug.Log("Removed clone " + clone.cloneMaterial.GetInstanceID() + " of " + clone.sharedMaterial.GetInstanceID());
    }
  }

  private bool UsingClone(Material cloneMaterial)
  {
    foreach (Clone clone in m_ourClones)
    {
      if (clone.cloneMaterial == cloneMaterial)
        return true;
    }
    return false;
  }
  
  // Applies clones of the shared materials as the instanced materials. Clones
  // are instantiated only once and are therefore themselves shared between any
  // object instances that clone the same shared materials.
  public void ApplySharedMaterialClones()
  {
    LazyInitAll();
    DestroyInstancedMaterials();
    foreach (Renderer renderer in gameObject.GetComponentsInChildren<Renderer>())
    {
      Material[] sharedMaterials = s_sharedMaterialsByRenderer[renderer];
      Material[] cloneMaterials = new Material[sharedMaterials.Length];
      for (int i = 0; i < sharedMaterials.Length; i++)
      {
        Material sharedMaterial = sharedMaterials[i];
        Clone clone = null;
        if (!s_clonesBySharedMaterial.TryGetValue(sharedMaterial, out clone))
        {
          // Create new clone
          clone = new Clone(sharedMaterial);
          s_clonesBySharedMaterial.Add(sharedMaterial, clone);
          //Debug.Log("Created clone of " + sharedMaterial.GetInstanceID() + ": " + clone.cloneMaterial.GetInstanceID());
        }
        else
        {
          //Debug.Log("Retrieved clone of " + sharedMaterial.GetInstanceID() + ": " + clone.cloneMaterial.GetInstanceID());
        }
        if (SetInsert(m_ourClones, clone))
        {
          // Reference count incremented only once for each gameObject that
          // uses a cloned material
          IncrementReferenceCount(clone);
        }
        cloneMaterials[i] = clone.cloneMaterial;
      }
      renderer.materials = cloneMaterials;
    }
  }

  public void RestoreSharedMaterials()
  {
    DestroyInstancedMaterials();
    foreach (Renderer renderer in gameObject.GetComponentsInChildren<Renderer>())
    {
      Material[] sharedMaterials = s_sharedMaterialsByRenderer[renderer];
      renderer.materials = sharedMaterials;
    }
  }

  // This function must be called first before *any* access to materials[] or 
  // sharedMaterials[] by parent objects. Saves sharedMaterials[] because 
  // instancing overwrites both materials[] *and* sharedMaterials[]!
  public void SaveSharedMaterials()
  {
    LazyInitStatics();
    Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>();
    foreach (Renderer renderer in renderers)
    {
      Material[] sharedMaterials = renderer.sharedMaterials;
      s_sharedMaterialsByRenderer[renderer] = sharedMaterials;
    }
  }

  private void DestroyInstancedMaterials()
  {
    //TODO: optimization: if sharedMaterials[] == s_sharedMaterialsByRenderer[renderer],
    //      then we know materials[] hasn't been modified and there is no need to free!
    //      Whenever materials[] is modified, sharedMaterials[] is set equal to it. No
    //      need to free!
    foreach (Renderer renderer in gameObject.GetComponentsInChildren<Renderer>())
    {
      Material[] materials = renderer.materials;  // if this is the first read, Unity will deep copy
      foreach (Material material in materials)
      {
        // If this is a cloned shared material, do nothing because we clean
        // them up automatically on destruction. Otherwise, destroy.
        if (null == m_ourClones || !UsingClone(material))
        {
          // Assuming it is safe to call Object.Destroy() multiple times
          Object.Destroy(material);
        }
      }
    }
  }

  private void DestroySharedMaterialClones()
  {
    if (null == m_ourClones)
      return;
    // Iterate backwards so we can remove from list if neccessary
    for (int i = m_ourClones.Count - 1; i >= 0; i--)
    {
      DecrementReferenceCount(m_ourClones[i]);
    }
  }

  private void Awake()
  {
    if (initializeOnAwake)
    {
      SaveSharedMaterials();
    }
  }

  private void OnDestroy()
  {
    DestroyInstancedMaterials();
    DestroySharedMaterialClones();
  }
}
