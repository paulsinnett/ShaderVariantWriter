//#define EXCLUDE_MESH_BAKER

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using PassType = UnityEngine.Rendering.PassType;
using System.IO;
using System.Text;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using System;
using UnityEngine.AI;

[System.Serializable]
public class Variant
{
    public PassType pass;
    public List<string> keywords;
}

[CreateAssetMenu()]
public class ShaderVariantWriter : ScriptableObject
{
    public SceneAsset scene;
    public List<string> additionalHiddenShaders;
    public List<Shader> additionalShaders;
    public List<GameObject> additionalPrefabs;
    public List<Material> additionalMaterials;
    public List<Variant> wantedVariants;
    public ShaderVariantCollection output;

    Dictionary<Shader, List<HashSet<string>>> shaderKeywords = null;
    Dictionary<Shader, List<string>> shaders = null;
    HashSet<Renderer> exclude = null;
    HashSet<string> internalShaders = null;
    StreamWriter file = null;
    HashSet<GameObject> seenObjects = null;

    public void Write()
    {
        Debug.Assert(output != null);
        string filename = "ShaderSources.txt";
        if (scene != null)
        {
            filename = string.Format("{0}.txt", scene.name);
        }
        file = new StreamWriter(filename);
        ShaderVariantCollection collection = output;
        collection.Clear();
        shaders = new Dictionary<Shader, List<string>>();
        exclude = new HashSet<Renderer>();
        seenObjects = new HashSet<GameObject>();
        internalShaders = new HashSet<string>(new string[] {
            "Hidden/InternalErrorShader",
            "Hidden/VideoDecodeAndroid",
            "GUI/Text Shader",
            "Oculus/Texture2D Blit"
        });
        shaderKeywords = new Dictionary<Shader, List<HashSet<string>>>();
        foreach (string shaderName in additionalHiddenShaders)
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
        foreach (Shader shader in additionalShaders)
        {
            AddShader(shader, "additional shaders");
        }
        foreach (Material material in additionalMaterials)
        {
            AddMaterial(material, "additional materials");
        }
        foreach (GameObject prefab in additionalPrefabs)
        {
            AddObjectShaders(prefab, "additional prefabs");
        }
        if (scene != null)
        {
            Scene writeScene =
                EditorSceneManager.OpenScene(
                    AssetDatabase.GetAssetPath(
                        scene));

            if (writeScene.IsValid())
            {
                foreach (GameObject root in writeScene.GetRootGameObjects())
                {
                    AddMeshBakeRenderersToExclusionList(root);
                }
                foreach (GameObject root in writeScene.GetRootGameObjects())
                {
                    AddObjectShaders(root, "scene object");
                }
            }
        }
        foreach (var entry in shaders)
        {
            foreach (Variant wantedVariant in wantedVariants)
            {
                AddVariations(collection, entry.Key, wantedVariant, entry.Value);
            }
        }
        file.Close();
        file = null;
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
            string logLine =
                string.Format(
                    "shader '{0}' from {1}",
                    shader.name,
                    source);

            file.WriteLine(logLine);
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
            // special case overrides
            if (shader.name != "Hidden/VideoDecodeAndroid" &&
                shader.name != "Oculus/Texture2D Blit" &&
                !keywords.Contains("SHADOWS_DEPTH"))
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
            if (internalShaders.Contains(shader.name) && keywords.Length == 0)
            {
                valid = true;
            }
        }
        return valid;
    }

    void AddObjectShaders(GameObject gameObject, string source)
    {
        if (seenObjects.Contains(gameObject)) return;
        seenObjects.Add(gameObject);
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
                    continue;
                }
                Material[] materials = renderer.sharedMaterials;
                foreach (Material material in materials)
                {
                    if (material != null)
                    {
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
            else if (!component.GetType().IsSubclassOf(typeof(Transform)) &&
                component.GetType() != typeof(Transform) &&
                component.GetType() != typeof(MeshFilter) &&
                !component.GetType().IsSubclassOf(typeof(Collider)) &&
                component.GetType() != typeof(NavMeshObstacle))
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
                if (type == typeof(Material))
                {
                    var material = (Material)property.objectReferenceValue;
                    AddMaterial(
                        material,
                        string.Format(
                            "{0}.{1} material '{2}'",
                            source,
                            property.propertyPath,
                            material.name));
                }
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
