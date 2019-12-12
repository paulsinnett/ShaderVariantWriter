//#define EXCLUDE_MESH_BAKER

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PassType = UnityEngine.Rendering.PassType;

[CustomEditor(typeof(ShaderVariantWriter))]
public class ShaderVariantWriterEditor : Editor
{
    Dictionary<Shader, List<HashSet<string>>> shaderKeywords = null;
    Dictionary<Shader, List<string>> shaders = null;
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
        if (GUILayout.Button("Output materials"))
        {
            for (int i = 0; i < SceneManager.sceneCount; ++i)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid())
                {
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        foreach (var renderer in
                            root.GetComponentsInChildren<Renderer>())
                        {
                            foreach (var material in
                                renderer.sharedMaterials)
                            {
                                if (material == null) continue;

                                var keywords = new List<string>();
                                if (material.shaderKeywords != null)
                                {
                                    keywords.AddRange(
                                        material.shaderKeywords);
                                }

                                if (renderer.shadowCastingMode ==
                                        ShadowCastingMode.On ||
                                    renderer.shadowCastingMode ==
                                        ShadowCastingMode.TwoSided)
                                {
                                    Debug.LogFormat(
                                        renderer.gameObject,
                                        "{0} uses shadow casting mode {1} shader {2} '{3}'",
                                        renderer.name,
                                        renderer.shadowCastingMode,
                                        material.shader.name,
                                        string.Join(" ", keywords));
                                }
                            }
                        }
                    }
                }

            }
        }
    }

    void Write()
    {
        ShaderVariantWriter settings = target as ShaderVariantWriter;
        Debug.Assert(settings.output != null);
        ShaderVariantCollection collection = settings.output;
        collection.Clear();
        shaders = new Dictionary<Shader, List<string>>();
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
            else
            {
                AddShader(shader, "additional hidden shaders");
            }
        }
        foreach (Shader shader in settings.additionalShaders)
        {
            AddShader(shader, "additional shaders");
        }
        foreach (Material material in settings.additionalMaterials)
        {
            AddMaterial(material, "additional materials");
        }
        foreach (GameObject prefab in settings.additionalPrefabs)
        {
            AddObjectShaders(prefab, "additional prefabs");
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
                    AddObjectShaders(root, "scene object");
                }
            }
        }
        foreach (var entry in shaders)
        {
            foreach (Variant wantedVariant in settings.wantedVariants)
            {
                AddVariations(collection, entry.Key, wantedVariant, entry.Value);
            }
        }
    }

    void AddShader(Shader shader, string source)
    {
        if (shaders.ContainsKey(shader))
        {
            shaders[shader].Add(source);
        }
        else
        {
            List<string> sources = new List<string>();
            sources.Add(source);
            shaders.Add(shader, sources);
        }
    }

    void AddMeshBakeRenderersToExclusionList(GameObject root)
    {
#if EXCLUDE_MESH_BAKER
        MB3_TextureBaker[] bakers =
            root.GetComponentsInChildren<MB3_TextureBaker>(true);

        foreach (var baker in bakers)
        {
            if (baker.objsToMesh != null && baker.objsToMesh.Count > 0)
            {
                foreach (GameObject baked in baker.objsToMesh)
                {
                    if (baked != null)
                    {
                        Renderer renderer = baked.GetComponent<Renderer>();
                        exclude.Add(renderer);
                    }
                }
            }
        }
#endif
    }

    void AddMaterial(Material material, string source)
    {
        Shader shader = material.shader;
        if (shader != null)
        {
            AddShader(shader, source);
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
                    list.Add(new HashSet<string>()); // always add the empty set
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

    string ObjectPath(GameObject gameObject)
    {
        StringBuilder path = new StringBuilder();
        path.Append(gameObject.name);
        PrependParent(gameObject.transform, path);
        return path.ToString();
    }

    void PrependParent(Transform transform, StringBuilder path)
    {
        if (transform.parent != null)
        {
            path.Insert(
                0,
                string.Format(
                    "{0}/",
                    transform.parent.name,
                    path));

            PrependParent(transform.parent, path);
        }
    }



    void AddVariations(
        ShaderVariantCollection collection,
        Shader shader,
        Variant wantedVariant,
        List<string> sources)
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
        foreach (var source in sources)
        {
            Debug.LogFormat(
                "shader '{0}' from {1}",
                shader.name,
                source);
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
            if (shader.name != "Hidden/VideoDecodeAndroid" && !keywords.Contains("SHADOWS_DEPTH"))
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

    void AddObjectShaders(GameObject gameObject, string source)
    {
        var components = gameObject.GetComponentsInChildren<Component>(true);
        foreach (var component in components)
        {
            if (component == null) continue;
            
            var renderer = component as Renderer;
            var image = component as Image;
            if (renderer != null)
            {
                if (exclude.Contains(renderer))
                {
                    // if (renderer.gameObject.name == "LockGamePacmanCasing")
                    // {
                    //     Debug.LogFormat(
                    //         renderer.gameObject,
                    //         "excluding object {0}",
                    //         renderer.name);
                    // }
                    continue;
                }
                Material[] materials = renderer.sharedMaterials;
                foreach (Material material in materials)
                {
                    if (material != null)
                    {
                        // if (material.shader != null &&
                        //     material.shader.name.StartsWith("Who/"))
                        // {
                        //     Debug.LogFormat(
                        //         renderer.gameObject,
                        //         "adding shader {0} from material {1}",
                        //         material.shader.name,
                        //         material.name);
                        // }
                        // if (gameObject.name == "LockGamePacmanCasing")
                        // {
                        //     Debug.LogFormat(
                        //         renderer.gameObject,
                        //         "adding material {0} from object {1}",
                        //         material.name,
                        //         renderer.name);
                        // }
                        AddMaterial(
                            material,
                            string.Format(
                                "{0} using material '{1}'",
                                string.Format(
                                    "{0} '{1}'",
                                    source,
                                    ObjectPath(renderer.gameObject)),
                                material.name));
                    }
                }
            }
            else if (image != null)
            {
                Debug.LogFormat(
                    gameObject,
                    "adding material {0} from object {1}",
                    image.material.name,
                    gameObject.name);

                AddMaterial(
                    image.material,
                    string.Format(
                        "{0} using material '{1}'",
                        string.Format(
                            "{0} '{1}'",
                            source,
                            ObjectPath(image.gameObject)),
                        image.material.name));
            }
            else if (!component.GetType().IsSubclassOf(typeof(Transform))
                && component.GetType() != typeof(Transform))
            {
                AddNonSceneObjects(
                    component,
                    string.Format(
                        "{0} '{1}' component '{2}'",
                        source,
                        ObjectPath(component.gameObject),
                        component.GetType().ToString()));
            }
        }
    }

    void AddNonSceneObjects(
        Component sourceComponent,
        string source)
    {
        var transform = sourceComponent.transform;
        var serializedObject = new SerializedObject(sourceComponent);
        var property = serializedObject.GetIterator();
        do
        {
            if (property.name != "m_GameObject" &&
                property.propertyType ==
                    SerializedPropertyType.ObjectReference &&
                property.objectReferenceValue != null)
            {
                Type type = property.objectReferenceValue.GetType();

                int reference =
                    property.objectReferenceValue.GetInstanceID();

                GameObject referencedObject = null;
                if (type == typeof(GameObject))
                {
                    referencedObject =
                        (GameObject)property.objectReferenceValue;
                }
                else if (type.IsSubclassOf(typeof(Component)))
                {
                    var component = (Component)property.objectReferenceValue;
                    reference = component.gameObject.GetInstanceID();
                    referencedObject = component.gameObject;
                }
                if (referencedObject != null &&
                    !referencedObject.scene.IsValid() &&
                    !transform.IsChildOf(referencedObject.transform))
                {
                    AddObjectShaders(
                        referencedObject,
                        string.Format("{0} referencing",
                            source));
                }
            }
        }
        while (property.Next(true));
    }
}
