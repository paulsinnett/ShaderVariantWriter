//#define EXCLUDE_MESH_BAKER

using System;
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
    Dictionary<Shader, List<HashSet<string>>> shaderKeywords = null;
    HashSet<Shader> shaders = null;
    HashSet<Renderer> exclude = null;
    HashSet<string> internalShaders = null;

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
        exclude = new HashSet<Renderer>();
        internalShaders = new HashSet<string>(new string[] {
            "Hidden/InternalErrorShader",
            "Hidden/VideoDecodeAndroid"
        });
        shaderKeywords = new Dictionary<Shader, List<HashSet<string>>>();
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
                    AddMeshBakeRenderersToExclusionList(root);
                }
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

    void AddMeshBakeRenderersToExclusionList(GameObject root)
    {
#if EXCLUDE_MESH_BAKER
        MB3_TextureBaker[] bakers =
            root.GetComponentsInChildren<MB3_TextureBaker>();

        foreach (var baker in bakers)
        {
            if (baker.objsToMesh != null && baker.objsToMesh.Count > 0)
            {
                foreach (GameObject baked in baker.objsToMesh)
                {
                    if (baked != null)
                    {
                        Renderer renderer = baked.GetComponent<Renderer>();

                        if (renderer != null)
                        {
                            exclude.Add(renderer);
                        }
                    }
                }
            }
        }
#endif
    }

    void AddMaterial(Material material)
    {
        Shader shader = material.shader;
        if (shader != null)
        {
            if (!shaders.Contains(shader))
            {
                shaders.Add(shader);
            }

            if (material.shaderKeywords.Length > 0)
            {
                List<HashSet<string>> list = null;
                if (shaderKeywords.ContainsKey(shader))
                {
                    list = shaderKeywords[shader];
                }
                else
                {
                    list = new List<HashSet<string>>();
                    shaderKeywords.Add(shader, list);
                }
                var newSet = new HashSet<string>(material.shaderKeywords);
                bool exists = false;
                foreach (var set in list)
                {
                    if (newSet.SetEquals(set))
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                {
                    // Debug.LogFormat(
                    //     "adding keywords {0} from material {1}",
                    //     string.Join(" ", newSet),
                    //     material.name);

                    list.Add(newSet);
                }
            }
        }
    }

    void AddVariations(
        ShaderVariantCollection collection,
        Shader shader,
        Variant wantedVariant)
    {
        List<string[]> options = new List<string[]>();
        int total = 1;
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
                    string[] subset = options[j][m].Split(new char[] { '+' });
                    keywords.AddRange(subset);
                }
            }

            // Debug.LogFormat(
            //     "Variation {0}: {1}",
            //     i,
            //     string.Join(" ", keywords));

            if (shaderKeywords.ContainsKey(shader))
            {
                foreach (var list in shaderKeywords[shader])
                {
                    List<string> materialKeywords = new List<string>(keywords);
                    materialKeywords.AddRange(list);

                    AddKeywords(
                        collection,
                        shader,
                        wantedVariant.pass,
                        materialKeywords.ToArray());
                }
            }
            else
            {
                AddKeywords(
                    collection,
                    shader,
                    wantedVariant.pass,
                    keywords.ToArray());
            }
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
            // special case override
            if (shader.name != "Hidden/VideoDecodeAndroid")
            {
                keywords.Add("STEREO_MULTIVIEW_ON");
            }

            ShaderVariantCollection.ShaderVariant variant =
                new ShaderVariantCollection.ShaderVariant();

            variant.shader = shader;
            variant.passType = pass;
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
            //     "Shader {0} pass {1} keywords '{2}' not found",
            //     shader.name,
            //     pass.ToString(),
            //     string.Join(" ", keywords));

            if (internalShaders.Contains(shader.name) && keywords.Length == 0)
            {
                valid = true;
            }
        }
        return valid;
    }

    void AddObjectShaders(GameObject gameObject)
    {
        Renderer[] renderers = null;
        renderers = gameObject.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (exclude.Contains(renderer))
            {
                continue;
            }
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