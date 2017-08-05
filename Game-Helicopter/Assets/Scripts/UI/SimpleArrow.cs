using System;
using UnityEngine;

public class GuidanceArrowMesh: MonoBehaviour
{
  public float bounceAmplitude = 0.25f;
  public float bounceFrequency = 2;
  public float numberOfBounces = 3;
  public float timeBetweenBounces = 2;
  public Material material;

  private Vector3 m_localPosition;
  private float m_nextBounceTime;

  private void ScheduleNextBounceAnimation(float now)
  {
    m_nextBounceTime = now + timeBetweenBounces;
  }

  private void Update()
  {
    //TODO: rather than oscillate about position, maybe should just always be a positive displacement
    //      (no negative values)

    float now = Time.time;
    float t = now - m_nextBounceTime;
    if (t < 0)
      return;

    float bouncePeriod = 1 / bounceFrequency;
    if (t > numberOfBounces * bouncePeriod)
    {
      ScheduleNextBounceAnimation(now);
      return;
    }

    float bounceOffset = bounceAmplitude * Mathf.Sin(2 * Mathf.PI * t * bounceFrequency);
    transform.localPosition = m_localPosition + Vector3.up * bounceOffset;
  }

  private void OnEnable()
  {
    m_localPosition = transform.localPosition;
    ScheduleNextBounceAnimation(Time.time);
  }

  private void CreateMesh(Mesh mesh)
  {
    mesh.vertices = new Vector3[]
    {
      // Just a simple triangle for now, pointing toward "up" (y) with
      // "forward" being normal to visible face. Pivot point is at the
      // pointing vertex.
      new Vector3(0, 0, 0),
      new Vector3(1, -1, 0),
      new Vector3(-1, -1, 0)
    };

    mesh.triangles = new int[]
    {
      0, 1, 2
    };

    mesh.RecalculateBounds();
    mesh.RecalculateNormals();
  }

  private void Awake()
  {
    Mesh mesh = gameObject.AddComponent<MeshFilter>().mesh;
    MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
    meshRenderer.material = material;
    CreateMesh(mesh);
  }
}
