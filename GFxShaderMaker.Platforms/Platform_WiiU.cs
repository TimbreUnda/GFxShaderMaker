using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GFxShaderMaker.Platforms;

[Platform("WiiU", "Nintendo WiiU GLSL")]
public class Platform_WiiU : ShaderPlatform
{
	private List<ShaderOutputType> OutputTypes = new List<ShaderOutputType>();

	private List<ShaderVersion> ShaderVersions = new List<ShaderVersion>();

	public override IEnumerable<ShaderOutputType> SupportedOutputTypes => OutputTypes;

	public override List<ShaderVersion> RequestedShaderVersions => ShaderVersions;

	public string SDKEnvironmentVariable => "CAFE_ROOT_DOS";

	public Platform_WiiU()
	{
		OutputTypes.Add(ShaderOutputType.Binary);
		ShaderVersions.Add(new WiiU_Version(this));
	}

	protected override string GetBinaryShaderDeclaration(ShaderPipeline pipeline)
	{
		return pipeline.Type switch
		{
			ShaderPipeline.PipelineType.Vertex => "const GX2VertexShader*    pBinary;\n", 
			_ => "const GX2PixelShader*     pBinary;\n", 
		};
	}

	protected override string GetBinaryShaderReference(ShaderPipeline pipeline, string id)
	{
		return pipeline.Type switch
		{
			ShaderPipeline.PipelineType.Vertex => $"&{id}_VS,", 
			_ => $"&{id}_PS,", 
		};
	}

	protected override string GetBinaryShaderExtern(ShaderPipeline pipeline, string id)
	{
		return pipeline.Type switch
		{
			ShaderPipeline.PipelineType.Vertex => $"extern GX2VertexShader    {id}_VS;", 
			_ => $"extern GX2PixelShader     {id}_PS;", 
		};
	}

	protected override void WriteBinarySource(StreamWriter sourceFile)
	{
		sourceFile.WriteLine("\r\n#include <cafe/gx2.h>\r\n\r\ntypedef struct _GFDLoopVar\r\n{\r\n    u32 reg[GX2_NUM_LOOP_VAR_U32_WORDS];\r\n} GFDLoopVar;\r\n\r\n\r\n");
		sourceFile.WriteLine("namespace Scaleform { namespace Render { namespace " + PlatformBase + " {\n\n");
		base.WriteBinarySource(sourceFile);
		sourceFile.WriteLine("}}}; // Scaleform::Render::" + PlatformBase + "\n\n");
	}

	public override void CreateShaderOutput(ShaderOutputType type)
	{
		if (type != ShaderOutputType.Binary)
		{
			throw new Exception(type.ToString() + " output type not supported on " + base.PlatformName);
		}
		string f = "";
		string text = Environment.GetEnvironmentVariable(SDKEnvironmentVariable);
		if (string.IsNullOrEmpty(text))
		{
			text = "C:\\CAFE_SDK";
		}
		if (!string.IsNullOrEmpty(text))
		{
			Path.Combine(text, "\\system\\bin\\win32");
			IEnumerable<string> files = Directory.GetFiles(text, "gshCompile.exe", SearchOption.AllDirectories);
			if (files.Count() > 0)
			{
				f = files.First();
			}
		}
		List<CompileThreadData> list = new List<CompileThreadData>();
		foreach (ShaderVersion requestedShaderVersion in RequestedShaderVersions)
		{
			foreach (ShaderLinkedSource value in requestedShaderVersion.LinkedSourceDuplicates.Values)
			{
				CompileThreadData item = new CompileThreadData(this, requestedShaderVersion, value, f);
				list.Add(item);
			}
		}
		CompileShadersThreaded(list);
		CreateBinarySource();
	}

	protected override void CompileSingleShaderImpl(CompileThreadData ctdata)
	{
		if (ctdata != null)
		{
			Platform_WiiU platform_WiiU = ctdata.This as Platform_WiiU;
			ShaderVersion sVersion = ctdata.SVersion;
			ShaderLinkedSource source = ctdata.Source;
			string exe = ctdata.Exe;
			string path = sVersion.ID + "_" + source.ID;
			string text = Path.Combine(ctdata.SVersion.SourceDirectory, path);
			string text2 = text + sVersion.SourceExtension;
			if (!File.Exists(text2))
			{
				StreamWriter streamWriter = File.CreateText(text2);
				streamWriter.Write(source.SourceCode);
				streamWriter.Close();
			}
			string shaderProfile = platform_WiiU.GetShaderProfile(source.Pipeline);
			string text3 = "-" + shaderProfile + " \"" + text2 + "\" -oh \"" + text + ".h\"";
			ctdata.ExitCode = launchProcess(exe, text3, out ctdata.StdOutput, out ctdata.StdError);
			ctdata.ShaderFilename = text2;
			ctdata.CommandLine = exe + " " + text3;
		}
	}

	private string GetShaderProfile(ShaderPipeline pipeline)
	{
		return pipeline.Type switch
		{
			ShaderPipeline.PipelineType.Fragment => "p", 
			_ => "v", 
		};
	}
}
