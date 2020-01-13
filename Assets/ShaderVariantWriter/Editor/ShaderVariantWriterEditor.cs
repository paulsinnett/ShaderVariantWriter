using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(ShaderVariantWriter))]
public class ShaderVariantWriterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Write variant collection"))
        {
            ShaderVariantWriter settings = target as ShaderVariantWriter;
            settings.Write();
            AssetDatabase.SaveAssets();
        }
    }
}
