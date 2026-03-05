using System;
using System.Text.RegularExpressions;

namespace GFxShaderMaker.Platforms;

public class ShaderVersion_GLSL150 : ShaderVersion_GLSLCommon
{
	public override string SourceExtension => ".150.glsl";

	protected override string GLSLVersionString => "#version 150\n";

	public ShaderVersion_GLSL150(ShaderPlatform platform)
		: base(platform, "GLSL150")
	{
	}

	protected override void PerformVersionSpecificReplacements(ref string shaderCode, ShaderLinkedSource linkedSrc)
	{
		shaderCode = Regex.Replace(shaderCode, "\\btex2D\\b", "texture");
		shaderCode = Regex.Replace(shaderCode, "\\btex2Dlod\\b", "textureLod");
	}

	protected override string GetShaderVariableQualifier(ShaderVariable.VariableType VarType, ShaderPipeline pipeline)
	{
		return VarType switch
		{
			ShaderVariable.VariableType.Variable_Uniform => "uniform", 
			ShaderVariable.VariableType.Variable_Attribute => "in", 
			ShaderVariable.VariableType.Variable_Varying => (pipeline.Type == ShaderPipeline.PipelineType.Fragment) ? "in" : "out", 
			ShaderVariable.VariableType.Variable_FragOut => "out", 
			_ => throw new Exception("GetShaderVariableQualifier should never be called with type: " + VarType), 
		};
	}
}
