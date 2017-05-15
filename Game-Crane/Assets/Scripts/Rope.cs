using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rope: MonoBehaviour
{
  [Tooltip("Total relaxed length of rope in meters")]
  public float ropeLength = 1;

  [Tooltip("Number of point masses/segments in rope (larger numbers increase the error in relaxed length)")]
  public int numSegments = 10;

  [Tooltip("Verlet integrator solution frequency in Hz")]
  public int solverFrequency = 60;

  [Tooltip("Number of iterations per solution")]
  public int solverIterations = 3;

  [Tooltip("Object suspended from rope")]
  public Joint connectedJoint = null;

  [Tooltip("Non-working experimental feature: attach suspended object to last rope segment and use Verlet engine to drive motion")]
  public bool connectToBone = false;

  [Tooltip("Draw rope using a procedurally-generated cylindrical skinned mesh")]
  public bool drawSkinnedMesh = true;

  [Tooltip("Material to apply to skinned mesh")]
  public Material material = null;

  [Tooltip("Double-sided skinned mesh (buggy)")]
  public bool drawDoubleSided = false;

  [Tooltip("Number of sides on skinned mesh cylinder")]
  public int numberOfSides = 8;

  [Tooltip("Draw rope with line renderer (for debugging)")]
  public bool drawLines = true;

  [Tooltip("Draw rope as a chain of capsules (for debugging)")]
  public bool drawCapsules = false;

  private Verlet.System m_verlet;
  private List<Verlet.IBody> m_bodies;
  private GameObject m_lineObject;
  private LineRenderer m_line;
  private GameObject[] m_capsules;
  private SkinnedMeshRenderer m_skinnedMesh;
  private Mesh m_mesh;

  private void Update()
  {
    m_verlet.Update(Time.deltaTime);

    if (drawLines)
    {
      for (int i = 0; i < m_bodies.Count; i++)
      {
        m_line.SetPosition(i, m_bodies[i].position);
      }
    }

    if (drawCapsules)
    {
      for (int i = 1; i < m_bodies.Count; i++)
      {
        m_capsules[i - 1].transform.position = 0.5f * (m_bodies[i - 1].position + m_bodies[i].position);
        Quaternion rotation = m_capsules[i - 1].transform.rotation;
        rotation.SetFromToRotation(Vector3.up, (m_bodies[i - 1].position - m_bodies[i].position).normalized);
        m_capsules[i - 1].transform.rotation = rotation;
      }
    }

    if (drawSkinnedMesh)
    {
      for (int i = 0; i < m_bodies.Count; i++)
      {
        m_skinnedMesh.bones[i].position = m_bodies[i].GetFramePosition(m_verlet.lerp); //m_bodies[i].position;
      }
    }
  }

  private void CreateSkinnedMesh()
  {
    // Generate a ring for each node. We will later connect adjacent rings with
    // quads to form sides. Pivot point is top of rope and we move downwards.
    List<Vector3> verts = new List<Vector3>();
    List<BoneWeight> weights = new List<BoneWeight>();
    float radius = 1;
    float segmentLength = ropeLength / numSegments;
    int numBones = numSegments + 1; // need one extra bone to cap the end
    int vertIdx = 0;
    for (int i = 0; i < numBones; i++)
    {
      float y = -i * segmentLength;
      for (int face = 0; face < (drawDoubleSided ? 2 : 1); face++)
      {
        for (int j = 0; j < numberOfSides; j++)
        {
          // Create vertex
          float angle = j * (360f / numberOfSides) * Mathf.Deg2Rad;
          float x = radius * Mathf.Cos(angle);
          float z = radius * Mathf.Sin(angle);
          verts.Add(new Vector3(x, y, z));

          // Assign weight
          BoneWeight weight = new BoneWeight();
          weight.boneIndex0 = i;
          weight.weight0 = 1;
          weight.boneIndex1 = 0;
          weight.weight1 = 0;
          weight.boneIndex2 = 0;
          weight.weight2 = 0;
          weight.boneIndex3 = 0;
          weight.weight3 = 0;
          weights.Add(weight);

          vertIdx += 1;
        }
      }
    }

    // Connect adjacent rings with quads
    List<int> tris = new List<int>();
    int vertsPerRing = numberOfSides;
    vertIdx = 0;
    for (int i = 0; i < numBones - 1; i++)
    {
      for (int face = 0; face < (drawDoubleSided ? 2 : 1); face++)
      {
        // Each pair of vertices around the ring is connected with the level
        // below it to form a quad comprised of two triangles
        for (int j = 0; j < vertsPerRing; j++)
        {
          int topLeft = vertIdx + 0 + j;
          int topRight = vertIdx + (1 + j) % vertsPerRing;
          int bottomLeft = topLeft + vertsPerRing;
          int bottomRight = topRight + vertsPerRing;

          if (face == 0)
          {
            // Front facing
            tris.Add(topLeft);
            tris.Add(bottomRight);
            tris.Add(bottomLeft);
            tris.Add(topLeft);
            tris.Add(topRight);
            tris.Add(bottomRight);
          }
          else
          {
            // Back facing
            tris.Add(topLeft);
            tris.Add(bottomLeft);
            tris.Add(bottomRight);
            tris.Add(topLeft);
            tris.Add(bottomRight);
            tris.Add(topRight);
          }
        }
        vertIdx += vertsPerRing;
      }
    }

    // Create mesh
    m_skinnedMesh = gameObject.AddComponent<SkinnedMeshRenderer>();
    m_skinnedMesh.enabled = drawSkinnedMesh;
    m_mesh = new Mesh();
    m_mesh.vertices = verts.ToArray();
    m_mesh.triangles = tris.ToArray();
    m_mesh.RecalculateNormals();
    m_mesh.boneWeights = weights.ToArray();

    // Create bones
    Transform[] bones = new Transform[numBones];
    Matrix4x4[] bindPoses = new Matrix4x4[numBones];
    for (int i = 0; i < numBones; i++)
    {
      bones[i] = new GameObject("Bone" + i).transform;
      bones[i].parent = transform;
      bones[i].localRotation = Quaternion.identity;
      bones[i].localPosition = new Vector3(0, -i * segmentLength, 0);

      // Bind pose is inverse transform matrix
      bindPoses[i] = bones[i].worldToLocalMatrix * transform.localToWorldMatrix;
    }

    // Hook up bones and mesh
    m_mesh.bindposes = bindPoses;
    m_skinnedMesh.bones = bones;
    m_skinnedMesh.sharedMesh = m_mesh;
    m_skinnedMesh.sharedMaterial = material;
  }

  private void OnDestroy()
  {
    Object.Destroy(m_mesh);
  }

  private void CreateLineRenderer()
  {
    if (!drawLines)
      return;    
    m_lineObject = new GameObject("line");
    m_line = m_lineObject.AddComponent<LineRenderer>();
    m_line.startWidth = .01f;
    m_line.endWidth = .01f;
    m_line.startColor = Color.red;
    m_line.endColor = Color.red;
    m_line.positionCount = m_bodies.Count;
    //m_line.material = material;
  }

  private void CreateCapsules()
  {
    if (!drawCapsules)
      return;
    float segmentLength = ropeLength / numSegments;
    m_capsules = new GameObject[m_bodies.Count - 1];
    for (int i = 0; i < m_bodies.Count - 1; i++)
    {
      m_capsules[i] = GameObject.CreatePrimitive(PrimitiveType.Capsule);
      m_capsules[i].transform.localScale = new Vector3(0.05f, 0.5f * segmentLength, 0.05f);
    }
  }

  private void Awake()
  {
    m_verlet = new Verlet.System(1f / solverFrequency, solverIterations);
    m_bodies = new List<Verlet.IBody>();

    // Construct rope with anchor assumed to be at pivot of this object
    Verlet.Anchor anchor = new Verlet.Anchor(transform);
    m_verlet.AddBody(anchor);
    m_bodies.Add(anchor);
    float segmentLength = ropeLength / numSegments;
    int numPoints = numSegments + 1;
    for (int i = 1; i < numPoints; i++)
    {
      Verlet.IBody point;
      if (connectedJoint && !connectToBone && i == numPoints - 1)
      {
        point = new Verlet.Anchor(connectedJoint);
        //point = new Verlet.Anchor(transform.position + Vector3.right * 0.25f);
      }
      else
        point = new Verlet.PointMass(transform.position - segmentLength * transform.up * i, 1);
      Verlet.IBody lastPoint = m_bodies[m_bodies.Count - 1];
      Verlet.IConstraint link = new Verlet.LinkConstraint(lastPoint, point, segmentLength, 1f);
      point.AddConstraint(link);
      point.AddForce(new Vector3(0, -9.8f * point.mass, 0));
      m_verlet.AddBody(point);
      m_bodies.Add(point);
    }

    CreateLineRenderer();
    CreateCapsules();
    CreateSkinnedMesh();

    // Attach bottom bone to object -- not working!
    if (connectedJoint && connectToBone)
    {
      Verlet.IBody attachNode = m_bodies[m_bodies.Count - 1];
      GameObject attachBone = m_skinnedMesh.bones[m_skinnedMesh.bones.Length - 1].gameObject;
      Rigidbody rb = attachBone.AddComponent<Rigidbody>();
      connectedJoint.connectedBody = rb;
      ConfigurableJoint cfj = connectedJoint as ConfigurableJoint;
      cfj.xMotion = ConfigurableJointMotion.Locked;
      cfj.yMotion = ConfigurableJointMotion.Locked;
      cfj.zMotion = ConfigurableJointMotion.Locked;
      SoftJointLimit limit = cfj.linearLimit;
      limit.limit = 0;
      cfj.linearLimit = limit;
    }
  }
}
