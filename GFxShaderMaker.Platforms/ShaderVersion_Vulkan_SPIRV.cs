using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace GFxShaderMaker.Platforms;

public class ShaderVersion_Vulkan_SPIRV : ShaderVersion_GLSLCommon
{
	public override string SourceExtension => ".450.glsl";

	protected override string GLSLVersionString => "#version 450\n";

	public ShaderVersion_Vulkan_SPIRV(ShaderPlatform platform)
		: base(platform, "Vulkan_SPIRV")
	{
	}

	public override string CreateFinalSource(ShaderLinkedSource linkedSrc)
	{
		string text = GLSLVersionString;
		List<ShaderVariable> list = linkedSrc.VariableList.FindAll((ShaderVariable var) => var.VarType != ShaderVariable.VariableType.Variable_VirtualUniform && (linkedSrc.Pipeline.Type != ShaderPipeline.PipelineType.Vertex || !var.Semantic.StartsWith("POSITION") || var.VarType == ShaderVariable.VariableType.Variable_Attribute)).ToList();
		list.Sort();

		// Vulkan: non-opaque uniforms must be in a block; samplers need layout(binding=N)
		List<ShaderVariable> blockVars = list.FindAll((ShaderVariable v) => v.VarType == ShaderVariable.VariableType.Variable_Uniform && Regex.Matches(v.Type, "sampler", RegexOptions.IgnoreCase).Count == 0);
		List<ShaderVariable> samplerVars = list.FindAll((ShaderVariable v) => v.VarType == ShaderVariable.VariableType.Variable_Uniform && Regex.Matches(v.Type, "sampler", RegexOptions.IgnoreCase).Count > 0);

		if (blockVars.Count > 0)
		{
			text += "layout(set=0, binding=0) uniform UniformBlock { ";
			for (int i = 0; i < blockVars.Count; i++)
			{
				ShaderVariable v = blockVars[i];
				text += v.Type + " " + v.ID + ((v.ArraySize > 1) ? ("[" + v.ArraySize + "]") : "") + "; ";
			}
			text += "};\n";
		}
		int binding = 1;
		foreach (ShaderVariable s in samplerVars)
		{
			string arr = (s.ArraySize > 1) ? ("[" + s.ArraySize + "]") : "";
			text += "layout(binding=" + (binding++) + ") uniform " + s.Type + " " + s.ID + arr + ";\n";
		}

		int locationIn = 0;
		int locationOut = 0;
		foreach (ShaderVariable item in list)
		{
			if (item.VarType == ShaderVariable.VariableType.Variable_Uniform)
				continue;
			string qual = GetShaderVariableQualifier(item.VarType, linkedSrc.Pipeline);
			if (qual != null)
			{
				bool isInput = (qual == "in");
				bool isOutput = (qual == "out");
				string layout = "";
				if (isInput)
					layout = "layout(location=" + (locationIn++) + ") ";
				else if (isOutput)
					layout = "layout(location=" + (locationOut++) + ") ";
				text += layout + qual + " " + item.Type + " " + item.ID + ((item.ArraySize > 1) ? ("[" + item.ArraySize + "]") : "") + ";\n";
			}
		}
		text += "void main() { \n";
		string sourceCode = linkedSrc.SourceCode;
		sourceCode = Regex.Replace(sourceCode, "\\[([^\\]]+)\\]", "[int($1)]");
		sourceCode = Regex.Replace(sourceCode, "([^\\w\\.])(\\d+)([^\\w\\.])", "$1$2.0$3");
		text += sourceCode + "}\n";
		text = Regex.Replace(text, "\\blerp\\b", "mix");
		text = Regex.Replace(text, "\\b(?:float|half|lowpf)([1-4])x([1-4])\\b", "mat$2");
		text = Regex.Replace(text, "\\b(?:float|half|lowpf)([1-4])\\b", "vec$1");
		text = Regex.Replace(text, "\\b(?:float|half|lowpf)\\b", "float");
		text = Regex.Replace(text, "\\b([0-9\\.]+)f", "$1");
		text = Regex.Replace(text, "[+-]0\\.?0*?f?([^\\.\\d])", "$1");
		text = Regex.Replace(text, "\\bfrac\\b", "fract");
		text = Regex.Replace(text, "mul\\s*\\((?'P0'" + ShaderVersion.SubexprRegex + "),(?'P1'" + ShaderVersion.SubexprRegex + ")\\)", "(${P0}) * (${P1})");
		foreach (ShaderVariable item2 in linkedSrc.VariableList.FindAll((ShaderVariable v) => v.VarType == ShaderVariable.VariableType.Variable_Varying && v.Semantic.StartsWith("POSITION")))
			text = Regex.Replace(text, "\\b" + item2.ID + "\\b", "gl_Position");
		PerformVersionSpecificReplacements(ref text, linkedSrc);
		return text;
	}

	public override void PostLink_Instanced(ShaderLinkedSource linkedSrc)
	{
		base.PostLink_Batch(linkedSrc);
		ShaderVariable vbatch = linkedSrc.VariableList.Find((ShaderVariable var) => var.ID == "vbatch");
		ShaderVariable factorAttr = linkedSrc.VariableList.Find((ShaderVariable var) => var.VarType == ShaderVariable.VariableType.Variable_Attribute && var.Semantic.StartsWith("factor", StringComparison.InvariantCultureIgnoreCase));
		if (factorAttr != null && vbatch == null)
		{
			linkedSrc.SourceCode = linkedSrc.SourceCode.Replace(factorAttr.ID + ".b*255.01f", "gl_InstanceIndex");
		}
		else if (vbatch != null)
		{
			linkedSrc.SourceCode = linkedSrc.SourceCode.Replace(vbatch.ID, "gl_InstanceIndex");
			linkedSrc.VariableList.Remove(vbatch);
		}
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

	public override void WriteBinaryShaderSource(StreamWriter sourceFile)
	{
		foreach (ShaderLinkedSource value in LinkedSourceDuplicates.Values)
		{
			string spvPath = Path.Combine(SourceDirectory, value.ID + ".spv");
			if (!File.Exists(spvPath))
				throw new FileNotFoundException("SPIR-V file not found: " + spvPath);
			byte[] bytes = File.ReadAllBytes(spvPath);
			// Use same full id as ShaderDescs (Version.ID + "_" + SourceCodeDuplicateID) for linker resolution.
			// extern to force external linkage (C++ const globals have internal linkage by default).
			string id = ID + "_" + value.SourceCodeDuplicateID;
			sourceFile.Write("extern const unsigned char pBinary_" + id + "[] = {\n");
			for (int i = 0; i < bytes.Length; i++)
			{
				if (i > 0 && (i % 16) == 0)
					sourceFile.Write("\n");
				sourceFile.Write("0x" + bytes[i].ToString("x2"));
				if (i < bytes.Length - 1)
					sourceFile.Write(",");
			}
			sourceFile.Write("\n};\nunsigned pBinary_" + id + "_size = sizeof(pBinary_" + id + ");\n\n");
		}
	}
}
