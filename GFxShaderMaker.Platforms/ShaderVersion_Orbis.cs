using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GFxShaderMaker.Platforms;

internal class ShaderVersion_Orbis : ShaderVersion
{
	public override string SourceExtension => ".pssl";

	public ShaderVersion_Orbis(ShaderPlatform platform)
		: base(platform, "Orbis")
	{
		SupportedPipelines.Add(new ShaderPipeline(ShaderPipeline.PipelineType.Hull));
		SupportedPipelines.Add(new ShaderPipeline(ShaderPipeline.PipelineType.Domain));
		SupportedPipelines.Add(new ShaderPipeline(ShaderPipeline.PipelineType.Geometry));
		SupportedPipelines.Add(new ShaderPipeline(ShaderPipeline.PipelineType.Compute));
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
		UndoFactorBatchIndexing(linkedSrc);
	}

	public override string CreateFinalSource(ShaderLinkedSource linkedSrc)
	{
		string text = "";
		List<ShaderVariable> list = linkedSrc.VariableList.FindAll((ShaderVariable shaderVariable) => shaderVariable.VarType == ShaderVariable.VariableType.Variable_Uniform && !shaderVariable.SamplerType).ToList();
		List<ShaderVariable> list2 = linkedSrc.VariableList.FindAll((ShaderVariable shaderVariable) => shaderVariable.VarType == ShaderVariable.VariableType.Variable_Uniform && shaderVariable.SamplerType).ToList();
		list.Sort();
		if (list.Count > 0)
		{
			text += "ConstantBuffer Constants : register(b0) { \n";
			foreach (ShaderVariable item in list)
			{
				object obj = text;
				text = string.Concat(obj, item.Type, " ", item.ID, (item.ArraySize > 1) ? ("[" + item.ArraySize + "]") : "", " : packoffset(c", item.BaseRegister, ");\n");
			}
			text += "};\n\n";
		}
		int num = 0;
		foreach (ShaderVariable item2 in list2)
		{
			object obj = text;
			text = string.Concat(obj, "SamplerState sampler_", item2.ID, (item2.ArraySize > 1) ? ("[" + item2.ArraySize + "]") : "", " : register(s", num++, ");\n");
			obj = text;
			text = string.Concat(obj, Regex.Replace(item2.Type, "^sampler", "Texture"), " ", item2.ID, (item2.ArraySize > 1) ? ("[" + item2.ArraySize + "]") : "", " : register(t", item2.BaseRegister, ");\n");
		}
		text += "void main( ";
		bool flag = true;
		ShaderVariable.VariableType inType;
		ShaderVariable.VariableType outType;
		switch (linkedSrc.Pipeline.Type)
		{
		default:
			inType = ShaderVariable.VariableType.Variable_Attribute;
			outType = ShaderVariable.VariableType.Variable_Varying;
			break;
		case ShaderPipeline.PipelineType.Fragment:
			inType = ShaderVariable.VariableType.Variable_Varying;
			outType = ShaderVariable.VariableType.Variable_FragOut;
			break;
		}
		ShaderVariable var;
		foreach (ShaderVariable item3 in linkedSrc.VariableList.FindAll((ShaderVariable shaderVariable) => shaderVariable.VarType == inType || shaderVariable.VarType == outType))
		{
			var = item3;
			if (!flag)
			{
				text += ",\n           ";
			}
			flag = false;
			string text2 = var.Semantic;
			string text3 = var.Type;
			if (var.Semantic.StartsWith("FACTOR") || var.Semantic.StartsWith("INSTANCE"))
			{
				if (var.Semantic.StartsWith("INSTANCE") && linkedSrc.PostFunctions.Find((string s) => string.Compare(s, "Instanced", ignoreCase: true) == 0) != null)
				{
					text2 = "S_INSTANCE_ID";
				}
				else
				{
					text2 = "COLOR";
					List<ShaderVariable> list3 = linkedSrc.VariableList.FindAll((ShaderVariable v) => v.Semantic.StartsWith("COLOR") && v.VarType == var.VarType);
					string value = "-1";
					if (list3.Count > 0)
					{
						value = Regex.Replace(list3.Max((ShaderVariable v) => v.Semantic), "^.*(\\d+)$", "$1");
					}
					text2 += Convert.ToInt32(value) + (var.Semantic.StartsWith("FACTOR") ? 1 : 2);
				}
			}
			else if (linkedSrc.Pipeline.Type == ShaderPipeline.PipelineType.Fragment && var.VarType == outType && var.Semantic.StartsWith("COLOR"))
			{
				text2 = Regex.Replace(var.Semantic, "^COLOR", "S_TARGET_OUTPUT");
			}
			else if (var.Semantic.StartsWith("POSITION"))
			{
				if (linkedSrc.Pipeline.Type == ShaderPipeline.PipelineType.Vertex && var.VarType == outType)
				{
					text2 = Regex.Replace(var.Semantic, "^POSITION", "S_POSITION");
				}
				text2 = Regex.Replace(text2, "0$", "");
			}
			if (var.Semantic.StartsWith("INSTANCE") || text2.StartsWith("S_INSTANCE_ID"))
			{
				text3 = Regex.Replace(text3, "float", "uint");
				text3 = Regex.Replace(text3, "lowpf", "uint");
			}
			string text4 = text;
			text = text4 + ((var.VarType == outType) ? "out " : "") + text3 + " " + var.ID + ((var.ArraySize > 1) ? ("[" + var.ArraySize + "]") : "") + " : " + text2;
		}
		text += ")\n{";
		text = text + linkedSrc.SourceCode + "}\n";
		text = text.Replace("lowpf", "float");
		text = Regex.Replace(text, "tex\\dD\\s*\\(\\s*([^,]+)", "$1.Sample(sampler_$1");
		return Regex.Replace(text, "tex\\dDlod\\s*\\(\\s*([^,]+)", "$1.SampleLOD(sampler_$1");
	}
}
