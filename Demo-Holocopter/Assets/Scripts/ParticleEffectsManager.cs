using UnityEngine;
using UnityEngine.VR.WSA;
using System.Collections;
using System.Collections.Generic;

public class ParticleEffectsManager: HoloToolkit.Unity.Singleton<ParticleEffectsManager>
{
  [Tooltip("Prefab for billboard explosion. Must destroy itself after animation completed.")]
  public ExplosionBillboard explosionBillboard1Prefab;

  [Tooltip("Prefab for volumetric spherical explosion. Must destroy itself after animation completed.")]
  public ExplosionSphere explosionSpherePrefab;

  [Tooltip("Prefab for the bullet impact's flash/illumination effect.")]
  public GroundFlash groundFlashPrefab; //TODO: rename to impact flash?

  [Tooltip("Prefab for the bullet impact's outward spray/blast effect.")]
  public GroundBlast groundBlastPrefab; //TODO: rename to impact spray?

  [Tooltip("Prefab for the lingering bullet impact fireball.")]
  public ExplosionSphere flameHemispherePrefab;

  [Tooltip("Prefab for blast shockwave.")]
  public ExplosionSphere blastHemispherePrefab;

  [Tooltip("Prefab for bullet hole decal.")]
  public GameObject bulletHolePrefab;

  [Tooltip("Maximum number of bullet holes allowed.")]
  public int maxBulletHoles = 10;

  [Tooltip("Prefab for a crater model.")]
  public GameObject craterPrefab;

  [Tooltip("Prefab for lingering bullet impact dust cloud.")]
  public ExplosionSphere dustHemispherePrefab;

  [Tooltip("Prefabs for a solid debris fragments (chosen at random).")]
  public TimeLimited[] debrisFragmentPrefabs;

  //TODO: refactor this logic into a class if used more than once
  private Queue<GameObject> m_bulletHoles;

  // 
  class PopulationLimitingBuffer
  {
    private GameObject[] m_array;
    private int m_idx = 0;
    private GameObject m_parent;

    private bool Intersects(Vector3 center, float radius, GameObject other)
    {
      SphereCollider collider = other.GetComponent<SphereCollider>();
      Vector3 scale = other.transform.lossyScale;
      float otherRadius = Mathf.Max(scale.x, scale.y, scale.z) * collider.radius;
      Vector3 otherCenter = other.transform.position;  // assume sphere collider is centered about object position
      return Vector3.Magnitude(center - otherCenter) < (radius + otherRadius);
    }

    public void Insert(Vector3 position, Vector3 normal)
    {
      // Object we want to try to place
      GameObject obj = m_array[m_idx];
      /*
      SphereCollider collider = obj.GetComponent<SphereCollider>();
      Vector3 scale = obj.transform.lossyScale;
      float radius = Mathf.Max(scale.x, scale.y, scale.z) * collider.radius;
      // Check against all existing objects
      foreach (GameObject other in m_array)
      {
        if (other.activeSelf && Intersects(position, radius, other))
        {
          return; // cannot place
        }
      }
      */
      // Place (or rather, replace existing object)
      obj.transform.position = position;
      obj.transform.rotation = Quaternion.LookRotation(normal);
      obj.SetActive(true);
      m_idx = (m_idx + 1) % m_array.Length;


/*
      // Save original bounds and positioning
      GameObject obj = m_array[m_idx];
      //Renderer renderer = obj.GetComponent<Renderer>();
      BoxCollider renderer = obj.GetComponent<BoxCollider>();
      Bounds original_bounds = renderer.bounds;
      Vector3 old_position = obj.transform.position;
      Quaternion old_rotation = obj.transform.rotation;
      // Try to place new bullet hole
      obj.transform.position = position;
      obj.transform.rotation = Quaternion.LookRotation(normal);
      Bounds new_bounds = renderer.bounds;
      Debug.Log("Old bounds:" + original_bounds + ", new bounds:" + new_bounds);
      // Check against original bullet hole and then all others
      bool collision = new_bounds.Intersects(original_bounds);
      for (int i = 0; i < m_idx - 1 && !collision; i++)
        collision = new_bounds.Intersects(m_array[i].GetComponent<Renderer>().bounds);
      for (int i = m_idx + 1; i < m_array.Length && !collision; i++)
        collision = new_bounds.Intersects(m_array[i].GetComponent<Renderer>().bounds);
      if (collision)
      {
        Debug.Log("cannot place!");
        // Cannot place here, restore old object
        obj.transform.position = old_position;
        obj.transform.rotation = old_rotation;
        return;
      }
      // Made it! The new placement is okay
      obj.SetActive(true);
      m_idx = (m_idx + 1) % m_array.Length;
*/
    }

    public PopulationLimitingBuffer(GameObject bulletHole_prefab, int max_population, GameObject parent)
    {
      m_array = new GameObject[max_population];
      m_parent = parent;
      for (int i = 0; i < m_array.Length; i++)
      {
        m_array[i] = Instantiate(bulletHole_prefab) as GameObject;
        //m_array[i].AddComponent<WorldAnchor>(); //TODO: disable these?
        //m_array[i].transform.parent = m_parent.transform;
        m_array[i].SetActive(false);
      }
    }
  }

