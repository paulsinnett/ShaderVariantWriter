using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CreateAssetMenu()]
public class ShaderVariantWriter : ScriptableObject
{
	public SceneAsset scene;
	public List<Shader> additionalShaders;
	public List<GameObject> additionalPrefabs;
	public List<Material> additionalMaterials;
}
