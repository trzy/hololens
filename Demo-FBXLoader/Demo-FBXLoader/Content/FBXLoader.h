#pragma once
#ifndef INCLUDED_FBXLOADER_H
#define INCLUDED_FBXLOADER_H

#include "Common\DirectXHelper.h"
#include <fbxsdk.h>

class FBXLoader
{
public:
  FBXLoader(const char *filename);

  bool Failed() const
  {
    return m_error;
  }

  const std::vector<DirectX::XMFLOAT3> &GetVertices() const
  {
    return m_vertices;
  }

  const std::vector<unsigned short> &GetIndices() const
  {
    return m_indices;
  }

private:
  bool m_error = true;
  FbxManager *m_fbx_sdk_manager = nullptr;
  std::vector<DirectX::XMFLOAT3> m_vertices;
  std::vector<unsigned short> m_indices;
};

#endif  // INCLUDED_FBXLOADER_H