  private PopulationLimitingBuffer m_bulletHoleBuffer;

  private Vector3 RandomPosition(float radius)
  {
    // Random position within a spherical region
    float r = Random.Range(0, radius);
    float theta = Random.Range(0, 180) * Mathf.Deg2Rad;
    float phi = Random.Range(0, 360) * Mathf.Deg2Rad;
    float sinTheta = Mathf.Sin(theta);
    return new Vector3(r * sinTheta * Mathf.Cos(phi), r * sinTheta * Mathf.Sin(phi), r * Mathf.Cos(theta));
  }

  private Vector3 RandomPositionHemisphere(float radius, float theta_max = 90)
  {
    // Random position within a hemispherical region, with +z pointing up, base
    // resting on xy plane. A maximum theta (angle from +z) can be specified.
    // For example, theta_max=10 would restrict points to a narrow cone.
    float r = Random.Range(0, radius);
    float theta = Random.Range(0, Mathf.Clamp(theta_max, 0, 90)) * Mathf.Deg2Rad;
    float phi = Random.Range(0, 360) * Mathf.Deg2Rad;
    float sinTheta = Mathf.Sin(theta);
    return new Vector3(r * sinTheta * Mathf.Cos(phi), r * sinTheta * Mathf.Sin(phi), r * Mathf.Cos(theta));
  }

