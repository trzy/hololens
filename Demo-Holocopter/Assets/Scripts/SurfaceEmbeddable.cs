using UnityEngine;
using UnityEngine.VR.WSA;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

public class SurfaceEmbeddable: MonoBehaviour
{
  void Start()
  {
    /*
     * Geometry is drawn with a render queue queue of 2000. Surface meshes
     * produced by HoloToolkit are at a lower value (1999 at the time of this
     * writing), meaning they are drawn first. This means they will occlude any
     * objects positioned so as to appear embedded in the surface.
     * 
     * A solution that does not require altering the surface meshes is to draw
     * embedded objects *before* the surface meshes. And, within those objects,
     * the occluding material must be drawn first. HoloToolkit's 
     * WindowOcclusion shader draws with a render queue of 1999 (i.e., higher
     * priority than geometry). 
     * 
     * We assume here that an embeddable object is comprised of meshes that
     * render as ordinary geometry or as occlusion surfaces with a small render
     * priority offset. We re-assign render queue priorities beginning at 1000,
     * incrementing by one for each unique render queue priority found among
     * the meshes. This is done in sorted order, so that the lowest render
     * queue value (which will be the occlusion material) is mapped to 1000.
     */
    Renderer renderer = GetComponent<Renderer>();
    List<Material> materials = renderer.materials.OrderBy(element => element.renderQueue).ToList();
    int new_priority = 1000 - 1;  // we will preincrement to 1000
    int last_priority = -1;
    foreach (Material material in materials)
    {
      new_priority += (material.renderQueue != last_priority ? 1 : 0);
      last_priority = material.renderQueue;
      material.renderQueue = new_priority;
    }
    gameObject.AddComponent<WorldAnchor>();
  }
}
