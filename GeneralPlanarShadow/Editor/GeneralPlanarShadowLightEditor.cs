using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(GeneralPlanarShadowLight))]
public class GeneralPlanarShadowLightEditor : Editor
{
    internal static readonly Vector3[] directionalLightHandlesRayPositions = new Vector3[8]
    {
      new Vector3(1f, 0.0f, 0.0f),
      new Vector3(-1f, 0.0f, 0.0f),
      new Vector3(0.0f, 1f, 0.0f),
      new Vector3(0.0f, -1f, 0.0f),
      new Vector3(1f, 1f, 0.0f).normalized,
      new Vector3(1f, -1f, 0.0f).normalized,
      new Vector3(-1f, 1f, 0.0f).normalized,
      new Vector3(-1f, -1f, 0.0f).normalized
    };

    internal static float SizeSlider(Vector3 p, Vector3 d, float r)
    {
        Vector3 position = p + d * r;
        float handleSize = HandleUtility.GetHandleSize(position);
        bool changed = GUI.changed;
        GUI.changed = false;
        Vector3 vector3 = Handles.Slider(position, d, handleSize * 0.03f, new Handles.CapFunction(Handles.DotHandleCap), 0.0f);
        if (GUI.changed)
            r = Vector3.Dot(vector3 - p, d);
        GUI.changed |= changed;
        return r;
    }

    internal static Vector2 ConeHandle(Quaternion rotation, Vector3 position, Vector2 angleAndRange, float angleScale, float rangeScale, bool handlesOnly)
    {
        float x = angleAndRange.x;
        float y = angleAndRange.y;
        float r1 = y * rangeScale;
        Vector3 vector3 = rotation * Vector3.forward;
        Vector3 d1 = rotation * Vector3.up;
        Vector3 d2 = rotation * Vector3.right;
        bool changed1 = GUI.changed;
        GUI.changed = false;
        float num = SizeSlider(position, vector3, r1);
        if (GUI.changed)
            y = Mathf.Max(0.0f, num / rangeScale);
        GUI.changed |= changed1;
        bool changed2 = GUI.changed;
        GUI.changed = false;
        float r2 = num * Mathf.Tan((float)(Mathf.PI / 180.0 * (double)x / 2.0)) * angleScale;
        float r3 = SizeSlider(position + vector3 * num, d1, r2);
        float r4 = SizeSlider(position + vector3 * num, -d1, r3);
        float r5 = SizeSlider(position + vector3 * num, d2, r4);
        float radius = SizeSlider(position + vector3 * num, -d2, r5);
        if (GUI.changed)
            x = Mathf.Clamp((float)(57.2957801818848 * (double)Mathf.Atan(radius / (num * angleScale)) * 2.0), 0.0f, 179f);
        GUI.changed |= changed2;
        if (!handlesOnly)
        {
            Handles.DrawLine(position, position + vector3 * num + d1 * radius);
            Handles.DrawLine(position, position + vector3 * num - d1 * radius);
            Handles.DrawLine(position, position + vector3 * num + d2 * radius);
            Handles.DrawLine(position, position + vector3 * num - d2 * radius);
            Handles.DrawWireDisc(position + num * vector3, vector3, radius);
        }
        return new Vector2(x, y);
    }

