using System.IO;
using System.Text.RegularExpressions;

namespace GFxShaderMaker.Platforms;

public abstract class ShaderVersion_D3DCommon : ShaderVersion
{
	public override string SourceExtension => ".hlsl";

	public ShaderVersion_D3DCommon(ShaderPlatform platform, string id)
		: base(platform, id)
	{
	}

	public virtual string GetD3DFXCExtraOptions(string exe, ShaderLinkedSource src)
	{
		return "";
	}

	public abstract string GetShaderProfile(ShaderPipeline pipeline);

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

	public override void WriteBinaryShaderSource(StreamWriter sourceFile)
	{
		foreach (ShaderLinkedSource value in LinkedSourceDuplicates.Values)
		{
			string path = Path.Combine(base.SourceDirectory, value.ID + SourceExtension + ".h");
			StreamReader streamReader = File.OpenText(path);
			string text = streamReader.ReadToEnd();
			text = ((!(base.Platform.PlatformName != "D3D1x")) ? text.Replace("const BYTE", "extern const BYTE") : text.Replace("const ", "extern const "));
			sourceFile.Write(text);
			if (base.Platform.PlatformName == "D3D1x")
			{
				sourceFile.WriteLine("extern const int pBinary_" + base.ID + "_" + value.ID + "_size = sizeof(pBinary_" + base.ID + "_" + value.ID + ");");
			}
			streamReader.Close();
		}
	}
}
