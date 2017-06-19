/*
 * Notes on dynamic NavMesh construction
 * -------------------------------------
 * - A NavMesh must be built before any NavMeshAgent can be instantiated.
 *   If NavMeshAgents are present in the scene hierarchy, this NavMesh builder
 *   must be run first (Init()) with some initial meshes. Its parent object can
 *   be forced to start up before other objects using the DefaultExecutionOrder
 *   attribute.
 * - UpdateNavMesh() builds and updates the NavMesh and can be run in blocking
 *   or asynchronous mode. The latter is for dynamically building the mesh and
 *   the former is only used for initial NavMesh construction.
 * - It is possible to use the AsyncOperation token in a coroutine's yield
 *   statement, which causes the coroutine to resume only when the operation
 *   completes. Alternatively, isDone can be polled in an Update() method.
 * - Although the documentation says that empty bounds in UpdateNavMeshData() 
 *   calls will consider all meshes passed in, this is in fact not true. An 
 *   artificially large bounding box is created to ensure everything is 
 *   processed.
 * 
 * Some things I am still unclear about:
 * 
 * - Settings for different agent types. NavMesh.GetSettingsByID() gets the
 *   settings configured in the Navigation window (although these can be set
 *   programmatically, also). To find settings for a given agent type, need to
 *   loop through all the IDs and check the string name to build a map of
 *   string -> integer ID. I *think* settings are part of the hash for 
 *   UpdateNavMeshData(), so perhaps this needs to be called multiple times for
 *   different agent types?
 */
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class DynamicNavMeshBuilder
{
  private List<NavMeshBuildSource> m_navMeshSources = new List<NavMeshBuildSource>();
  private NavMeshData m_navMeshData;
  private AsyncOperation m_navMeshOperation = null;

  public bool isFinished
  {
    get
    {
      if (m_navMeshOperation == null)
        return true;
      return m_navMeshOperation.isDone;
    }
  }

  public void AddSourceMeshes(List<MeshFilter> meshFilters, Transform meshParentTransform)
  {
    foreach (MeshFilter meshFilter in meshFilters)
    {
      NavMeshBuildSource src = new NavMeshBuildSource();
      src.shape = NavMeshBuildSourceShape.Mesh;
      src.area = 0;
      src.transform = meshParentTransform.localToWorldMatrix;
      src.sourceObject = meshFilter.sharedMesh;
      m_navMeshSources.Add(src);
    }
  }

  private void UpdateNavMesh(bool async)
  {
    // Navmesh settings
    NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);

    // Bounds set to be enormous to ensure all areas are covered
    Bounds bounds = new Bounds(Vector3.zero, 1000 * Vector3.one);

    // Build!
    if (async)
    {
      // Build navmesh asynchronously from sources
      m_navMeshOperation = NavMeshBuilder.UpdateNavMeshDataAsync(m_navMeshData, settings, m_navMeshSources, bounds);
    }
    else
    {
      // Blocking call used only for initial empty NavMesh
      NavMeshBuilder.UpdateNavMeshData(m_navMeshData, settings, m_navMeshSources, bounds);
    }
  }

  // Start building the NavMesh asynchronously
  public AsyncOperation BuildAsync()
  {
    // Yielding on the returned AsyncOperation object in a coroutine will
    // "sleep" until it's done. In practice, may want to use a coroutine or
    // poll inside of an Update loop to provide a status bar.
    UpdateNavMesh(true);
    return m_navMeshOperation;
  }

  // Must be called before any NavMeshAgent components are instantiated
  public void Init()
  {
    // Create the NavMesh data that is used by the builder. This is a member
    // variable because if we ever want to update the NavMesh again later, we
    // need to pass the same data to the same builder.
    m_navMeshData = new NavMeshData();
    NavMesh.AddNavMeshData(m_navMeshData);

    // Generate an initial, empty NavMesh. Required by NavMeshAgents that may
    // be present in the scene.
    UpdateNavMesh(false);
  }
}
