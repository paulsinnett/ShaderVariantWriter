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
                    keepShaders = stripper.keepShaders;
                }
            }

            if (keepShaders == null || keepShaders.Count == 0)
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
                    keywords.Add(keyword.GetKeywordName());
                }

                bool match = false;
                try
                {
                    ShaderVariant currentVariant =
                        new ShaderVariant(
                            shader,
                            snippet.passType,
                            keywords.ToArray());

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
                    // instance not supported, carry on...
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
