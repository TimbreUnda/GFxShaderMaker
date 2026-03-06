using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GFxShaderMaker.Platforms;

[Platform("Vulkan", "Vulkan SPIR-V")]
public class Platform_Vulkan : ShaderPlatform
{
	private static List<ShaderOutputType> OutputTypes;
	private List<ShaderVersion> ReqShaderVersions;

	static Platform_Vulkan()
	{
		OutputTypes = new List<ShaderOutputType> { ShaderOutputType.Binary };
	}

	public override List<ShaderVersion> RequestedShaderVersions
	{
		get
		{
			if (ReqShaderVersions != null)
				return ReqShaderVersions;
			ReqShaderVersions = new List<ShaderVersion> { new ShaderVersion_Vulkan_SPIRV(this) };
			return ReqShaderVersions;
		}
	}

	public override IEnumerable<ShaderOutputType> SupportedOutputTypes => OutputTypes;

	protected override string GetBinaryShaderDeclaration(ShaderPipeline pipeline)
	{
		return "const unsigned char* pBinary;\nunsigned BinarySize;";
	}

	protected override string GetBinaryShaderReference(ShaderPipeline pipeline, string id)
	{
		return "pBinary_" + id + ",\npBinary_" + id + "_size,";
	}

	protected override string GetBinaryShaderExtern(ShaderPipeline pipeline, string id)
	{
		return "extern const unsigned char pBinary_" + id + "[];\nextern unsigned pBinary_" + id + "_size;";
	}

	protected override void WriteBinarySource(StreamWriter sourceFile)
	{
		sourceFile.WriteLine("namespace Scaleform { namespace Render { namespace Vulkan {\n\n");
		foreach (ShaderVersion requestedShaderVersion in RequestedShaderVersions)
			requestedShaderVersion.WriteBinaryShaderSource(sourceFile);
		sourceFile.WriteLine("}}}; // Scaleform::Render::Vulkan\n\n");
	}

	public override void CreateShaderOutput(ShaderOutputType type)
	{
		if (type != ShaderOutputType.Binary)
			throw new Exception(type + " output type not supported on " + PlatformName);

		string glslangValidator = FindGlslangValidator();
		if (string.IsNullOrEmpty(glslangValidator))
			throw new Exception("Could not locate glslangValidator. Set VULKAN_SDK or add it to PATH.");

		if (!Directory.Exists(PlatformSourceDirectory))
			Directory.CreateDirectory(PlatformSourceDirectory);

		var threadData = new List<CompileThreadData>();
		foreach (ShaderVersion requestedShaderVersion in RequestedShaderVersions)
		{
			foreach (ShaderLinkedSource source in requestedShaderVersion.LinkedSourceDuplicates.Values)
			{
				threadData.Add(new CompileThreadData(this, requestedShaderVersion, source, glslangValidator));
			}
		}
		CompileShadersThreaded(threadData);
		CreateBinarySource();
	}

	protected override void CompileSingleShaderImpl(CompileThreadData ctdata)
	{
		if (ctdata == null) return;

		ShaderVersion_Vulkan_SPIRV sversion = ctdata.SVersion as ShaderVersion_Vulkan_SPIRV;
		ShaderLinkedSource source = ctdata.Source;
		string exe = ctdata.Exe;

		string inputPath = Path.Combine(PlatformSourceDirectory, source.ID + sversion.SourceExtension);
		using (var sw = File.CreateText(inputPath))
			sw.Write(source.SourceCode);

		string stage = source.Pipeline.Type == ShaderPipeline.PipelineType.Vertex ? "vert" : "frag";
		string outputPath = Path.Combine(PlatformSourceDirectory, source.ID + ".spv");
		string args = "-V -S " + stage + " -o \"" + outputPath + "\" \"" + inputPath + "\"";

		ctdata.ExitCode = launchProcess(exe, args, out ctdata.StdOutput, out ctdata.StdError);
		ctdata.ShaderFilename = inputPath;
		ctdata.CommandLine = exe + " " + args;
	}

	private static string FindGlslangValidator()
	{
		string name = "glslangValidator" + (Environment.OSVersion.Platform == PlatformID.Win32NT ? ".exe" : "");
		string sdk = Environment.GetEnvironmentVariable("VULKAN_SDK");
		if (!string.IsNullOrEmpty(sdk))
		{
			string path = Path.Combine(sdk, "Bin", name);
			if (File.Exists(path))
				return path;
		}
		string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
		foreach (string dir in pathEnv.Split(Path.PathSeparator))
		{
			if (string.IsNullOrWhiteSpace(dir)) continue;
			string candidate = Path.Combine(dir.Trim(), name);
			if (File.Exists(candidate))
				return candidate;
		}
		return null;
	}
}
