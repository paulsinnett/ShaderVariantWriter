using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PassType = UnityEngine.Rendering.PassType;

[CustomEditor(typeof(ShaderVariantWriter))]
public class ShaderVariantWriterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Write variant collection"))
        {
            Write();
            AssetDatabase.SaveAssets();
        }
    }

    void Write()
    {
        ShaderVariantWriter settings = target as ShaderVariantWriter;
        Debug.Assert(settings.output != null);
        ShaderVariantCollection collection = settings.output;
        collection.Clear();
        HashSet<Shader> shaders = new HashSet<Shader>();
        foreach (string shaderName in settings.additionalHiddenShaders)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogErrorFormat(
                    "Could not find shader '{0}'",
                    shaderName);
            }
            else if (!shaders.Contains(shader))
            {
                shaders.Add(shader);
            }
        }
        foreach (Shader shader in settings.additionalShaders)
        {
            if (!shaders.Contains(shader))
            {
                shaders.Add(shader);
            }
        }
        foreach (Material material in settings.additionalMaterials)
        {
            Shader shader = material.shader;
            if (shader != null && !shaders.Contains(shader))
            {
                shaders.Add(shader);
            }
        }
        foreach (GameObject prefab in settings.additionalPrefabs)
        {
            AddObjectShaders(prefab, shaders);
        }
        if (settings.scene != null)
        {
            Scene scene =
                EditorSceneManager.OpenScene(
                    AssetDatabase.GetAssetPath(
                        settings.scene));

            if (scene.IsValid())
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    AddObjectShaders(root, shaders);
                }
            }
        }
        foreach (Shader shader in shaders)
        {
            foreach (Variant wantedVariant in settings.wantedVariants)
            {
                AddKeywords(collection, shader, wantedVariant);
            }
        }
    }

    void AddKeywords(
        ShaderVariantCollection collection,
        Shader shader,
        Variant wantedVariant)
    {
        foreach (string keywordString in wantedVariant.keywords)
        {
            string [] keywordList = keywordString.Split(new char [] {' '});
            if (CheckKeywords(shader, wantedVariant.pass, keywordList))
            {
                List<string> keywords = new List<string>(keywordList);
                keywords.Add("STEREO_MULTIVIEW_ON");

                ShaderVariantCollection.ShaderVariant variant =
                    new ShaderVariantCollection.ShaderVariant(
                        shader,
                        wantedVariant.pass,
                        new string [] {}
                    );

                variant.keywords = keywords.ToArray();
                collection.Add(variant);

                keywords.Add("UNITY_SINGLE_PASS_STEREO");
                variant.keywords = keywords.ToArray();
                collection.Add(variant);
            }
        }
    }

    bool CheckKeywords(Shader shader, PassType pass, string [] keywords)
    {
        bool valid = false;
        try
        {
            ShaderVariantCollection.ShaderVariant variant =
                new ShaderVariantCollection.ShaderVariant(
                    shader,
                    pass,
                    keywords
                );
            
            valid = true;
        }
        catch (System.ArgumentException)
        {
            Debug.LogFormat(
                "Shader {0} pass {1} keywords {2} not found",
                shader.name,
                pass.ToString(),
                keywords);
        }
        return valid;
    }


    void AddObjectShaders(GameObject gameObject, HashSet<Shader> shaders)
    {
        Renderer [] renderers = null;
        renderers = gameObject.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            Material [] materials = renderer.sharedMaterials;
            foreach (Material material in materials)
            {
                if (material != null)
                {
                    Shader shader = material.shader;
                    if (shader != null && !shaders.Contains(material.shader))
                    {
                        shaders.Add(shader);
                    }
                }
            }
        }
    }
}
