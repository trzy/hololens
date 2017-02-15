using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FXManager: HoloToolkit.Unity.Singleton<FXManager>
{
  private struct EmitParams
  {
    public float time;
    public int number;
    public Vector3 position;
    public Vector3 up;

    public EmitParams(float t, int num, Vector3 pos, Vector3 u)
    {
      time = t;
      number = num;
      position = pos;
      up = u;
    }
  }

  private ParticleSystem m_impactPS;
  private ParticleSystem m_explosionPS;
  private LinkedList<EmitParams> m_futureExplosions; // in order of ascending time

  private void InsertTimeSorted(LinkedList<EmitParams> list, ref EmitParams item)
  {
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

  public void EmitExplosion(Vector3 position, Vector3 up)
  {
    EmitParams emit = new EmitParams(Time.time, 5, position, up);
    InsertTimeSorted(m_futureExplosions, ref emit);
  }

  private void EmitExplosionNow(EmitParams emit)
  {
    m_explosionPS.transform.position = emit.position;
    m_explosionPS.transform.rotation.SetLookRotation(emit.up);
    m_explosionPS.Emit(emit.number);
  }

  private void Update()
  {
    if (m_futureExplosions.Count == 0)
      return;
    float now = Time.time;
    for (LinkedListNode<EmitParams> node = m_futureExplosions.First; node != null; )
    {
      LinkedListNode<EmitParams> next = node.Next;
      if (now >= node.Value.time)
      {
        EmitExplosionNow(node.Value);
        m_futureExplosions.Remove(node);
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
    m_futureExplosions = new LinkedList<EmitParams>();
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
