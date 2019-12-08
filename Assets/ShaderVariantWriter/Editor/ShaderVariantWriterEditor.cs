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
    Dictionary<Shader, List<string[]>> shaderKeywords = null;
    HashSet<Shader> shaders = null;

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
        shaders = new HashSet<Shader>();
        shaderKeywords = new Dictionary<Shader, List<string[]>>();
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
            AddMaterial(material);
        }
        foreach (GameObject prefab in settings.additionalPrefabs)
        {
            AddObjectShaders(prefab);
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
                    AddObjectShaders(root);
                }
            }
        }
        foreach (Shader shader in shaders)
        {
            foreach (Variant wantedVariant in settings.wantedVariants)
            {
                AddVariations(collection, shader, wantedVariant);
            }
        }
    }

    void AddMaterial(Material material)
    {
        Shader shader = material.shader;
        if (shader != null)
        {
            if (!shaders.Contains(material.shader))
            {
                shaders.Add(shader);
            }

            List<string[]> list = null;
            if (shaderKeywords.ContainsKey(shader))
            {
                list = shaderKeywords[shader];
            }
            else
            {
                list = new List<string[]>();
                shaderKeywords.Add(shader, list);
            }
            list.Add(material.shaderKeywords);
        }
    }

    void AddVariations(
        ShaderVariantCollection collection,
        Shader shader,
        Variant wantedVariant)
    {
        List<string[]> options = new List<string[]>();
        int total = 1;
        Debug.Assert(wantedVariant.keywords.Count > 0);
        foreach (string keywordString in wantedVariant.keywords)
        {
            string[] choices = keywordString.Split(new char[] { ' ' });
            Debug.Assert(choices.Length > 0);
            options.Add(choices);
            total *= choices.Length;
        }
        for (int i = 0; i < total; ++i)
        {
            List<string> keywords = new List<string>();
            int index = i;
            for (int j = 0; j < options.Count; ++j)
            {
                int m = index % options[j].Length;
                index /= options[j].Length;
                if (options[j][m] != "_")
                {
                    keywords.Add(options[j][m]);
                }
            }

            // Debug.LogFormat(
            //     "Variation {0}: {1}",
            //     i,
            //     string.Join(" ", keywords));

            AddKeywords(
                collection,
                shader,
                wantedVariant.pass,
                keywords.ToArray());
        }
    }

    void AddKeywords(
        ShaderVariantCollection collection,
        Shader shader,
        PassType pass,
        string[] keywordList)
    {
        if (CheckKeywords(shader, pass, keywordList))
        {
            List<string> keywords = new List<string>(keywordList);
            keywords.Add("STEREO_MULTIVIEW_ON");

            ShaderVariantCollection.ShaderVariant variant =
                new ShaderVariantCollection.ShaderVariant(
                    shader,
                    pass,
                    new string[] { }
                );

            variant.keywords = keywords.ToArray();
            collection.Add(variant);
        }
    }

    bool CheckKeywords(Shader shader, PassType pass, string[] keywords)
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
            // Debug.LogFormat(
            //     "Shader {0} pass {1} keywords {2} not found",
            //     shader.name,
            //     pass.ToString(),
            //     keywords);
        }
        return valid;
    }


    void AddObjectShaders(GameObject gameObject)
    {
        Renderer[] renderers = null;
        renderers = gameObject.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            foreach (Material material in materials)
            {
                if (material != null)
                {
                    AddMaterial(material);
                }
            }
        }
    }
}