  private Vector2 RandomPosition2D(float radius)
  {
    // Random radius within a circular region
    float r = Random.Range(0, radius);
    float theta = Random.Range(0, 180) * Mathf.Deg2Rad;
    return r * new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));
  }

  public void CreateBulletImpact(Vector3 position, Vector3 normal)
  {
    GroundFlash flash = Instantiate(groundFlashPrefab, position + normal * 0.01f, Quaternion.LookRotation(normal)) as GroundFlash;
    GroundBlast blast = Instantiate(groundBlastPrefab, position + normal * 0.02f, Quaternion.LookRotation(normal)) as GroundBlast;
    blast.transform.localScale = new Vector3(0.03f, 0.03f, 0.03f);  // scale down to real world size
    flash.transform.parent = this.transform;
    blast.transform.parent = this.transform;
  }

  //TODO: move this function into Bullet.cs
  //TODO: anchors should be embedded into the SurfacePlane and then the bullet holes attached to that plane
  public void CreateBulletHole(Vector3 position, Vector3 normal, HoloToolkit.Unity.SpatialMapping.SurfacePlane plane)
  {
    //m_bulletHoleBuffer.Insert(position + normal * .005f, normal);

    
    GameObject bulletHole = Instantiate(bulletHolePrefab, position + normal * .005f, Quaternion.LookRotation(normal)) as GameObject;
    bulletHole.AddComponent<WorldAnchor>();
    bulletHole.transform.parent = this.transform;
    //SpatialMeshDeformationManager.Instance.Embed(bulletHole);
    SurfacePlaneDeformationManager.Instance.Embed(bulletHole, plane);
    //TODO: logic for objects to self destruct after not being gazed at for long enough?
    m_bulletHoles.Enqueue(bulletHole);
    while (m_bulletHoles.Count > maxBulletHoles)
    {
      GameObject oldBulletHole = m_bulletHoles.Dequeue();
      if (oldBulletHole)  // if hasn't destroyed itself already
      {
        Destroy(oldBulletHole);
      }
    }
    
  }

  public void CreateCrater(Vector3 position, Vector3 normal)
  {
    /*
    GameObject crater = Instantiate(craterPrefab, position + normal * 0, Quaternion.LookRotation(normal)) as GameObject;
    crater.AddComponent<WorldAnchor>();
    crater.transform.parent = this.transform;
    */
  }

  public void CreateLingeringFireball(Vector3 position, Vector3 normal, float startTimeInSeconds)
  {
    ExplosionSphere hemisphere = Instantiate(flameHemispherePrefab, position + normal * .01f, Quaternion.LookRotation(normal)) as ExplosionSphere;
    hemisphere.delayTime = startTimeInSeconds;
    hemisphere.transform.parent = this.transform;
  }

  public void CreateBulletImpactDebris(Vector3 origin, Vector3 normal, float radius, int count, float startTimeInSeconds)
  {
    /*
     * Generate random points on the plane described by the impact position and
     * normal.
     * 
     * Find two perpendicular vectors in plane to form a basis set. The second
     * is just the cross product of plane normal and first vector. The first
     * vector is any arbitrary vector parallel to plane. Equation of plane is:
     *
     *  (P-O).N = 0 -> nx*x + ny*y + nz*z - (nx*ox+ny*oy+nz*oz) = 0
     *
     * Where P = (x,y,z) is some arbitrary point on plane, O is a known point,
     * and N is the plane normal vector. Therefore:
     * 
     *  a = nx, b = ny, c = nz, d = -(N.O)
     *
     * We can solve for z:
     *
     *  z = -(a*x + b*y + d) / c
     *  
     * Then pick x and y to find z:
     * 
     *  x = 1, y = 2, z = -(a + 2b + d) / c
     *  
     * If nz = 0, just pick x and z:
     * 
     *  y = -(a*x + c*z + d) / b
     *  x = 1, z = 2, y = -(a + 2*c + d) / b
     *
     * Remember that this solves for the *point*, P. To get the axis, subtract
     * O.
     */

    float a = normal.x;
    float b = normal.y;
    float c = normal.z;
    float d = -Vector3.Dot(normal, origin);
    Vector3 planeXAxis;
    Vector3 planeYAxis;
    if (Mathf.Abs(normal.z) < 1e-6)
    {
      if (Mathf.Abs(normal.y) < 1e-6)
      {
        // Normal along x axis, trivial to pick perpendicular axes
        planeXAxis = new Vector3(0, 0, 1);
      }
      else
      {
        planeXAxis = new Vector3(1, -(a + 2 * c + d) / b, 2) - origin;
      }
    }
    else
      planeXAxis = new Vector3(1, 2, -(a + 2 * b + d) / c) - origin;
    planeXAxis = Vector3.Normalize(planeXAxis);
    planeYAxis = Vector3.Normalize(Vector3.Cross(planeXAxis, normal));

    const float delayTime = 0;
    float startTime = startTimeInSeconds;
    while (count-- > 0)
    {
      // Spawn cloud at a random location within a circular region
      Vector2 pos2d = RandomPosition2D(radius);
      Vector3 pos = origin + pos2d.x * planeXAxis + pos2d.y * planeYAxis;
      ExplosionSphere hemisphere = Instantiate(dustHemispherePrefab, pos, Quaternion.LookRotation(normal)) as ExplosionSphere;
      hemisphere.delayTime = startTime;
      hemisphere.transform.parent = this.transform;
      startTime += delayTime;

      // Launch pieces of flying debris in random directions along an outward-
      // facing hemisphere. Note that RandomPositionHemisphere() places a point
      // in a hemisphere with base in xy and axis along z (forward).
      Vector3 flyTowards = Quaternion.FromToRotation(Vector3.forward, normal) * Vector3.Normalize(RandomPositionHemisphere(radius));
      int which = Random.Range(0, debrisFragmentPrefabs.Length);
      TimeLimited debris = Instantiate(debrisFragmentPrefabs[which], pos, Quaternion.identity) as TimeLimited;
      debris.transform.parent = this.transform;
      Rigidbody rb = debris.GetComponent<Rigidbody>();
      rb.maxAngularVelocity = 30;
      rb.AddForce(rb.mass * (Vector3.up + flyTowards), ForceMode.Impulse);  // slightly biased toward flying upward
      rb.AddRelativeTorque(10f * rb.mass * Vector3.right, ForceMode.Impulse);
    }
  }

  public void CreateExplosionCloud(Vector3 position, float radius, int count, float delayTime = 0)
  {
    float startTime = 0;
    while (count-- > 0)
    {
      Vector3 pos = position + RandomPosition(radius);
      ExplosionBillboard billboardExplosion = Instantiate(explosionBillboard1Prefab, pos, explosionBillboard1Prefab.transform.rotation) as ExplosionBillboard;
      ExplosionSphere volumetricExplosion = Instantiate(explosionSpherePrefab, pos, explosionSpherePrefab.transform.rotation) as ExplosionSphere;
      billboardExplosion.delayTime = startTime;
      volumetricExplosion.delayTime = startTime;
      billboardExplosion.transform.parent = this.transform;
      volumetricExplosion.transform.parent = this.transform;
      startTime += delayTime;
    }
  }

  public void CreateExplosionPillar(Vector3 base_pos, float height, int count, float delayTime = 0)
  {
    float verticalStepSize = height / count;
    Vector3 pos = base_pos;
    float startTime = Time.time;
    while (count-- > 0)
    {
      ExplosionBillboard billboardExplosion = Instantiate(explosionBillboard1Prefab, pos, explosionBillboard1Prefab.transform.rotation) as ExplosionBillboard;
      ExplosionSphere volumetricExplosion = Instantiate(explosionSpherePrefab, pos, explosionSpherePrefab.transform.rotation) as ExplosionSphere;
      billboardExplosion.delayTime = startTime;
      volumetricExplosion.delayTime = startTime;
      billboardExplosion.transform.parent = this.transform;
      volumetricExplosion.transform.parent = this.transform;
      startTime += delayTime;
      pos += Vector3.up * verticalStepSize;
    }
  }

  public void CreateExplosionBlastWave(Vector3 ground_position, Vector3 normal, float delayTime = 0)
  {
    ExplosionSphere blast = Instantiate(blastHemispherePrefab, ground_position, Quaternion.LookRotation(normal)) as ExplosionSphere;
    blast.delayTime = delayTime;
    blast.transform.parent = this.transform;
  }

  private new void Awake()
  {
    base.Awake();
    m_bulletHoles = new Queue<GameObject>(maxBulletHoles + 2);
    m_bulletHoleBuffer = new PopulationLimitingBuffer(bulletHolePrefab, maxBulletHoles, this.gameObject);
  }
}
