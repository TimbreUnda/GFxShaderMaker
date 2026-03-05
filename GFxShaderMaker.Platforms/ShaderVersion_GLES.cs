using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GFxShaderMaker.Platforms;

public class ShaderVersion_GLES : ShaderVersion
{
	public override string SourceExtension => ".gles2.glsl";

	public override bool RequireShaderCombos => true;

	public ShaderVersion_GLES(ShaderPlatform platform)
		: base(platform, "GLES")
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

	public override string CreateFinalSource(ShaderLinkedSource linkedSrc)
	{
		string text = ((linkedSrc.Pipeline.Type == ShaderPipeline.PipelineType.Fragment) ? "precision mediump float;\n" : "");
		List<ShaderVariable> list = linkedSrc.VariableList.FindAll((ShaderVariable var) => var.VarType != ShaderVariable.VariableType.Variable_VirtualUniform && var.VarType != ShaderVariable.VariableType.Variable_FragOut && (linkedSrc.Pipeline.Type != ShaderPipeline.PipelineType.Vertex || !var.Semantic.StartsWith("POSITION") || var.VarType == ShaderVariable.VariableType.Variable_Attribute)).ToList();
		list.Sort();
		foreach (ShaderVariable item in list)
		{
			string text2 = item.VarType.ToString().Split("_".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)[1].ToLower();
			string text3 = text;
			text = text3 + text2 + " " + item.Type + " " + item.ID + ((item.ArraySize > 1) ? ("[" + item.ArraySize + "]") : "") + ";\n";
		}
		text += "void main() { \n";
		ShaderVariable shaderVariable = null;
		if (linkedSrc.Pipeline.Type == ShaderPipeline.PipelineType.Fragment)
		{
			shaderVariable = linkedSrc.VariableList.FindLast((ShaderVariable v) => v.VarType == ShaderVariable.VariableType.Variable_FragOut);
			string text3 = text;
			text = text3 + shaderVariable.Type + " " + shaderVariable.ID + ";\n";
		}
		string sourceCode = linkedSrc.SourceCode;
		sourceCode = Regex.Replace(sourceCode, "\\[([^\\]]+)\\]", "[int($1)]");
		sourceCode = Regex.Replace(sourceCode, "([^\\w\\.])(\\d+)([^\\w\\.])", "$1$2.0$3");
		if (shaderVariable != null)
		{
			sourceCode = sourceCode + "gl_FragColor = " + shaderVariable.ID + ";\n";
		}
		text = text + sourceCode + "}\n";
		text = Regex.Replace(text, "(^|\\b)half([1-4])x([1-4])\\b", "mediump mat$3");
		text = Regex.Replace(text, "(^|\\b)lowpf([1-4])x([1-4])", "lowp mat$3");
		text = Regex.Replace(text, "(^|\\b)half([1-4])\\b", "mediump vec$2");
		text = Regex.Replace(text, "(^|\\b)lowpf([1-4])\\b", "lowp vec$2");
		text = Regex.Replace(text, "(^|\\b)half\\b", "mediump float");
		text = Regex.Replace(text, "(^|\\b)lowpf\\b", "lowp float");
		text = Regex.Replace(text, "(^|\\b)tex2Dlod\\s*\\((?'P0'" + ShaderVersion.SubexprRegex + "),(?'P1'" + ShaderVersion.SubexprRegex + "),(?'P2'" + ShaderVersion.SubexprRegex + ")\\)", "texture2D(${P0}, ${P1})");
		text = Regex.Replace(text, "(^|\\b)lerp\\b", "mix");
		text = Regex.Replace(text, "(^|\\b)(?:float|half|lowpf)([1-4])x([1-4])\\b", "mat$3");
		text = Regex.Replace(text, "(^|\\b)(?:float|half|lowpf)([1-4])\\b", "vec$2");
		text = Regex.Replace(text, "(^|\\b)(?:float|half|lowpf)\\b", "float");
		text = Regex.Replace(text, "(^|\\b)([0-9\\.]+)f", "$2");
		text = Regex.Replace(text, "[+-]0\\.?0*?f?([^\\.\\d])", "$2");
		text = Regex.Replace(text, "(^|\\b)tex2D\\b", "texture2D");
		text = Regex.Replace(text, "(^|\\b)tex2Dlod\\b", "texture2DLod");
		text = Regex.Replace(text, "(^|\\b)frac\\b", "fract");
		text = Regex.Replace(text, "mul\\s*\\((?'P0'" + ShaderVersion.SubexprRegex + "),(?'P1'" + ShaderVersion.SubexprRegex + ")\\)", "(${P0}) * (${P1})");
		foreach (ShaderVariable item2 in linkedSrc.VariableList.FindAll((ShaderVariable v) => v.VarType == ShaderVariable.VariableType.Variable_Varying && v.Semantic.StartsWith("POSITION")))
		{
			text = Regex.Replace(text, "(^|\\b)" + item2.ID + "\\b", "gl_Position");
		}
		return text;
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
}
