using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rope : MonoBehaviour
{
  public Material material;

  private void InitGameObject()
  {
    // Create reticle game object and mesh
    gameObject.AddComponent<Cloth>();
    SkinnedMeshRenderer meshRenderer = GetComponent<SkinnedMeshRenderer>();
    meshRenderer.material = material;
    meshRenderer.enabled = true;
  }

  private void Start()
  {
    InitGameObject();
  }
}