    void OnSceneGUI()
    {
        var shadowLight = target as GeneralPlanarShadowLight;
        if (shadowLight.GetComponent<Light>() != null) return;
        Color color = Handles.color;
        float range = shadowLight.range;
        switch (shadowLight.type)
        {
            case GeneralPlanarShadowLight.LightType.Spot:
                Transform transform = shadowLight.transform;
                Vector3 position1 = transform.position;
                Vector3 center = position1 + transform.forward * shadowLight.range;
                float radius1 = shadowLight.range * Mathf.Tan((float)(Mathf.PI / 180.0 * (double)shadowLight.outerSpotAngle / 2.0));
                Handles.DrawLine(position1, center + transform.up * radius1);
                Handles.DrawLine(position1, center - transform.up * radius1);
                Handles.DrawLine(position1, center + transform.right * radius1);
                Handles.DrawLine(position1, center - transform.right * radius1);
                Handles.DrawWireDisc(center, transform.forward, radius1);
                Vector2 angleAndRange = new Vector2(shadowLight.outerSpotAngle, shadowLight.range);
                Vector2 vector2_1 = ConeHandle(shadowLight.transform.rotation, shadowLight.transform.position, angleAndRange, 1f, 1f, true);
                if (GUI.changed)
                {
                    Undo.RecordObject((UnityEngine.Object)shadowLight, "Adjust Spot Light");
                    shadowLight.outerSpotAngle = vector2_1.x;
                    shadowLight.range = Mathf.Max(vector2_1.y, 0.01f);
                    break;
                }
                break;
            case GeneralPlanarShadowLight.LightType.Directional:
                Vector3 position2 = shadowLight.transform.position;
                float handleSize;
                using (new Handles.DrawingScope(Matrix4x4.identity))
                    handleSize = HandleUtility.GetHandleSize(position2);
                float radius2 = handleSize * 0.2f;
                using (new Handles.DrawingScope(Matrix4x4.TRS(position2, shadowLight.transform.rotation, Vector3.one)))
                {
                    Handles.DrawWireDisc(Vector3.zero, Vector3.forward, radius2);
                    foreach (Vector3 handlesRayPosition in directionalLightHandlesRayPositions)
                    {
                        Vector3 p1 = handlesRayPosition * radius2;
                        Handles.DrawLine(p1, p1 + new Vector3(0.0f, 0.0f, handleSize));
                    }
                    break;
                }
            case GeneralPlanarShadowLight.LightType.Point:
                float num = Handles.RadiusHandle(Quaternion.identity, shadowLight.transform.position, range);
                if (GUI.changed)
                {
                    Undo.RecordObject((UnityEngine.Object)shadowLight, "Adjust Point Light");
                    shadowLight.range = num;
                    break;
                }
                break;
        }
    }

    public override void OnInspectorGUI()
    {
        var shadowLight = target as GeneralPlanarShadowLight;
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.BeginVertical();
        var shadowStrength = serializedObject.FindProperty("shadowStrength");
        EditorGUILayout.PropertyField(shadowStrength);
        shadowStrength.floatValue = Mathf.Max(0, shadowStrength.floatValue);
        if (shadowLight.GetComponent<Light>() == null)
        {
            var type = serializedObject.FindProperty("type");
            EditorGUILayout.PropertyField(type);
            if (type.enumValueIndex != (int)LightType.Directional)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("range"));
                if (type.enumValueIndex == (int)LightType.Spot)
                {
                    var innerSpotAngle = serializedObject.FindProperty("innerSpotAngle");
                    var outerSpotAngle = serializedObject.FindProperty("outerSpotAngle");
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.PropertyField(innerSpotAngle);
                    EditorGUILayout.PropertyField(outerSpotAngle);
                    EditorGUI.EndDisabledGroup();
                    var innerSpotAngleValue = innerSpotAngle.floatValue;
                    var outerSpotAngleValue = outerSpotAngle.floatValue;
                    EditorGUILayout.MinMaxSlider("Inner / Outer Spot Angle", ref innerSpotAngleValue, ref outerSpotAngleValue, 0, 179);
                    innerSpotAngle.floatValue = innerSpotAngleValue;
                    outerSpotAngle.floatValue = outerSpotAngleValue;
                }
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("color"));
            var intensity = serializedObject.FindProperty("intensity");
            EditorGUILayout.PropertyField(intensity);
            intensity.floatValue = Mathf.Max(0, intensity.floatValue);
        }
        EditorGUILayout.EndVertical();
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
        }
    }
}
