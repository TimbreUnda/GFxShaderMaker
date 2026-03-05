using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GFxShaderMaker.Platforms;

[Platform("PSVita", "Sony PS VITA Cg")]
public class Platform_PSVITA : ShaderPlatform
{
	private List<ShaderOutputType> OutputTypes = new List<ShaderOutputType>();

	private List<ShaderVersion> ShaderVersions = new List<ShaderVersion>();

	public override IEnumerable<ShaderOutputType> SupportedOutputTypes => OutputTypes;

	public override List<ShaderVersion> RequestedShaderVersions => ShaderVersions;

	public string CGCExtraOptions
	{
		get
		{
			string option = CommandLineParser.GetOption(CommandLineParser.Options.Config);
			if (option.StartsWith("Debug"))
			{
				return "-cache ";
			}
			if (option.StartsWith("Release") || option.StartsWith("Shipping"))
			{
				return "-cache -fastprecision ";
			}
			return "-cache -fastprecision ";
		}
	}

	public string PSP2SDKEnvironmentVariable => "SCE_PSP2_SDK_DIR";

	public Platform_PSVITA()
	{
		OutputTypes.Add(ShaderOutputType.Binary);
		ShaderVersions.Add(new ShaderVersion_PSVITA(this));
	}

	protected override string GetBinaryShaderDeclaration(ShaderPipeline pipeline)
	{
		return "const SceGxmProgram*     pBinary;";
	}

	protected override string GetBinaryShaderReference(ShaderPipeline pipeline, string id)
	{
		return $"_binary_{id}_gxp,";
	}

	protected override string GetBinaryShaderExtern(ShaderPipeline pipeline, string id)
	{
		return $"extern \"C\" const SceGxmProgram _binary_{id}_gxp[];";
	}

	protected string GetShaderProfile(ShaderPipeline pipeline)
	{
		return pipeline.Type switch
		{
			ShaderPipeline.PipelineType.Fragment => "sce_fp_psp2", 
			ShaderPipeline.PipelineType.Vertex => "sce_vp_psp2", 
			_ => null, 
		};
	}

	public override void CreateShaderOutput(ShaderOutputType type)
	{
		if (type != ShaderOutputType.Binary)
		{
			throw new Exception(type.ToString() + " output type not supported on " + base.PlatformName);
		}
		string f = "";
		string executable = "";
		string text = "";
		string environmentVariable = Environment.GetEnvironmentVariable(PSP2SDKEnvironmentVariable);
		if (!string.IsNullOrEmpty(environmentVariable))
		{
			IEnumerable<string> files = Directory.GetFiles(environmentVariable, "psp2cgc.exe", SearchOption.AllDirectories);
			if (files.Count() <= 0)
			{
				throw new Exception("Could not locate psp2cgc.exe.");
			}
			f = files.First();
			files = Directory.GetFiles(environmentVariable, "psp2snarl.exe", SearchOption.AllDirectories);
			if (files.Count() <= 0)
			{
				throw new Exception("Could not locate psp2snarl.exe.");
			}
			executable = files.First();
			files = Directory.GetFiles(environmentVariable, "psp2bin.exe", SearchOption.AllDirectories);
			if (files.Count() <= 0)
			{
				throw new Exception("Could not locate psp2bin.exe.");
			}
			text = files.First();
		}
		if (!Directory.Exists(PlatformObjDirectory))
		{
			Directory.CreateDirectory(PlatformObjDirectory);
		}
		string text2 = "rc \"" + PlatformBinaryLibrary + "\"";
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
		string stdout;
		string stderr;
		foreach (ShaderVersion requestedShaderVersion2 in RequestedShaderVersions)
		{
			string currentDirectory = Environment.CurrentDirectory;
			foreach (ShaderLinkedSource value2 in requestedShaderVersion2.LinkedSourceDuplicates.Values)
			{
				Environment.CurrentDirectory = currentDirectory;
				string text3 = requestedShaderVersion2.ID + "_" + value2.ID;
				string text4 = Path.Combine(PlatformObjDirectory, text3 + ".o");
				Environment.CurrentDirectory = PlatformObjDirectory;
				string text5 = "-i " + text3 + ".gxp -o " + text3 + ".o -b2e PSP2,_binary_" + text3 + "_gxp,_binary_" + text3 + "_gxp_size";
				text2 = text2 + " \"" + text4 + "\"";
				if (launchProcess(text, text5, out stdout, out stderr) != 0)
				{
					Console.WriteLine("Error creating " + text4 + ":");
					Console.WriteLine(text + " " + text5);
					Console.WriteLine(stderr);
					throw new Exception("Error creating " + text4);
				}
			}
			Environment.CurrentDirectory = currentDirectory;
		}
		if (!Directory.Exists(Path.GetDirectoryName(PlatformBinaryLibrary)))
		{
			Directory.CreateDirectory(Path.GetDirectoryName(PlatformBinaryLibrary));
		}
		File.Delete(PlatformBinaryLibrary);
		if (launchProcess(executable, text2, out stdout, out stderr) != 0)
		{
			Console.WriteLine("Error creating " + PlatformBinaryLibrary + ":");
			Console.WriteLine(stderr);
			throw new Exception("Error creating " + PlatformBinaryLibrary);
		}
	}

	protected override void CompileSingleShaderImpl(CompileThreadData ctdata)
	{
		if (ctdata != null)
		{
			Platform_PSVITA platform_PSVITA = ctdata.This as Platform_PSVITA;
			ShaderVersion sVersion = ctdata.SVersion;
			ShaderLinkedSource source = ctdata.Source;
			string exe = ctdata.Exe;
			string text = Path.Combine(ctdata.SVersion.SourceDirectory, sVersion.ID + "_" + source.ID);
			string text2 = text + sVersion.SourceExtension;
			if (!File.Exists(text2))
			{
				StreamWriter streamWriter = File.CreateText(text2);
				streamWriter.Write(source.SourceCode);
				streamWriter.Close();
			}
			string text3 = Path.Combine(PlatformObjDirectory, sVersion.ID + "_" + source.ID) + ".gxp";
			string shaderProfile = platform_PSVITA.GetShaderProfile(source.Pipeline);
			string text4 = "-profile " + shaderProfile + " -o \"" + text3 + "\" " + platform_PSVITA.CGCExtraOptions + " \"" + text2 + "\"";
			ctdata.ExitCode = launchProcess(exe, text4, out ctdata.StdOutput, out ctdata.StdError);
			if (ctdata.ExitCode == 0 && !File.Exists(text3))
			{
				ctdata.ExitCode = -255;
			}
			ctdata.ShaderFilename = text2;
			ctdata.CommandLine = exe + " " + text4;
		}
	}
}
