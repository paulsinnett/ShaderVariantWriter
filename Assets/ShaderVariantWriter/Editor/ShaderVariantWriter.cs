using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using PassType = UnityEngine.Rendering.PassType;

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
	public List<Shader> additionalShaders;
	public List<GameObject> additionalPrefabs;
	public List<Material> additionalMaterials;
    public List<Variant> wantedVariants;
    public ShaderVariantCollection output;
}
