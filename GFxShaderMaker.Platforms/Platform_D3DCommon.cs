using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace GFxShaderMaker.Platforms;

public abstract class Platform_D3DCommon : ShaderPlatform
{
	private static List<ShaderOutputType> OutputTypes;

	public override IEnumerable<ShaderOutputType> SupportedOutputTypes => OutputTypes;

	protected abstract string D3DSDKEnvironmentVariable { get; }

	protected abstract string D3DFXCExtraOptions { get; }

	static Platform_D3DCommon()
	{
		OutputTypes = new List<ShaderOutputType>();
		OutputTypes.Add(ShaderOutputType.Binary);
	}

	protected override string GetBinaryShaderDeclaration(ShaderPipeline pipeline)
	{
		return "const BYTE*    pBinary;";
	}

	protected override string GetBinaryShaderReference(ShaderPipeline pipeline, string id)
	{
		return $"pBinary_{id},";
	}

	protected override string GetBinaryShaderExtern(ShaderPipeline pipeline, string id)
	{
		return $"extern const BYTE pBinary_{id}[];";
	}

	protected override void WriteBinarySource(StreamWriter sourceFile)
	{
		if (base.PlatformName != "X360")
		{
			sourceFile.WriteLine("#include <windows.h> // BYTE\n\n");
		}
		else
		{
			sourceFile.WriteLine("#include <xtl.h> // DWORD\n\n");
		}
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
		string text = "";
		string text2 = Environment.GetEnvironmentVariable(D3DSDKEnvironmentVariable);
		try
		{
			if (GetType() != typeof(Platform_X360))
			{
				RegistryKey localMachine = Registry.LocalMachine;
				localMachine = localMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows Kits\\Installed Roots");
				string path = localMachine.GetValue("KitsRoot") as string;
				IEnumerable<string> files = Directory.GetFiles(path, "fxc.exe", SearchOption.AllDirectories);
				if (files.Count() > 0)
				{
					text = files.First();
					text2 = null;
				}
			}
		}
		catch (Exception)
		{
		}
		if (!string.IsNullOrEmpty(text2))
		{
			IEnumerable<string> files = Directory.GetFiles(text2, "fxc.exe", SearchOption.AllDirectories);
			if (files.Count() > 0)
			{
				text = files.First();
			}
		}
		if (string.IsNullOrEmpty(text))
		{
			throw new Exception("Could not locate fxc.exe - Windows Kits/DirectX SDK/XDK installation not found.");
		}
		List<CompileThreadData> list = new List<CompileThreadData>();
		foreach (ShaderVersion requestedShaderVersion in RequestedShaderVersions)
		{
			foreach (ShaderLinkedSource value in requestedShaderVersion.LinkedSourceDuplicates.Values)
			{
				CompileThreadData item = new CompileThreadData(this, requestedShaderVersion, value, text);
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
			Platform_D3DCommon platform_D3DCommon = ctdata.This as Platform_D3DCommon;
			ShaderLinkedSource source = ctdata.Source;
			string exe = ctdata.Exe;
			ShaderVersion_D3DCommon shaderVersion_D3DCommon = ctdata.SVersion as ShaderVersion_D3DCommon;
			string text = Path.Combine(shaderVersion_D3DCommon.SourceDirectory, source.ID) + shaderVersion_D3DCommon.SourceExtension;
			if (!File.Exists(text))
			{
				StreamWriter streamWriter = File.CreateText(text);
				streamWriter.Write(source.SourceCode);
				streamWriter.Close();
			}
			string shaderProfile = shaderVersion_D3DCommon.GetShaderProfile(source.Pipeline);
			string text2 = "/nologo /E main /T " + shaderProfile + " /Fh \"" + text + "\".h /Vn pBinary_" + shaderVersion_D3DCommon.ID + "_" + source.ID + " " + platform_D3DCommon.D3DFXCExtraOptions + "  " + shaderVersion_D3DCommon.GetD3DFXCExtraOptions(exe, source) + " \"" + text + "\"";
			ctdata.ExitCode = launchProcess(exe, text2, out ctdata.StdOutput, out ctdata.StdError);
			ctdata.ShaderFilename = text;
			ctdata.CommandLine = exe + " " + text2;
		}
	}
}
