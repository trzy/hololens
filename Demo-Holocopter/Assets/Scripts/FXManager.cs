using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FXManager: HoloToolkit.Unity.Singleton<FXManager>
{
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
    /*
    m_ps = gameObject.AddComponent<ParticleSystem>();
    //m_ps.useAutoRandomSeed = false;

    var main = m_ps.main;
    main.loop = false;
    main.playOnAwake = true;
        
    var emission = m_ps.emission;
    emission.enabled = true;

    var shape = m_ps.shape;
    shape.enabled = false;

    var velocityOverLifetime = m_ps.velocityOverLifetime;
    velocityOverLifetime.enabled = false;

    var limitVelocityOverLifetime = m_ps.limitVelocityOverLifetime;
    limitVelocityOverLifetime.enabled = true;
    limitVelocityOverLifetime.limit = 0;

    var inheritVelocity = m_ps.inheritVelocity;
    inheritVelocity.enabled = false;

    var forceOverLifetime = m_ps.forceOverLifetime;
    forceOverLifetime.enabled = false;

    var colorOverLifetime = m_ps.colorOverLifetime;
    colorOverLifetime.enabled = false;

    var colorBySpeed = m_ps.colorBySpeed;
    colorBySpeed.enabled = false;

    var sizeOverLifetime = m_ps.sizeOverLifetime;
    sizeOverLifetime.enabled = false;

    var sizeBySpeed = m_ps.sizeBySpeed;
    sizeBySpeed.enabled = false;

    var rotationOverLifetime = m_ps.rotationOverLifetime;
    rotationOverLifetime.enabled = false;

    var rotationBySpeed = m_ps.rotationBySpeed;
    rotationBySpeed.enabled = false;

    var externalForces = m_ps.externalForces;
    externalForces.enabled = false;

    var noise = m_ps.noise;
    noise.enabled = false;

    var collision = m_ps.collision;
    collision.enabled = false;

    var triggers = m_ps.trigger;
    triggers.enabled = false;

    var subEmitters = m_ps.subEmitters;
    subEmitters.enabled = false;

    var textureSheetAnimation = m_ps.textureSheetAnimation;
    textureSheetAnimation.enabled = true;
    textureSheetAnimation.numTilesX = 4;
    textureSheetAnimation.numTilesY = 4;
    textureSheetAnimation.animation = ParticleSystemAnimationType.WholeSheet;
    textureSheetAnimation.frameOverTime = new ParticleSystem.MinMaxCurve(0, 16);
    textureSheetAnimation.startFrame = 0;
    textureSheetAnimation.cycleCount = 1;
    textureSheetAnimation.flipU = 0;
    textureSheetAnimation.flipV = 0;
    textureSheetAnimation.uvChannelMask =
      UnityEngine.Rendering.UVChannelFlags.UV0 |
      UnityEngine.Rendering.UVChannelFlags.UV1 |
      UnityEngine.Rendering.UVChannelFlags.UV2 |
      UnityEngine.Rendering.UVChannelFlags.UV3;

    var lights = m_ps.lights;
    lights.enabled = false;

    var trails = m_ps.trails;
    trails.enabled = false;

    var renderer = GetComponent<ParticleSystemRenderer>();
    renderer.renderMode = ParticleSystemRenderMode.Billboard;
    renderer.normalDirection = 1;
    renderer.material = textureSheet;
    */

  }
}
