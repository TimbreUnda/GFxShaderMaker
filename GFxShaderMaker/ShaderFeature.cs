using System;
using System.Collections.Generic;
using System.Xml;

namespace GFxShaderMaker;

public class ShaderFeature
{
	public string ID;

	public IList<ShaderFeatureFlavor> Flavors;

	public void ReadFromXml(XmlElement root)
	{
		Flavors = new List<ShaderFeatureFlavor>();
		ID = root.Attributes.GetNamedItem("id").Value;
		XmlNode namedItem = root.Attributes.GetNamedItem("optional");
		if (namedItem != null && Convert.ToBoolean(namedItem.Value))
		{
			ShaderFeatureFlavor shaderFeatureFlavor = new ShaderFeatureFlavor();
			shaderFeatureFlavor.Hidden = true;
			shaderFeatureFlavor.ID = ShaderFeatureFlavor.EmptyID;
			Flavors.Add(shaderFeatureFlavor);
		}
		if (root.GetElementsByTagName("ShaderFeatureFlavor").Count == 0)
		{
			ShaderFeatureFlavor shaderFeatureFlavor2 = new ShaderFeatureFlavor();
			shaderFeatureFlavor2.ReadFromXml(root, root);
			Flavors.Add(shaderFeatureFlavor2);
			return;
		}
		foreach (XmlElement item in root.GetElementsByTagName("ShaderFeatureFlavor"))
		{
			ShaderFeatureFlavor shaderFeatureFlavor2 = new ShaderFeatureFlavor();
			shaderFeatureFlavor2.ReadFromXml(item, root);
			Flavors.Add(shaderFeatureFlavor2);
		}
	}
}
