using System;
using System.Collections.Generic;

namespace GFxShaderMaker.Platforms;

[Platform("GL", "OpenGL 2.0+")]
public class Platform_GL : ShaderPlatform
{
	public enum CommandLineOptions
	{
		[CommandLineOption(CmdLineOptionType.OptionType_String, "glslversion", null, "The comma separated list of GLSL version(s) required (see -listversion).", null)]
		GLSLVersion,
		[CommandLineOption(CmdLineOptionType.OptionType_Action, "listversion", "", "", typeof(ListGLSLVersionsAction))]
		ListGLSLVersion
	}

	public enum GLSLVersions
	{
		[ShaderVersion(typeof(ShaderVersion_GLSL110))]
		GLSL110,
		[ShaderVersion(typeof(ShaderVersion_GLSL120))]
		GLSL120,
		[ShaderVersion(typeof(ShaderVersion_GLSL150))]
		GLSL150
	}

	private List<ShaderVersion> ReqShaderVersions = null;

	private List<ShaderVersion> PosShaderVersions = null;

	private List<ShaderOutputType> OutputTypes = new List<ShaderOutputType>();

	private List<ShaderVersion> ShaderVersions = new List<ShaderVersion>();

	public override List<ShaderVersion> PossibleShaderVersions
	{
		get
		{
			if (PosShaderVersions != null)
			{
				return PosShaderVersions;
			}
			PosShaderVersions = ExtractPossibleVersions(typeof(GLSLVersions), null);
			return PosShaderVersions;
		}
	}

	public override List<ShaderVersion> RequestedShaderVersions
	{
		get
		{
			if (ReqShaderVersions != null)
			{
				return ReqShaderVersions;
			}
			ReqShaderVersions = ExtractPossibleVersions(typeof(GLSLVersions), CommandLineParser.GetOption<string>(CommandLineOptions.GLSLVersion.ToString()));
			return ReqShaderVersions;
		}
	}

	public override IEnumerable<ShaderOutputType> SupportedOutputTypes => OutputTypes;

	public Platform_GL()
	{
		OutputTypes.Add(ShaderOutputType.Source);
		ShaderVersions.Add(new ShaderVersion_GLSL110(this));
		ShaderVersions.Add(new ShaderVersion_GLSL120(this));
		ShaderVersions.Add(new ShaderVersion_GLSL150(this));
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
