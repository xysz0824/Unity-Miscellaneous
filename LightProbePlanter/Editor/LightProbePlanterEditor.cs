using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(LightProbePlanter))]
public class LightProbePlanterEditor : Editor
{
    Vector3[] Plant(Transform transform, Vector3 center, Vector3 size, float space, float layer, float height)
    {
        var obj = target as LightProbePlanter;
        float xMax = size.x * 0.5f;
        float xMin = -xMax;
        float yMax = size.y * 0.5f;
        float yMin = -yMax;
        float zMax = size.z * 0.5f;
        float zMin = -zMax;
        var result = new List<Vector3>();
        var spotLights = Light.GetLights(LightType.Spot, int.MaxValue);
        var pointLights = Light.GetLights(LightType.Point, int.MaxValue);
        for (float x = xMin; x <= xMax; x += space)
        {
            for (float z = zMin; z <= zMax; z += space)
            {
                Vector3 localPos = new Vector3();
                Vector3 worldPos = new Vector3();
                float increment = (yMax - yMin) / layer;
                for (float i = 0; i < layer; i++)
                {
                    float y = yMin + increment * i * height;
                    localPos = new Vector3(x, y, z) + center;
                    worldPos = transform.TransformPoint(localPos);
                    Vector3 testPos = transform.TransformPoint(new Vector3(x, yMax - space, z) + center);
                    RaycastHit hit;
                    if (Physics.Raycast(testPos, -Vector3.up, out hit, yMax - space - y))
                    {
                        if (Vector3.Distance(worldPos, hit.point) <= space * 0.1f)
                        {
                            worldPos = hit.point + hit.normal * 0.1f;
                            localPos = transform.InverseTransformPoint(worldPos);
                            result.Add(localPos);
                        }
                        continue;
                    }
                    Vector3[] cross = new Vector3[4];
                    cross[0] = new Vector3(1, 0, 0);
                    cross[1] = new Vector3(0, 0, 1);
                    cross[2] = -cross[0];
                    cross[3] = -cross[1];
                    float distance = float.MaxValue;
                    for (int k = 0; k < 4; ++k)
                    {
                        if (Physics.Raycast(worldPos, cross[k], out hit, space) && hit.distance <= distance)
                        {
                            distance = hit.distance;
                            worldPos = hit.point + hit.normal * 0.1f;
                            localPos = transform.InverseTransformPoint(worldPos);
                            localPos.y = y + center.y;
                        }
                    }
                    bool tooClose = false;
                    foreach (var pointLight in pointLights)
                    {
                        float ideal = Mathf.Log(pointLight.intensity + 1.0f);
                        Vector3 dist = worldPos - pointLight.transform.position;
                        if (dist.magnitude < ideal)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (tooClose) continue;
                    foreach (var spotLight in spotLights)
                    {
                        float ideal = 2.0f * Mathf.Log(spotLight.intensity + 5.0f) - 3.0f;
                        Vector3 dist = worldPos - spotLight.transform.position;
                        float angle = Vector3.Angle(spotLight.transform.forward, dist);
                        if (angle <= (spotLight.innerSpotAngle + spotLight.spotAngle) * 0.5f && dist.magnitude < ideal)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (!tooClose) result.Add(localPos);
                }
            }
        }
        return result.ToArray();
    }
    public override void OnInspectorGUI()
    {
        var obj = target as LightProbePlanter;
        var collider = obj.GetComponent<BoxCollider>();
        var lightProbeGroup = obj.GetComponent<LightProbeGroup>();
        EditorGUILayout.BeginVertical();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("space"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("layer"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("height"));
        EditorGUILayout.EndVertical();
        if (GUI.changed)
        {
            serializedObject.ApplyModifiedProperties();
        }
        if (GUILayout.Button("Clear"))
        {
            lightProbeGroup.probePositions = new Vector3[0];
        }
        if (GUILayout.Button("Plant"))
        {
            lightProbeGroup.probePositions = Plant(obj.transform, collider.center, collider.size, obj.space, obj.layer, obj.height);
        }
    }

    void OnSceneGUI()
    {
        var obj = target as LightProbePlanter;
        var collider = obj.GetComponent<BoxCollider>();
        float xMax = collider.size.x * 0.5f;
        float xMin = -xMax;
        float yMax = collider.size.y * 0.5f;
        float yMin = -yMax;
        float zMax = collider.size.z * 0.5f;
        float zMin = -zMax;
        float y = yMin + (yMax - yMin) * obj.height;
        Vector3 a = obj.transform.TransformPoint(new Vector3(xMin, y, zMin) + collider.center);
        Vector3 b = obj.transform.TransformPoint(new Vector3(xMax, y, zMin) + collider.center);
        Vector3 c = obj.transform.TransformPoint(new Vector3(xMax, y, zMax) + collider.center);
        Vector3 d = obj.transform.TransformPoint(new Vector3(xMin, y, zMax) + collider.center);
        Handles.DrawLine(a, b);
        Handles.DrawLine(b, c);
        Handles.DrawLine(c, d);
        Handles.DrawLine(d, a);
    }
}
