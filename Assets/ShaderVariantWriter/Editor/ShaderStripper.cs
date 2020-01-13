using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEngine.ShaderVariantCollection;

[CreateAssetMenu()]
public class ShaderStripper : ScriptableObject
{
    public bool enabled = true;
    public List<ShaderVariantCollection> keepShaders;

    public class Stripper : IPreprocessShaders
    {
        public int callbackOrder { get { return 1; } }

        public void OnProcessShader(
            Shader shader,
            ShaderSnippetData snippet,
            IList<ShaderCompilerData> data)
        {
            List<ShaderVariantCollection> keepShaders = null;
            foreach (var guid in AssetDatabase.FindAssets("t:ShaderStripper"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ShaderStripper stripper = AssetDatabase.LoadAssetAtPath<ShaderStripper>(path);
                if (stripper != null)
                {
                    if (!stripper.enabled) return;
                    keepShaders = stripper.keepShaders;
                }
            }

            Debug.Assert(keepShaders != null);
            if (keepShaders.Count == 0)
            {
                return;
            }
            for (int i = data.Count - 1; i >= 0; --i)
            {
                ShaderCompilerData variant = data[i];
                List<string> keywords = new List<string>();
                var variantKeywords = variant.shaderKeywordSet.GetShaderKeywords();
                foreach (ShaderKeyword keyword in variantKeywords)
                {
                    string k = keyword.GetKeywordName();
                    keywords.Add(k);
                }

                bool match = false;
                try
                {
                    ShaderVariant currentVariant = new ShaderVariant();
                    currentVariant.shader = shader;
                    currentVariant.passType = snippet.passType;
                    currentVariant.keywords = keywords.ToArray();

                    foreach (ShaderVariantCollection collection in keepShaders)
                    {
                        if (collection.Contains(currentVariant))
                        {
                            match = true;
                            break;
                        }
                    }
                }
                catch (System.ArgumentException)
                {
                    // not valid, continue...
                }

                Debug.LogFormat(
                    "{0} shader {1} pass {2} keywords {3}",
                    match ? "Keep" : "Strip",
                    shader.name,
                    snippet.passType,
                    string.Join(" ", keywords));

                if (!match)
                {
                    data.RemoveAt(i);
                }
            }
        }
    }
}
