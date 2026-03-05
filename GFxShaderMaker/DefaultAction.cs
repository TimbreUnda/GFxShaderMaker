using System;
using System.IO;
using System.Xml;

namespace GFxShaderMaker;

public class DefaultAction : CommandLineAction
{
	public void Execute()
	{
		string platformName = CommandLineParser.GetOption(CommandLineParser.Options.Platform);
		if (string.IsNullOrEmpty(platformName))
		{
			throw new Exception("Platform must be provided (use -platform).");
		}
		ShaderPlatform shaderPlatform = ShaderPlatform.PlatformList.Find((ShaderPlatform plt) => plt.PlatformName == platformName);
		if (shaderPlatform == null)
		{
			throw new Exception("Unrecognized platform: " + platformName + " (use -list to see possible platforms).");
		}
		string option = CommandLineParser.GetOption(CommandLineParser.Options.SourceXML);
		if (string.IsNullOrEmpty(option) || !File.Exists(option))
		{
			throw new Exception("Invalid source XML, or does not exist: " + option + ". (use -xml to specify).");
		}
		XmlDocument xmlDocument = new XmlDocument();
		xmlDocument.Load(option);
		shaderPlatform.ReadFromXml(xmlDocument.DocumentElement);
		shaderPlatform.WriteHeaderFile();
		shaderPlatform.WriteSourceFile();
		shaderPlatform.WriteShaderSources();
		switch (CommandLineParser.GetOption(CommandLineParser.Options.OutputType).ToLower())
		{
		case "binary":
			shaderPlatform.CreateShaderOutput(ShaderPlatform.ShaderOutputType.Binary);
			break;
		case "source":
			shaderPlatform.CreateShaderOutput(ShaderPlatform.ShaderOutputType.Source);
			break;
		}
	}
}
