using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace GFxShaderMaker;

public class ShaderGroup
{
	public string ID;

	public List<ShaderFeature> Features;

	public List<ShaderPermutation> Permutations;

	public Dictionary<string, uint> FeatureFlavorFlags;

	public void ReadFromXml(XmlElement root)
	{
		Features = new List<ShaderFeature>();
		ID = root.Attributes.GetNamedItem("id").Value;
		foreach (XmlElement item in root.GetElementsByTagName("ShaderFeature"))
		{
			ShaderFeature shaderFeature = new ShaderFeature();
			shaderFeature.ReadFromXml(item);
			Features.Add(shaderFeature);
		}
	}

	private void DetermineFeatureFlags(Dictionary<string, uint> globalFeatureFlags)
	{
		FeatureFlavorFlags = new Dictionary<string, uint>();
		foreach (ShaderFeature feature in Features)
		{
			foreach (ShaderFeatureFlavor flavor in feature.Flavors)
			{
				if (flavor.ID != ShaderFeatureFlavor.EmptyID)
				{
					if (feature.Flavors.Count == 1 || !globalFeatureFlags.TryGetValue(flavor.ID, out var value))
					{
						value = 0u;
					}
					FeatureFlavorFlags.Add(flavor.ID, value);
				}
			}
		}
	}

	public void CreatePermutations(Dictionary<string, uint> globalFeatureFlags)
	{
		DetermineFeatureFlags(globalFeatureFlags);
		Permutations = new List<ShaderPermutation>();
		int[] array = new int[Features.Count];
		for (int i = 0; i < Features.Count; i++)
		{
			array[i] = 0;
		}
		int num = Features.Count - 1;
		while (num >= 0)
		{
			ShaderPermutation shaderPermutation = new ShaderPermutation();
			for (int i = 0; i < Features.Count; i++)
			{
				shaderPermutation.SpecificFeatures.Add(Features[i].Flavors[array[i]]);
			}
			if (shaderPermutation.IsValid())
			{
				shaderPermutation.ComputeShaderType(FeatureFlavorFlags);
				Permutations.Add(shaderPermutation);
			}
			bool flag = false;
			array[num]++;
			while (num >= 0 && array[num] >= Features[num].Flavors.Count())
			{
				array[num] = 0;
				num--;
				if (num < 0)
				{
					flag = false;
					break;
				}
				array[num]++;
				flag = true;
			}
			if (flag)
			{
				num = Features.Count - 1;
			}
		}
	}

	public List<ShaderLinkedSource> LinkShaderSources(List<ShaderSource> sources, ShaderVersion shaderVersion)
	{
		List<ShaderLinkedSource> list = new List<ShaderLinkedSource>();
		foreach (ShaderPermutation permutation in Permutations)
		{
			list.AddRange(permutation.LinkShaderSources(sources, shaderVersion));
		}
		return list;
	}
}
