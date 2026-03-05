using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GFxShaderMaker.Platforms;

public abstract class ShaderVersion_GLSLCommon : ShaderVersion
{
	public override bool RequireShaderCombos => true;

	protected abstract string GLSLVersionString { get; }

	public ShaderVersion_GLSLCommon(ShaderPlatform platform, string id)
		: base(platform, id)
	{
	}

	public override string GetVariableUniformRegisterType(ShaderVariable var)
	{
		string result = "c";
		if (Regex.Matches(var.Type, "sampler", RegexOptions.IgnoreCase).Count > 0)
		{
			result = "s";
		}
		return result;
	}

	public override void PostLink_Instanced(ShaderLinkedSource linkedSrc)
	{
		base.PostLink_Batch(linkedSrc);
		ShaderVariable shaderVariable = linkedSrc.VariableList.Find((ShaderVariable var) => var.ID == "vbatch");
		ShaderVariable shaderVariable2 = linkedSrc.VariableList.Find((ShaderVariable var) => var.VarType == ShaderVariable.VariableType.Variable_Attribute && var.Semantic.StartsWith("factor", StringComparison.InvariantCultureIgnoreCase));
		if (shaderVariable2 != null && shaderVariable == null)
		{
			linkedSrc.SourceCode = linkedSrc.SourceCode.Replace(shaderVariable2.ID + ".b*255.01f", "gl_InstanceID");
		}
		else if (shaderVariable != null)
		{
			linkedSrc.SourceCode = linkedSrc.SourceCode.Replace(shaderVariable.ID, "gl_InstanceID");
			linkedSrc.VariableList.Remove(shaderVariable);
		}
	}

	public override string CreateFinalSource(ShaderLinkedSource linkedSrc)
	{
		string text = "";
		text += GLSLVersionString;
		if (linkedSrc.Flags.Find((string f) => f == "Instanced") != null)
		{
			text += "#extension GL_ARB_draw_instanced : enable\n";
		}
		List<ShaderVariable> list = linkedSrc.VariableList.FindAll((ShaderVariable var) => var.VarType != ShaderVariable.VariableType.Variable_VirtualUniform && (linkedSrc.Pipeline.Type != ShaderPipeline.PipelineType.Vertex || !var.Semantic.StartsWith("POSITION") || var.VarType == ShaderVariable.VariableType.Variable_Attribute)).ToList();
		list.Sort();
		foreach (ShaderVariable item in list)
		{
			string shaderVariableQualifier = GetShaderVariableQualifier(item.VarType, linkedSrc.Pipeline);
			if (shaderVariableQualifier != null)
			{
				string text2 = text;
				text = text2 + shaderVariableQualifier + " " + item.Type + " " + item.ID + ((item.ArraySize > 1) ? ("[" + item.ArraySize + "]") : "") + ";\n";
			}
		}
		text += "void main() { \n";
		string sourceCode = linkedSrc.SourceCode;
		sourceCode = Regex.Replace(sourceCode, "\\[([^\\]]+)\\]", "[int($1)]");
		sourceCode = Regex.Replace(sourceCode, "([^\\w\\.])(\\d+)([^\\w\\.])", "$1$2.0$3");
		text = text + sourceCode + "}\n";
		text = Regex.Replace(text, "\\blerp\\b", "mix");
		text = Regex.Replace(text, "\\b(?:float|half|lowpf)([1-4])x([1-4])\\b", "mat$2");
		text = Regex.Replace(text, "\\b(?:float|half|lowpf)([1-4])\\b", "vec$1");
		text = Regex.Replace(text, "\\b(?:float|half|lowpf)\\b", "float");
		text = Regex.Replace(text, "\\b([0-9\\.]+)f", "$1");
		text = Regex.Replace(text, "[+-]0\\.?0*?f?([^\\.\\d])", "$1");
		text = Regex.Replace(text, "\\bfrac\\b", "fract");
		text = Regex.Replace(text, "mul\\s*\\((?'P0'" + ShaderVersion.SubexprRegex + "),(?'P1'" + ShaderVersion.SubexprRegex + ")\\)", "(${P0}) * (${P1})");
		foreach (ShaderVariable item2 in linkedSrc.VariableList.FindAll((ShaderVariable v) => v.VarType == ShaderVariable.VariableType.Variable_Varying && v.Semantic.StartsWith("POSITION")))
		{
			text = Regex.Replace(text, "\\b" + item2.ID + "\\b", "gl_Position");
		}
		PerformVersionSpecificReplacements(ref text, linkedSrc);
		return text;
	}

	protected abstract void PerformVersionSpecificReplacements(ref string shaderCode, ShaderLinkedSource linkedSrc);

	protected abstract string GetShaderVariableQualifier(ShaderVariable.VariableType VarType, ShaderPipeline pipeline);
}
