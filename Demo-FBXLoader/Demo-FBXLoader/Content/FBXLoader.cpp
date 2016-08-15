#include "pch.h"
#include "Content\FBXLoader.h"

static DirectX::XMFLOAT3 ToD3DCoordinateSystem(const DirectX::XMFLOAT3 &vertex)
{
  // Not strictly correct. z should be inverted but this will screw up polygon winding.
  return DirectX::XMFLOAT3(vertex.y, vertex.z, vertex.x);
}

static bool IsClockwise(const DirectX::XMFLOAT3 *vertex, int num_verts, const FbxVector4 &polygon_normal)
{
  // Compute a normal using two edges of the triangle
  FbxVector4 edge_1(vertex[1].x - vertex[0].x, vertex[1].y - vertex[0].y, vertex[1].z - vertex[0].z);
  FbxVector4 edge_2(vertex[2].x - vertex[0].x, vertex[2].y - vertex[0].y, vertex[2].z - vertex[0].z);
  FbxVector4 normal = edge_1.CrossProduct(edge_2);
  // Dot product between computed normal and supplied normal will reveal winding
  double dot = polygon_normal.DotProduct(normal);
  return dot < 0;
}

FBXLoader::FBXLoader(const char *filename)
{
  m_error = true;
  if (m_fbx_sdk_manager == nullptr)
  {
    m_fbx_sdk_manager = FbxManager::Create();
    m_fbx_sdk_manager->SetIOSettings(FbxIOSettings::Create(m_fbx_sdk_manager, IOSROOT));
  }
  FbxImporter *importer = FbxImporter::Create(m_fbx_sdk_manager, "");
  FbxScene *scene = FbxScene::Create(m_fbx_sdk_manager, "");
  if (!importer->Initialize(filename, -1, m_fbx_sdk_manager->GetIOSettings()))
    return;
  if (!importer->Import(scene))
    return;
  importer->Destroy();

  FbxNode *root_node = scene->GetRootNode();
  if (root_node)
  {
    for (int i = 0; i < root_node->GetChildCount(); i++)
    {
      FbxNode *child_node = root_node->GetChild(i);
      if (child_node->GetNodeAttribute() == NULL)
        continue;

      FbxNodeAttribute::EType type = child_node->GetNodeAttribute()->GetAttributeType();
      if (type != FbxNodeAttribute::eMesh)
        continue;

      FbxMesh *mesh = (FbxMesh *) child_node->GetNodeAttribute();
      FbxVector4 *vertices = mesh->GetControlPoints();
      for (int j = 0; j < mesh->GetPolygonCount(); j++)
      {
        int num_verts = mesh->GetPolygonSize(j);
        assert(num_verts == 3);
        DirectX::XMFLOAT3 vertex[3];
        FbxVector4 polygon_normal(0, 0, 0);

        for (int k = 0; k < num_verts; k++)
        {
          int control_point_idx = mesh->GetPolygonVertex(j, k);
          FbxVector4 vertex_normal;
          mesh->GetPolygonVertexNormal(j, k, vertex_normal);
          polygon_normal += vertex_normal;
          vertex[k].x = (float) vertices[control_point_idx].mData[0];
          vertex[k].y = (float) vertices[control_point_idx].mData[1];
          vertex[k].z = (float) vertices[control_point_idx].mData[2];
        }

        if (IsClockwise(vertex, num_verts, polygon_normal))
        {
          for (int i = 0; i < num_verts; i++)
            m_vertices.push_back(ToD3DCoordinateSystem(vertex[i]));
        }
        else
        {
          for (int i = 0; i < num_verts; i++)
            m_vertices.push_back(ToD3DCoordinateSystem(vertex[num_verts - 1 - i]));
        }
      }
    }
  }

  for (size_t i = 0; i < m_vertices.size(); i++)
    m_indices.push_back(i);

  m_error = false;
}