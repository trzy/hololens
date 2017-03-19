using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FXManager: HoloToolkit.Unity.Singleton<FXManager>
{
  [Tooltip("Prefab for the bullet impact's flash/illumination effect.")]
  public BulletImpactFlash bulletImpactFlashPrefab;

  [Tooltip("Prefab for the bullet impact's outward spray/blast effect.")]
  public BulletImpactSpray bulletImpactSprayPrefab;

  private struct EmitParams
  {
    public ParticleSystem particleSystem;
    public float time;
    public int number;
    public Vector3 position;
    public Quaternion rotation;

    public EmitParams(ParticleSystem ps, float t, int num, Vector3 pos, Vector3 forward, Vector3 up)
    {
      particleSystem = ps;
      time = t;
      number = num;
      position = pos;
      rotation = Quaternion.LookRotation(forward, up);
    }

    public EmitParams(ParticleSystem ps, float t, int num, Vector3 pos, Vector3 eulerAngles)
    {
      particleSystem = ps;
      time = t;
      number = num;
      position = pos;
      rotation = Quaternion.Euler(eulerAngles);
    }
  }

  private ParticleSystem m_impactPS;
  private ParticleSystem m_explosionPS;
  private ParticleSystem m_randomExplosionPS;
  private ParticleSystem m_randomExplosionSmallPS;
  private ParticleSystem m_flameOutPS;
  private LinkedList<EmitParams> m_futureParticles; // in order of ascending time

  public void CreateBulletImpact(Vector3 position, Vector3 normal)
  {
    BulletImpactFlash flash = Instantiate(bulletImpactFlashPrefab, position + normal * 0.01f, Quaternion.LookRotation(normal)) as BulletImpactFlash;
    BulletImpactSpray spray = Instantiate(bulletImpactSprayPrefab, position + normal * 0.02f, Quaternion.LookRotation(normal)) as BulletImpactSpray;
    spray.transform.localScale = new Vector3(0.03f, 0.03f, 0.03f);  // scale down to real world size
    flash.transform.parent = this.transform;
    spray.transform.parent = this.transform;
  }

  private void InsertTimeSorted(ref EmitParams item)
  {
    LinkedList<EmitParams> list = m_futureParticles;
    if (list.Count == 0 || list.Last.Value.time <= item.time)
    {
      list.AddLast(item);
      return;
    }
    for (LinkedListNode<EmitParams> node = list.First; node != null; )
    {
      if (node.Value.time > item.time)
      {
        list.AddBefore(node, item);
        break;
      }
      node = node.Next;
    }
  }

  public void EmitImpact(Vector3 position)
  {
    m_impactPS.transform.position = position;
    m_impactPS.Emit(1);
  }

  public void EmitTankExplosion(Vector3 position)
  {
    // Horizontal flames
    float t = Time.time;
    float deltaT = .1f;
    EmitParams emit;
    for (int i = 0; i < 2; i++)
    {
      emit = new EmitParams(m_flameOutPS, t, 1, position, -Camera.main.transform.right, Camera.main.transform.up);
      InsertTimeSorted(ref emit);
      emit = new EmitParams(m_flameOutPS, t, 1, position, Camera.main.transform.right, Camera.main.transform.up);
      InsertTimeSorted(ref emit);
      t += deltaT;
    }

    // Large central explosion
    t = Time.time;
    Vector3 pos = position;
    for (int i = 0; i < 2; i++)
    {
      t += .1f;
      // Note: local forward is the direction of particle motion, hence to move
      // up, we set *forward* direction to Vector3.up
      emit = new EmitParams(m_randomExplosionPS, t, 1, pos, Vector3.up, Vector3.forward);
      InsertTimeSorted(ref emit);
      pos += .1f * Vector3.up;
    }

    // Occasionally, a series of small bursts after a short pause
    float r = Random.Range(0f, 1f);
    if (r < 0.33f) // 33% chance
    {
      t += 0.33f;
      for (int i = 0; i < 5; i++)
      {
        emit = new EmitParams(m_randomExplosionSmallPS, t, 1, pos, Vector3.up, Vector3.forward);
        InsertTimeSorted(ref emit);
        t += 0.1f;
      }
    }
  }

  private void EmitParticlesNow(EmitParams emit)
  {
    emit.particleSystem.transform.position = emit.position;
    emit.particleSystem.transform.rotation = emit.rotation;
    emit.particleSystem.Emit(emit.number);
  }

  private void Update()
  {
    if (m_futureParticles.Count == 0)
      return;
    float now = Time.time;
    for (LinkedListNode<EmitParams> node = m_futureParticles.First; node != null; )
    {
      LinkedListNode<EmitParams> next = node.Next;
      if (now >= node.Value.time)
      {
        EmitParticlesNow(node.Value);
        m_futureParticles.Remove(node);
        node = next;
      }
      else
      {
        // Remaining elements are in the future
        break;
      }
    }
  }

  private new void Awake()
  {
    base.Awake();
    m_futureParticles = new LinkedList<EmitParams>();
    foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>())
    {
      if (ps.name == "Impact")
      {
        m_impactPS = ps;
      }
      else if (ps.name == "Explosion")
      {
        m_explosionPS = ps;
      }
      else if (ps.name == "RandomExplosion")
      {
        m_randomExplosionPS = ps;
      }
      else if (ps.name == "RandomExplosionSmall")
      {
        m_randomExplosionSmallPS = ps;
      }
      else if (ps.name == "FlameOut")
      {
        m_flameOutPS = ps;
      }
    }
  }

  private void Start()
  {
  }
}
