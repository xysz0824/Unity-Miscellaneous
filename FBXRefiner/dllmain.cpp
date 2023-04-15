// dllmain.cpp : 定义 DLL 应用程序的入口点。
#include "pch.h"
#include <string>

enum class Result {
    Success = 0,
    FailedToInitalize,
    NoRootNode,
};

void RemoveGeometryLayerElement(FbxNode* pNode, const char* pMeshName, bool pUV, int pUVStart, int pUVEnd, bool pVertexColor) {
    for (int i = 0; i < pNode->GetNodeAttributeCount(); i++) {
        auto name = pNode->GetName();
        if (pMeshName != nullptr && strcmp(name, pMeshName) != 0)
        {
            continue;
        }
        auto lAttribute = pNode->GetNodeAttributeByIndex(i);
        auto lType = lAttribute->GetAttributeType();
        if (lType == FbxNodeAttribute::eMesh) {
            auto mesh = pNode->GetMesh();
            if (pUV) {
                for (int k = std::min(mesh->GetElementUVCount(), pUVEnd + 1) - 1; k >= pUVStart; --k)
                {
                    auto uv = mesh->GetElementUV(k);
                    mesh->RemoveElementUV(uv);
                }
            }
            if (pVertexColor) {
                for (int k = mesh->GetElementVertexColorCount() - 1; k >= 0; --k)
                {
                    auto vertexColor = mesh->GetElementVertexColor(k);
                    mesh->RemoveElementVertexColor(vertexColor);
                }
            }
        }
    }
    for (int i = 0; i < pNode->GetChildCount(); i++) {
        RemoveGeometryLayerElement(pNode->GetChild(i), pMeshName, pUV, pUVStart, pUVEnd, pVertexColor);
    }
}

DLLAPI Result RemoveGeometryLayerElement(const char* pFileName, const char* pMeshName, bool pUV, int pUVStart, int pUVEnd, bool pVertexColor) {
    FbxManager* lSdkManager = FbxManager::Create();
    FbxIOSettings* ios = FbxIOSettings::Create(lSdkManager, IOSROOT);
    lSdkManager->SetIOSettings(ios);
    FbxScene* lScene = FbxScene::Create(lSdkManager, "rootScene");
    FbxImporter* lImporter = FbxImporter::Create(lSdkManager, "");
    if (!lImporter->Initialize(pFileName, -1, lSdkManager->GetIOSettings())) {
        return Result::FailedToInitalize;
    }
    lImporter->Import(lScene);
    lImporter->Destroy();
    FbxNode* lRootNode = lScene->GetRootNode();
    if (lRootNode) {
        for (int i = 0; i < lRootNode->GetChildCount(); i++)
            RemoveGeometryLayerElement(lRootNode->GetChild(i), pMeshName, pUV, pUVStart, pUVEnd, pVertexColor);
    }
    else {
        return Result::NoRootNode;
    }
    FbxExporter* lExporter = FbxExporter::Create(lSdkManager, "");
    if (!lExporter->Initialize(pFileName, -1, lSdkManager->GetIOSettings())) {
        return Result::FailedToInitalize;
    }
    lExporter->Export(lScene);
    lExporter->Destroy();
    lSdkManager->Destroy();
    return Result::Success;
}