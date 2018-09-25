using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ReflectCamera : MonoBehaviour
{
    public enum ReflectType
    {
        ReflectLayer,
        ExceptReflectLayer
    }
    RenderTexture renderTexture;
    Camera reflectCamera;
    int realtimeReflectionID;
    public float CameraDepth;
    public GameObject Plane;
    public ReflectType Type;
    public Camera SpecificCamera;
    void Start()
    {
        realtimeReflectionID = Shader.PropertyToID("_RealtimeReflection");
        renderTexture = new RenderTexture(Screen.width / 2, Screen.height / 2, 16, RenderTextureFormat.ARGB32);
        reflectCamera = new GameObject("Reflect Camera").AddComponent<Camera>();
        reflectCamera.transform.SetParent(transform);
        reflectCamera.gameObject.AddComponent<ReflectCameraBlur>();
        if (Type == ReflectType.ReflectLayer)
        {
            reflectCamera.cullingMask = LayerMask.GetMask("Reflect");
        }
        else
        {
            reflectCamera.cullingMask = ~LayerMask.GetMask("Reflect");
        }
        reflectCamera.clearFlags = CameraClearFlags.Skybox;
        reflectCamera.useOcclusionCulling = false;
        reflectCamera.depth = CameraDepth;
        reflectCamera.allowHDR = false;
        reflectCamera.allowMSAA = false;
        reflectCamera.targetTexture = renderTexture;
        var skybox = GetComponent<AttachSkyBox>();
        if (skybox != null)
        {
            reflectCamera.gameObject.AddComponent<Skybox>().material = skybox.skyBox;
        }
    }
    void OnDestroy()
    {
        if (reflectCamera != null) Destroy(reflectCamera.gameObject);
        renderTexture.Release();
    }
    void LateUpdate()
    {
        if (renderTexture.width != Screen.width / 2 || renderTexture.height != Screen.height / 2)
        {
            reflectCamera.targetTexture = null;
            DestroyImmediate(renderTexture);
            renderTexture = new RenderTexture(Screen.width / 2, Screen.height / 2, 16, RenderTextureFormat.ARGB32);
            renderTexture.useMipMap = true;
            reflectCamera.targetTexture = renderTexture;
        }
        var camera = SpecificCamera == null ? Camera.main : SpecificCamera;
        reflectCamera.fieldOfView = camera.fieldOfView;
        reflectCamera.farClipPlane = camera.farClipPlane;
        reflectCamera.nearClipPlane = camera.nearClipPlane;
        var planeNormal = Vector3.up;
        var planePoint = Plane.transform.position;
        var axis = Vector3.ProjectOnPlane(camera.transform.forward, planeNormal);
        reflectCamera.transform.rotation = Quaternion.AngleAxis(180, axis) * camera.transform.rotation;
        var distance = Vector3.Dot(planePoint - camera.transform.position, planeNormal) / planeNormal.magnitude;
        reflectCamera.transform.position = camera.transform.position + 2 * distance * planeNormal;
        Shader.SetGlobalTexture(realtimeReflectionID, renderTexture);
    }
}
