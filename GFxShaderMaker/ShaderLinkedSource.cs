using System;
using System.Collections.Generic;

namespace GFxShaderMaker;

public class ShaderLinkedSource
{
	public string ID;

	public List<ShaderVariable> VariableList = new List<ShaderVariable>();

	public string SourceCode;

	public List<string> PostFunctions = new List<string>();

	public List<string> Flags = new List<string>();

	public ShaderPipeline Pipeline;

	private uint ShaderIndexValue = 0u;

	private bool ShaderIndexSet = false;

	private uint ShaderComboIndexValue = 0u;

	private bool ShaderComboIndexSet = false;

	private string SourceCodeDuplicateIDValue = "";

	private bool SourceCodeDuplicateIDSet = false;

	public uint ShaderIndex
	{
		get
		{
			if (ShaderIndexSet)
			{
				return ShaderIndexValue;
			}
			throw new Exception("Internal error: ShaderIndex accessed before set.");
		}
		set
		{
			ShaderIndexValue = value;
			ShaderIndexSet = true;
		}
	}

	public uint ShaderComboIndex
	{
		get
		{
			if (ShaderComboIndexSet)
			{
				return ShaderComboIndexValue;
			}
			throw new Exception("Internal error: ShaderComboIndex accessed before set.");
		}
		set
		{
			ShaderComboIndexValue = value;
			ShaderComboIndexSet = true;
		}
	}

	public string SourceCodeDuplicateID
	{
		get
		{
			if (SourceCodeDuplicateIDSet)
			{
				return SourceCodeDuplicateIDValue;
			}
			throw new Exception("Internal error: SourceCodeDuplicateID accessed before set.");
		}
		set
		{
			SourceCodeDuplicateIDValue = value;
			SourceCodeDuplicateIDSet = true;
		}
	}

	public uint UniformSize
	{
		get
		{
			uint num = 0u;
			foreach (ShaderVariable item in VariableList.FindAll((ShaderVariable var) => var.VarType == ShaderVariable.VariableType.Variable_Uniform))
			{
				num += item.ArraySize * item.RegisterCount;
			}
			return num;
		}
	}

	public override int GetHashCode()
	{
		int num = SourceCode.GetHashCode() ^ Pipeline.GetHashCode();
		foreach (int item in Flags.ConvertAll((string f) => f.GetHashCode()))
		{
			num ^= item;
		}
		return num;
	}
}
