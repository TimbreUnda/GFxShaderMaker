using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace GFxShaderMaker.Platforms;

[Platform("Orbis", "Sony Orbis PSSL")]
public class Platform_Orbis : ShaderPlatform
{
	private List<ShaderVersion> ShaderVersions = new List<ShaderVersion>();

	private List<ShaderOutputType> OutputTypes = new List<ShaderOutputType>();

	internal string PSSLExtraOptions => "";

	public override List<ShaderVersion> RequestedShaderVersions => ShaderVersions;

	public override IEnumerable<ShaderOutputType> SupportedOutputTypes => OutputTypes;

	protected virtual string OrbisEnvironmentVariable => "SCE_ORBIS_SDK_DIR";

	public Platform_Orbis()
	{
		ShaderVersions.Add(new ShaderVersion_Orbis(this));
		OutputTypes.Add(ShaderOutputType.Binary);
	}

	public override void CreateShaderOutput(ShaderOutputType type)
	{
		if (type != ShaderOutputType.Binary)
		{
			throw new Exception(type.ToString() + " output type not supported on " + base.PlatformName);
		}
		string text = "";
		string text2 = "";
		string fileName = "";
		string environmentVariable = Environment.GetEnvironmentVariable(OrbisEnvironmentVariable);
		if (!string.IsNullOrEmpty(environmentVariable))
		{
			IEnumerable<string> files = Directory.GetFiles(environmentVariable, "orbis-psslc.exe", SearchOption.AllDirectories);
			if (files.Count() > 0)
			{
				text = files.First();
			}
			files = Directory.GetFiles(environmentVariable, "orbis-ar.exe", SearchOption.AllDirectories);
			if (files.Count() <= 0)
			{
				throw new Exception("Could not locate orbis-ar.exe");
			}
			fileName = files.First();
			files = Directory.GetFiles(environmentVariable, "orbis-objcopy.exe", SearchOption.AllDirectories);
			if (files.Count() <= 0)
			{
				throw new Exception("Could not locate orbis-objcopy.exe.");
			}
			text2 = files.First();
		}
		if (string.IsNullOrEmpty(text))
		{
			throw new Exception("Could not locate orbis-psslc.exe - not found in Orbis installation directory.");
		}
		if (!Directory.Exists(PlatformObjDirectory))
		{
			Directory.CreateDirectory(PlatformObjDirectory);
		}
		string text3 = "rc \"" + PlatformBinaryLibrary + "\"";
		List<CompileThreadData> list = new List<CompileThreadData>();
		foreach (ShaderVersion requestedShaderVersion in RequestedShaderVersions)
		{
			foreach (ShaderLinkedSource value3 in requestedShaderVersion.LinkedSourceDuplicates.Values)
			{
				CompileThreadData item = new CompileThreadData(this, requestedShaderVersion, value3, text);
				list.Add(item);
			}
		}
		CompileShadersThreaded(list);
		foreach (ShaderVersion requestedShaderVersion2 in RequestedShaderVersions)
		{
			string currentDirectory = Environment.CurrentDirectory;
			foreach (ShaderLinkedSource value4 in requestedShaderVersion2.LinkedSourceDuplicates.Values)
			{
				Environment.CurrentDirectory = currentDirectory;
				string text4 = Path.Combine(PlatformObjDirectory, requestedShaderVersion2.ID + "_" + value4.ID + ".o");
				Environment.CurrentDirectory = PlatformObjDirectory;
				string text5 = "-I binary -O elf64-x86-64-freebsd -B i386 " + requestedShaderVersion2.ID + "_" + value4.ID + ".sb " + requestedShaderVersion2.ID + "_" + value4.ID + ".o";
				text3 = text3 + " \"" + text4 + "\"";
				ProcessStartInfo processStartInfo = new ProcessStartInfo(text2, text5);
				processStartInfo.ErrorDialog = false;
				processStartInfo.CreateNoWindow = true;
				processStartInfo.UseShellExecute = false;
				processStartInfo.RedirectStandardError = true;
				processStartInfo.RedirectStandardOutput = true;
				Process process = Process.Start(processStartInfo);
				string value = process.StandardError.ReadToEnd();
				process.WaitForExit();
				if (process.ExitCode != 0)
				{
					Console.WriteLine("Error creating " + text4 + ":");
					Console.WriteLine(text2 + " " + text5);
					Console.WriteLine(value);
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
		ProcessStartInfo processStartInfo2 = new ProcessStartInfo(fileName, text3);
		processStartInfo2.ErrorDialog = false;
		processStartInfo2.CreateNoWindow = true;
		processStartInfo2.UseShellExecute = false;
		processStartInfo2.RedirectStandardError = true;
		processStartInfo2.RedirectStandardOutput = true;
		Process process2 = Process.Start(processStartInfo2);
		string value2 = process2.StandardError.ReadToEnd();
		process2.WaitForExit();
		if (process2.ExitCode != 0)
		{
			Console.WriteLine("Error creating " + PlatformBinaryLibrary + ":");
			Console.WriteLine(value2);
			throw new Exception("Error creating " + PlatformBinaryLibrary);
		}
	}

	protected override void CompileSingleShaderImpl(CompileThreadData ctdata)
	{
		if (ctdata != null)
		{
			Platform_Orbis platform_Orbis = ctdata.This as Platform_Orbis;
			ShaderVersion sVersion = ctdata.SVersion;
			ShaderLinkedSource source = ctdata.Source;
			string exe = ctdata.Exe;
			string text = Path.Combine(ctdata.SVersion.SourceDirectory, source.ID);
			string text2 = text + sVersion.SourceExtension;
			if (!File.Exists(text2))
			{
				StreamWriter streamWriter = File.CreateText(text2);
				streamWriter.Write(source.SourceCode);
				streamWriter.Close();
			}
			string text3 = Path.Combine(PlatformObjDirectory, sVersion.ID + "_" + source.ID) + ".sb";
			string shaderProfile = platform_Orbis.GetShaderProfile(source.Pipeline);
			string text4 = "-entry main -profile " + shaderProfile + " -o \"" + text3 + "\" " + platform_Orbis.PSSLExtraOptions + " \"" + text2 + "\"";
			ctdata.ExitCode = launchProcess(exe, text4, out ctdata.StdOutput, out ctdata.StdError);
			if (ctdata.ExitCode == 0 && !File.Exists(text3))
			{
				ctdata.ExitCode = -255;
			}
			ctdata.ShaderFilename = text2;
			ctdata.CommandLine = exe + " " + text4;
		}
	}

	protected override string GetBinaryShaderDeclaration(ShaderPipeline pipeline)
	{
		return "const int*     pBinary;\nconst int      BinarySize\n;";
	}

	protected override string GetBinaryShaderReference(ShaderPipeline pipeline, string id)
	{
		return string.Format("_binary_{0}_sb_start,\n(_binary_{0}_sb_end - _binary_{0}_sb_start)*4,\n", id);
	}

	protected override string GetBinaryShaderExtern(ShaderPipeline pipeline, string id)
	{
		return string.Format("extern \"C\" const int _binary_{0}_sb_start[];\nextern \"C\" const int _binary_{0}_sb_end[];\n", id);
	}

	internal string GetShaderProfile(ShaderPipeline pipeline)
	{
		return pipeline.Type switch
		{
			ShaderPipeline.PipelineType.Fragment => "sce_ps_orbis", 
			ShaderPipeline.PipelineType.Vertex => "sce_vs_vs_orbis", 
			_ => null, 
		};
	}

	public override string GeneratePipelineSourceExtras(ShaderVersion ver, ShaderPipeline pipeline, ShaderLinkedSource src)
	{
		string text = "";
		string text2 = "";
		if (pipeline.Type == ShaderPipeline.PipelineType.Vertex)
		{
			List<ShaderVariable> list = src.VariableList.FindAll((ShaderVariable v) => v.VarType == ShaderVariable.VariableType.Variable_Attribute);
			if (src.PostFunctions.Find((string f) => f == "Instanced") != null)
			{
				list.RemoveAll((ShaderVariable v) => v.Semantic.StartsWith("INSTANCE"));
			}
			string text3 = text;
			text = text3 + text2 + "/* NumAttribs */    " + list.Count + ",\n";
			text = text + text2 + "/* Attributes */    {\n";
			text2 += "                      ";
			foreach (ShaderVariable item in list)
			{
				string text4 = "VET_Color";
				string semantic = item.Semantic;
				text4 = Regex.Replace(semantic, "\\d+$", "") switch
				{
					"COLOR" => "VET_Color", 
					"FACTOR" => "VET_Color | (1 << VET_Index_Shift)", 
					"TEXCOORD" => "VET_TexCoord", 
					"INSTANCE" => "VET_Instance8", 
					_ => "VET_Pos", 
				};
				object obj = text;
				text = string.Concat(obj, text2, "{ \"", item.ID, "\", ".PadRight(13 - item.ID.Length), item.ElementCount, " | ", text4, "},\n");
			}
			text = text + text2 + "},\n";
			text2 = text2.Remove(0, 22);
		}
		return text;
	}
}
