using UnityEngine;
using UnityEngine.VR.WSA;
using System.Collections;
using System.Collections.Generic;

public class ParticleEffectsManager: MonoBehaviour
{
  [Tooltip("Prefab for billboard explosion. Must destroy itself after animation completed.")]
  public ExplosionBillboard m_explosion_billboard1_prefab;

  [Tooltip("Prefab for volumetric spherical explosion. Must destroy itself after animation completed.")]
  public ExplosionSphere m_explosion_sphere_prefab;

  [Tooltip("Prefab for the bullet impact's flash/illumination effect.")]
  public GroundFlash m_ground_flash_prefab; //TODO: rename to impact flash?

  [Tooltip("Prefab for the bullet impact's outward spray/blast effect.")]
  public GroundBlast m_ground_blast_prefab; //TODO: rename to impact spray?

  [Tooltip("Prefab for the lingering bullet impact fireball.")]
  public ExplosionSphere m_flame_hemisphere_prefab;

  [Tooltip("Prefab for bullet hole decal.")]
  public GameObject m_bullet_hole_prefab;

  [Tooltip("Prefab for lingering bullet impact dust cloud.")]
  public ExplosionSphere m_dust_hemisphere_prefab;

  [Tooltip("Prefabs for a solid debris fragments (chosen at random).")]
  public TimeLimited[] m_debris_fragment_prefabs;

  private Vector3 RandomPosition(float radius)
  {
    // Random position within a spherical region
    float r = Random.Range(0, radius);
    float theta = Random.Range(0, 180) * Mathf.Deg2Rad;
    float phi = Random.Range(0, 360) * Mathf.Deg2Rad;
    float sin_theta = Mathf.Sin(theta);
    return new Vector3(r * sin_theta * Mathf.Cos(phi), r * sin_theta * Mathf.Sin(phi), r * Mathf.Cos(theta));
  }

  private Vector3 RandomPositionHemisphere(float radius, float theta_max = 90)
  {
    // Random position within a hemispherical region, with +z pointing up, base
    // resting on xy plane. A maximum theta (angle from +z) can be specified.
    // For example, theta_max=10 would restrict points to a narrow cone.
    float r = Random.Range(0, radius);
    float theta = Random.Range(0, Mathf.Clamp(theta_max, 0, 90)) * Mathf.Deg2Rad;
    float phi = Random.Range(0, 360) * Mathf.Deg2Rad;
    float sin_theta = Mathf.Sin(theta);
    return new Vector3(r * sin_theta * Mathf.Cos(phi), r * sin_theta * Mathf.Sin(phi), r * Mathf.Cos(theta));
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
    GroundFlash flash = Instantiate(m_ground_flash_prefab, position + normal * 0.01f, Quaternion.LookRotation(normal)) as GroundFlash;
    GroundBlast blast = Instantiate(m_ground_blast_prefab, position + normal * 0.02f, Quaternion.LookRotation(normal)) as GroundBlast;
    blast.transform.localScale = new Vector3(0.03f, 0.03f, 0.03f);  // scale down to real world size
  }

  public void CreateBulletHole(Vector3 position, Vector3 normal)
  {
    GameObject hole = Instantiate(m_bullet_hole_prefab, position + normal * .005f, Quaternion.LookRotation(normal)) as GameObject;
    hole.AddComponent<WorldAnchor>();
  }

  public void CreateLingeringFireball(Vector3 position, Vector3 normal, float start_time_in_seconds)
  {
    ExplosionSphere hemisphere = Instantiate(m_flame_hemisphere_prefab, position + normal * .01f, Quaternion.LookRotation(normal)) as ExplosionSphere;
    hemisphere.delayTime = start_time_in_seconds;
  }

  public void CreateBulletImpactDebris(Vector3 origin, Vector3 normal, float radius, int count, float start_time_in_seconds)
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
    Vector3 plane_x_axis;
    Vector3 plane_y_axis;
    if (Mathf.Abs(normal.z) < 1e-6)
    {
      if (Mathf.Abs(normal.y) < 1e-6)
      {
        // Normal along x axis, trivial to pick perpendicular axes
        plane_x_axis = new Vector3(0, 0, 1);
      }
      else
      {
        plane_x_axis = new Vector3(1, -(a + 2 * c + d) / b, 2) - origin;
      }
    }
    else
      plane_x_axis = new Vector3(1, 2, -(a + 2 * b + d) / c) - origin;
    plane_x_axis = Vector3.Normalize(plane_x_axis);
    plane_y_axis = Vector3.Normalize(Vector3.Cross(plane_x_axis, normal));

    const float delay_time = 0;
    float start_time = start_time_in_seconds;
    while (count-- > 0)
    {
      // Spawn cloud at a random location within a circular region
      Vector2 pos2d = RandomPosition2D(radius);
      Vector3 pos = origin + pos2d.x * plane_x_axis + pos2d.y * plane_y_axis;
      ExplosionSphere hemisphere = Instantiate(m_dust_hemisphere_prefab, pos, Quaternion.LookRotation(normal)) as ExplosionSphere;
      hemisphere.delayTime = start_time;
      start_time += delay_time;

      // Launch pieces of flying debris in random directions along an outward-
      // facing hemisphere. Note that RandomPositionHemisphere() places a point
      // in a hemisphere with base in xy and axis along z (forward).
      Vector3 fly_towards = Quaternion.FromToRotation(Vector3.forward, normal) * Vector3.Normalize(RandomPositionHemisphere(radius));
      int which = Random.Range(0, m_debris_fragment_prefabs.Length);
      TimeLimited debris = Instantiate(m_debris_fragment_prefabs[which], pos, Quaternion.identity) as TimeLimited;
      Rigidbody rb = debris.GetComponent<Rigidbody>();
      rb.maxAngularVelocity = 30;
      rb.AddForce(rb.mass * (Vector3.up + fly_towards), ForceMode.Impulse);  // slightly biased toward flying upward
      rb.AddRelativeTorque(10f * rb.mass * Vector3.right, ForceMode.Impulse);
    }
  }

  public void CreateExplosionCloud(Vector3 position, float radius, int count, float delay_time = 0)
  {
    float start_time = 0;
    while (count-- > 0)
    {
      Vector3 pos = position + RandomPosition(radius);
      ExplosionBillboard billboard_explosion = Instantiate(m_explosion_billboard1_prefab, pos, m_explosion_billboard1_prefab.transform.rotation) as ExplosionBillboard;
      ExplosionSphere volumetric_explosion = Instantiate(m_explosion_sphere_prefab, pos, m_explosion_sphere_prefab.transform.rotation) as ExplosionSphere;
      billboard_explosion.delayTime = start_time;
      volumetric_explosion.delayTime = start_time;
      start_time += delay_time;
    }
  }

  public void CreateExplosionPillar(Vector3 base_pos, float height, int count, float delay_time = 0)
  {
    float vertical_step_size = height / count;
    Vector3 pos = base_pos;
    float start_time = Time.time;
    while (count-- > 0)
    {
      ExplosionBillboard billboard_explosion = Instantiate(m_explosion_billboard1_prefab, pos, m_explosion_billboard1_prefab.transform.rotation) as ExplosionBillboard;
      ExplosionSphere volumetric_explosion = Instantiate(m_explosion_sphere_prefab, pos, m_explosion_sphere_prefab.transform.rotation) as ExplosionSphere;
      billboard_explosion.delayTime = start_time;
      volumetric_explosion.delayTime = start_time;
      start_time += delay_time;
      pos += Vector3.up * vertical_step_size;
    }
  }
}
