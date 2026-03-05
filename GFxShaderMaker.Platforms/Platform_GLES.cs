using System;
using System.Collections.Generic;

namespace GFxShaderMaker.Platforms;

[Platform("GLES", "GLES 2.0+")]
public class Platform_GLES : ShaderPlatform
{
	private List<ShaderOutputType> OutputTypes = new List<ShaderOutputType>();

	private List<ShaderVersion> ShaderVersions = new List<ShaderVersion>();

	public override IEnumerable<ShaderOutputType> SupportedOutputTypes => OutputTypes;

	public override List<ShaderVersion> RequestedShaderVersions => ShaderVersions;

	public override string PlatformBase => "GL";

	public Platform_GLES()
	{
		OutputTypes.Add(ShaderOutputType.Source);
		ShaderVersions.Add(new ShaderVersion_GLES(this));
	}

	public override void CreateShaderOutput(ShaderOutputType type)
	{
		if (type != ShaderOutputType.Source)
		{
			throw new Exception(type.ToString() + " output type not supported on " + base.PlatformName);
		}
		CreateSourceSource();
	}
}
