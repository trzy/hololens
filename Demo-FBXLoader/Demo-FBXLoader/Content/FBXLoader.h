#pragma once
#ifndef INCLUDED_FBXLOADER_H
#define INCLUDED_FBXLOADER_H

#include "Common\DirectXHelper.h"
#include <fbxsdk.h>

/*
 * FBX SDK DLL deployment in UWP apps:
 * -----------------------------------
 *
 * These instructions pertain to v2016.1.2 of the FBX SDK.
 *
 * 1. Do not define FBXSDK_DLL. Although Autodesk claims this is required for
 *    DLLs, it appears to blow up badly here.
 * 2. Place the appropriate libfbxsdk.dll file into the project root (e.g., the
 *    same folder as Package.appxmanifest. It will not work if placed anywhere
 *    else, evidently.
 * 3. In Solution Explorer, right click on the project, Add -> Existing Item...
 * 4. Right click on the newly added file, select "Properties". Set "Content"
 *    to "Yes". "Excluded From Build" will be blank and "Item Type" will be
 *    "Does not participate in build".
 * 5. Under Configuration Properties -> Linker -> Input -> Additional 
 *    Dependencies, add libfbxsdk.lib, which is the dynamic version of the
 *    library. For the static version, which does not require the DLL to be
 *    bundled, use libfbxsdk-md.lib.
 *
 * Unresolved issues:
 *
 * 1. How to place the DLL file into an arbitrary sub-folder?
 */

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