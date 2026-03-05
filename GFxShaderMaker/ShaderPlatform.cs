#define DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;

namespace GFxShaderMaker;

public abstract class ShaderPlatform
{
	public enum ShaderOutputType
	{
		Source,
		Binary
	}

	protected class CompileThreadData
	{
		public ShaderPlatform This;

		public ShaderVersion SVersion;

		public ShaderLinkedSource Source;

		public string Exe;

		public string ShaderFilename;

		public string CommandLine;

		public string StdOutput;

		public string StdError;

		public int ExitCode;

		public Exception Exception;

		public CompileThreadData(ShaderPlatform p, ShaderVersion v, ShaderLinkedSource s, string f)
		{
			This = p;
			SVersion = v;
			Source = s;
			Exe = f;
			StdOutput = "";
			StdError = "";
			ExitCode = 0;
			Exception = null;
		}
	}

	public static List<ShaderPlatform> PlatformList;

	public List<ShaderGroup> ShaderGroups;

	public Dictionary<string, uint> FeatureFlavorFlags;

	private static Dictionary<int, StringBuilder> launchProcessStdOuts;

	private static Dictionary<int, StringBuilder> launchProcessStdErrs;

	public virtual List<ShaderVersion> PossibleShaderVersions => RequestedShaderVersions;

	public virtual ShaderVersion DefaultShaderVersion => RequestedShaderVersions.First();

	public abstract List<ShaderVersion> RequestedShaderVersions { get; }

	public abstract IEnumerable<ShaderOutputType> SupportedOutputTypes { get; }

	private int MaximumSimultaneousCompileThreads => Environment.ProcessorCount + 1;

	public string PlatformName
	{
		get
		{
			PlatformAttribute platformAttribute = GetPlatformAttribute();
			if (platformAttribute == null)
			{
				return "Unknown";
			}
			return platformAttribute.Name;
		}
	}

	public virtual string PlatformBase => PlatformName;

	public string PlatformHeaderFilename => Path.Combine(CommandLineParser.GetOption(CommandLineParser.Options.OutputDirectory), PlatformName + "_ShaderDescs.h");

	public string PlatformSourceFilename => Path.Combine(CommandLineParser.GetOption(CommandLineParser.Options.OutputDirectory), PlatformName + "_ShaderDescs.cpp");

	public string PlatformBinarySourceFilename => Path.Combine(CommandLineParser.GetOption(CommandLineParser.Options.OutputDirectory), PlatformName + "_ShaderBinary.cpp");

	public string PlatformSourceSourceFilename => Path.Combine(CommandLineParser.GetOption(CommandLineParser.Options.OutputDirectory), PlatformName + "_ShaderSource.cpp");

	public string PlatformSourceDirectory => Path.Combine(CommandLineParser.GetOption(CommandLineParser.Options.OutputDirectory), "Shaders");

	public virtual string PlatformObjDirectory
	{
		get
		{
			string text = CommandLineParser.GetOption(CommandLineParser.Options.ObjectDirectory);
			if (string.IsNullOrEmpty(text))
			{
				text = Path.Combine(CommandLineParser.GetOption(CommandLineParser.Options.OutputDirectory), "..\\..\\..");
				text = Path.Combine(text, "Obj\\" + PlatformBase + ((PlatformCompiler.Length > 0) ? ("-" + PlatformCompiler) : "") + "\\" + CommandLineParser.GetOption(CommandLineParser.Options.Config));
				text = Path.Combine(text, "Src\\Render\\" + PlatformBase + "\\Shaders");
			}
			DirectoryInfo directoryInfo = new DirectoryInfo(text);
			return directoryInfo.FullName;
		}
	}

	public virtual string PlatformLibDirectory
	{
		get
		{
			string text = CommandLineParser.GetOption(CommandLineParser.Options.LibraryDirectory);
			if (string.IsNullOrEmpty(text))
			{
				text = Path.Combine(CommandLineParser.GetOption(CommandLineParser.Options.OutputDirectory), "..\\..\\..");
				text = Path.Combine(text, "Lib\\" + PlatformBase + "\\" + PlatformCompiler + "\\" + CommandLineParser.GetOption(CommandLineParser.Options.Config));
			}
			DirectoryInfo directoryInfo = new DirectoryInfo(text);
			return directoryInfo.FullName;
		}
	}

	public virtual string PlatformBinaryLibrary => Path.Combine(PlatformLibDirectory, "libgfxshaders.a");

	public virtual string PlatformCompiler => "";

	public List<ShaderVariable> UniqueUniformList
	{
		get
		{
			List<ShaderVariable> list = new List<ShaderVariable>();
			foreach (ShaderVersion requestedShaderVersion in RequestedShaderVersions)
			{
				list.AddRange(requestedShaderVersion.UniqueUniformList);
			}
			List<ShaderVariable> list2 = list.Distinct(new ShaderVariableCompareIDs()).ToList();
			list2.Sort();
			return list2;
		}
	}

	protected virtual string GetHeaderFileAdditionalIncludes => "";

	protected virtual string GetBinaryShaderDeclaration(ShaderPipeline pipeline)
	{
		throw new Exception("This method must be overridden for platforms that use binary shaders.");
	}

	protected virtual string GetBinaryShaderReference(ShaderPipeline pipeline, string ID)
	{
		throw new Exception("This method must be overridden for platforms that use binary shaders.");
	}

	protected virtual string GetBinaryShaderExtern(ShaderPipeline pipeline, string ID)
	{
		throw new Exception("This method must be overridden for platforms that use binary shaders.");
	}

	static ShaderPlatform()
	{
		launchProcessStdOuts = new Dictionary<int, StringBuilder>();
		launchProcessStdErrs = new Dictionary<int, StringBuilder>();
		LinkedList<ShaderPlatform> linkedList = new LinkedList<ShaderPlatform>();
		Type[] types = Assembly.GetExecutingAssembly().GetTypes();
		Type[] array = types;
		foreach (Type type in array)
		{
			object[] customAttributes = type.GetCustomAttributes(typeof(PlatformAttribute), inherit: true);
			if (customAttributes.GetLength(0) > 0)
			{
				ShaderPlatform value = (ShaderPlatform)Activator.CreateInstance(type);
				linkedList.AddFirst(value);
			}
		}
		PlatformList = linkedList.ToList();
	}

	public PlatformAttribute GetPlatformAttribute()
	{
		object[] customAttributes = GetType().GetCustomAttributes(typeof(PlatformAttribute), inherit: true);
		if (customAttributes.GetLength(0) > 0)
		{
			return customAttributes[0] as PlatformAttribute;
		}
		return null;
	}

	public void ReadFromXml(XmlElement root)
	{
		ShaderGroups = new List<ShaderGroup>();
		foreach (XmlElement item in root.GetElementsByTagName("ShaderGroup"))
		{
			ShaderGroup shaderGroup = new ShaderGroup();
			shaderGroup.ReadFromXml(item);
			ShaderGroups.Add(shaderGroup);
		}
		foreach (ShaderVersion requestedShaderVersion in RequestedShaderVersions)
		{
			requestedShaderVersion.ReadFromXml(root);
		}
		CreatePermutations();
	}

	private void CreatePermutations()
	{
		DetermineFeatureFlags();
		foreach (ShaderGroup shaderGroup in ShaderGroups)
		{
			shaderGroup.CreatePermutations(FeatureFlavorFlags);
		}
		foreach (ShaderVersion requestedShaderVersion in RequestedShaderVersions)
		{
			requestedShaderVersion.CreatePermutations();
		}
	}

	private void DetermineFeatureFlags()
	{
		FeatureFlavorFlags = new Dictionary<string, uint>();
		Dictionary<string, List<ShaderGroup>> dictionary = new Dictionary<string, List<ShaderGroup>>();
		List<string> list = new List<string>();
		foreach (ShaderGroup shaderGroup in ShaderGroups)
		{
			foreach (ShaderFeature feature in shaderGroup.Features)
			{
				bool flag = true;
				foreach (ShaderFeatureFlavor flavor in feature.Flavors)
				{
					if (flavor.ID != ShaderFeatureFlavor.EmptyID && !flag)
					{
						list.Add(flavor.ID);
						if (!dictionary.TryGetValue(flavor.ID, out var value))
						{
							value = new List<ShaderGroup>();
							dictionary.Add(flavor.ID, value);
						}
						value.Add(shaderGroup);
					}
					flag = false;
				}
			}
		}
		List<KeyValuePair<string, List<string>>> list2 = new List<KeyValuePair<string, List<string>>>();
		foreach (KeyValuePair<string, List<ShaderGroup>> item in dictionary.OrderByDescending((KeyValuePair<string, List<ShaderGroup>> pair) => pair.Value.Count))
		{
			list2.Add(new KeyValuePair<string, List<string>>(item.Key, new List<string>()));
			list2.Last().Value.Add(item.Key);
		}
		for (int num = 0; num < list2.Count; num++)
		{
			for (int num2 = list2.Count - 1; num2 > 0; num2--)
			{
				string key = list2[num].Key;
				string key2 = list2[num2].Key;
				if (dictionary[key].Intersect(dictionary[key2]).Count() == 0)
				{
					dictionary[key].AddRange(dictionary[key2]);
					dictionary.Remove(key2);
					list2[num].Value.AddRange(list2[num2].Value);
					list2.RemoveAt(num2);
					break;
				}
			}
		}
		uint num3 = 1u;
		foreach (KeyValuePair<string, List<string>> item2 in list2)
		{
			foreach (string item3 in item2.Value)
			{
				FeatureFlavorFlags.Add(item3, num3);
			}
			num3 <<= 1;
		}
	}

	public string CopyrightNotice(string filename)
	{
		return "/**************************************************************************\r\n\r\n    PublicHeader:   Render\r\n    Filename    :   " + Path.GetFileName(filename) + "\r\n    Content     :   " + PlatformName + " Shader descriptors\r\n    Created     :   " + DateTime.Today.Date.ToShortDateString() + "\r\n    Authors     :   Automatically generated.\r\n\r\n    Copyright   :   Copyright " + DateTime.Today.Year + " Autodesk, Inc. All Rights reserved.\r\n\r\n    Use of this software is subject to the terms of the Autodesk license\r\n    agreement provided at the time of installation or download, or which\r\n    otherwise accompanies this software in either electronic or hard copy form.\r\n\r\n**************************************************************************/\r\n\r\n";
	}

	public void WriteHeaderFile()
	{
		uint num = 1u;
		uint num2 = 0u;
		if (!Directory.Exists(Path.GetDirectoryName(PlatformHeaderFilename)))
		{
			Directory.CreateDirectory(Path.GetDirectoryName(PlatformHeaderFilename));
		}
		StreamWriter streamWriter = File.CreateText(PlatformHeaderFilename);
		IndentStreamWriter indentStreamWriter = new IndentStreamWriter(streamWriter);
		indentStreamWriter.Write(CopyrightNotice(PlatformHeaderFilename) + "\n");
		indentStreamWriter.Write("#ifndef INC_SF_Render_" + PlatformName + "_Shaders_H\n");
		indentStreamWriter.Write("#define INC_SF_Render_" + PlatformName + "_Shaders_H\n\n");
		indentStreamWriter.Write(GetHeaderFileAdditionalIncludes);
		indentStreamWriter.Write("#include \"Kernel/SF_Types.h\"\n");
		indentStreamWriter.Write("#include \"Kernel/SF_Debug.h\"\n");
		indentStreamWriter.Write("#if !defined(SF_BUILD_GFXSHADERMAKER)\n");
		indentStreamWriter.Write("// This define specifies we are using GFxShaderMaker.\n#define SF_BUILD_GFXSHADERMAKER\n");
		indentStreamWriter.Write("#endif\n\n");
		indentStreamWriter.Write("#if !defined(SF_RENDER_MAX_BATCHES)\n");
		indentStreamWriter.Write("#define SF_RENDER_MAX_BATCHES 24\n");
		indentStreamWriter.Write("#endif\n\n");
		indentStreamWriter.Write("namespace Scaleform { namespace Render { namespace " + PlatformBase + " {\n\n");
		indentStreamWriter.CurrentIndent = "";
		indentStreamWriter.Write("const static SInt64 SF_GFXSHADERMAKER_TIMESTAMP = " + DateTime.Now.ToBinary().ToString("D") + "LL;\n");
		indentStreamWriter.Write("struct Uniform\n");
		indentStreamWriter.Write("{\n");
		indentStreamWriter.Write("enum Flags\n");
		indentStreamWriter.Write("{\n");
		indentStreamWriter.Write("Uniform_Builtin  = 1,\n");
		indentStreamWriter.Write("Uniform_TexScale = 2,\n");
		indentStreamWriter.Write("};\n\n");
		indentStreamWriter.Write("enum UniformType\n");
		indentStreamWriter.Write("{\n");
		foreach (ShaderVariable uniqueUniform in UniqueUniformList)
		{
			indentStreamWriter.Write("SU_" + uniqueUniform.ID + ",\n");
		}
		indentStreamWriter.Write("\nSU_Count,\n\n");
		ShaderPipeline.PipelineType pipelineType;
		foreach (ShaderPipeline.PipelineType value in Enum.GetValues(typeof(ShaderPipeline.PipelineType)))
		{
			pipelineType = value;
			uint val = 0u;
			foreach (ShaderVersion requestedShaderVersion in RequestedShaderVersions)
			{
				ShaderPipeline pipeline = requestedShaderVersion.Pipelines.FirstOrDefault((ShaderPipeline p) => p.Type == pipelineType);
				if (pipeline == null)
				{
					continue;
				}
				val = Math.Max(val, requestedShaderVersion.LinkedSources.FindAll((ShaderLinkedSource src) => src.Pipeline.Type == pipeline.Type).DefaultIfEmpty().Max((ShaderLinkedSource src) => (uint)((src != null) ? src.VariableList.Sum((ShaderVariable v) => v.RegisterCount * 4) : 0u)));
			}
			string pipelineName = ShaderPipeline.GetPipelineName(pipelineType);
			indentStreamWriter.Write("SU_" + pipelineName + "Size = " + val + ",\n");
		}
		indentStreamWriter.Write("\n");
		indentStreamWriter.Write("SU_TotalSize = ");
		bool flag = true;
		foreach (ShaderPipeline.PipelineType value2 in Enum.GetValues(typeof(ShaderPipeline.PipelineType)))
		{
			if (!flag)
			{
				indentStreamWriter.Write(" + ");
			}
			flag = false;
			string pipelineName = ShaderPipeline.GetPipelineName(value2);
			indentStreamWriter.Write("SU_" + pipelineName + "Size");
		}
		indentStreamWriter.Write(",\n");
		indentStreamWriter.Write("};\n");
		indentStreamWriter.Write("static const unsigned char UniformFlags[]; \n");
		indentStreamWriter.Write("};\n\n");
		indentStreamWriter.Write("extern const char* ShaderUniformNames[Uniform::SU_Count];\n");
		indentStreamWriter.Write("enum ShaderFlags\n");
		indentStreamWriter.Write("{\n");
		List<string> list = new List<string>();
		foreach (ShaderGroup shaderGroup in ShaderGroups)
		{
			foreach (ShaderPermutation permutation in shaderGroup.Permutations)
			{
				foreach (ShaderFeatureFlavor specificFeature in permutation.SpecificFeatures)
				{
					list.AddRange(specificFeature.Flags);
					list.AddRange(specificFeature.PostLink);
				}
			}
		}
		list.Add("User");
		uint num3 = 1u;
		foreach (string item in list.Distinct())
		{
			indentStreamWriter.Write(("Shader_" + item + " = ").PadRight(30) + "0x" + num3.ToString("X8") + ",\n");
			num3 <<= 1;
		}
		indentStreamWriter.Write("};\n\n");
		indentStreamWriter.Write("enum ShaderStages\n");
		indentStreamWriter.Write("{\n");
		foreach (ShaderPipeline.PipelineType value3 in Enum.GetValues(typeof(ShaderPipeline.PipelineType)))
		{
			string pipelineName = ShaderPipeline.GetPipelineName(value3);
			indentStreamWriter.Write("    ShaderStage_" + pipelineName + ",\n");
		}
		indentStreamWriter.Write("     ShaderStage_Count,\n");
		indentStreamWriter.Write("};\n");
		indentStreamWriter.Write("struct UniformVar\n");
		indentStreamWriter.Write("{\n");
		indentStreamWriter.Write("short         Location;\n");
		indentStreamWriter.Write("short         ShadowOffset;\n");
		indentStreamWriter.Write("unsigned char ElementSize;\n");
		indentStreamWriter.Write("short         Size;\n");
		indentStreamWriter.Write("unsigned char ElementCount;\n");
		indentStreamWriter.Write("unsigned char BatchSize;\n");
		indentStreamWriter.Write("};\n\n");
		indentStreamWriter.Write("struct BatchVar\n");
		indentStreamWriter.Write("{\n");
		indentStreamWriter.Write("signed char   Array;\n");
		indentStreamWriter.Write("signed char   Offset;\n");
		indentStreamWriter.Write("unsigned char Size;\n");
		indentStreamWriter.Write("};\n\n");
		indentStreamWriter.Write("struct ShaderDesc\n");
		indentStreamWriter.Write("{\n");
		indentStreamWriter.Write("enum ShaderVersion\n");
		indentStreamWriter.Write("{\n");
		foreach (ShaderVersion possibleShaderVersion in PossibleShaderVersions)
		{
			indentStreamWriter.Write("ShaderVersion_" + possibleShaderVersion.ID + ",\n");
		}
		indentStreamWriter.Write("ShaderVersion_Count,\n");
		indentStreamWriter.Write("ShaderVersion_Default = ShaderVersion_" + DefaultShaderVersion.ID + "\n");
		indentStreamWriter.Write("};\n\n");
		uint num4 = 0u;
		uint num5 = 0u;
		foreach (ShaderGroup shaderGroup2 in ShaderGroups)
		{
			num5 = Math.Max(num5, shaderGroup2.FeatureFlavorFlags.Values.Max());
		}
		num5 <<= 1;
		indentStreamWriter.Write("enum ShaderType\n");
		indentStreamWriter.Write("{\n");
		indentStreamWriter.Write("ST_None = 0,\n\n");
		num = 1u;
		num2 = 1u;
		List<string> list2 = new List<string>();
		foreach (ShaderGroup shaderGroup3 in ShaderGroups)
		{
			foreach (string key in shaderGroup3.FeatureFlavorFlags.Keys)
			{
				indentStreamWriter.Write(("ST_" + shaderGroup3.ID + "_" + key).PadRight(40) + " = 0x" + shaderGroup3.FeatureFlavorFlags[key].ToString("X8") + ",\n");
			}
			indentStreamWriter.Write("\n");
			indentStreamWriter.Write("ST_start_" + shaderGroup3.ID + " = " + num + ",\n");
			shaderGroup3.Permutations.Sort();
			foreach (ShaderPermutation permutation2 in shaderGroup3.Permutations)
			{
				indentStreamWriter.Write("ST_" + permutation2.GetPermutationName());
				list2.Add(permutation2.GetPermutationName());
				uint num6 = permutation2.ShaderType + num;
				if (num2 + 1 != num6)
				{
					indentStreamWriter.Write(" = " + num6);
				}
				num2 = num6;
				indentStreamWriter.Write(",\n");
			}
			indentStreamWriter.Write("ST_end_" + shaderGroup3.ID + " = " + num2 + ",\n\n");
			num4 = num2;
			num = (num2 + num5) & ~(num5 - 1);
			num2 = 0u;
		}
		indentStreamWriter.Write("};\n\n");
		indentStreamWriter.Write("enum ShaderIndex\n");
		indentStreamWriter.Write("{\n");
		indentStreamWriter.Write("STI_None = 0,\n\n");
		foreach (string item2 in list2)
		{
			indentStreamWriter.Write("STI_" + item2 + ",\n");
		}
		indentStreamWriter.Write("STI_Count,\n");
		indentStreamWriter.Write("};\n");
		indentStreamWriter.Write("static bool        IsShaderVersionSupported(ShaderVersion ver);\n");
		indentStreamWriter.Write("static ShaderType  GetShaderTypeForComboIndex(unsigned comboIndex, ShaderVersion ver = ShaderVersion_Default);\n");
		indentStreamWriter.Write("static ShaderIndex GetShaderIndex(ShaderType type);\n");
		indentStreamWriter.Write("};\n\n");
		List<ShaderPipeline> UsedPipelineList = new List<ShaderPipeline>();
		RequestedShaderVersions.ForEach(delegate(ShaderVersion v)
		{
			UsedPipelineList.AddRange(v.Pipelines);
		});
		UsedPipelineList = UsedPipelineList.Distinct(new ShaderPipelineCompareTypes()).ToList();
		ShaderPipeline pipeline2;
		foreach (ShaderPipeline item3 in UsedPipelineList)
		{
			pipeline2 = item3;
			string pipelineName = pipeline2.Name;
			char letter = pipeline2.Letter;
			indentStreamWriter.Write("struct " + pipelineName + "ShaderDesc\n");
			indentStreamWriter.Write("{\n");
			indentStreamWriter.Write("enum ShaderIndex\n");
			indentStreamWriter.Write("{\n");
			indentStreamWriter.Write(letter + "SI_None = 0,\n\n");
			int num7 = 1;
			foreach (ShaderVersion requestedShaderVersion2 in RequestedShaderVersions)
			{
				foreach (List<ShaderLinkedSource> value4 in requestedShaderVersion2.LinkedSourceUniqueDescs.Values)
				{
					bool flag2 = false;
					foreach (ShaderLinkedSource item4 in value4.FindAll((ShaderLinkedSource s) => s.Pipeline.Type == pipeline2.Type))
					{
						indentStreamWriter.Write((letter + "SI_" + requestedShaderVersion2.ID + "_" + item4.ID).PadRight(50) + " = " + num7 + ",\n");
						flag2 = true;
					}
					if (flag2)
					{
						num7++;
					}
				}
			}
			indentStreamWriter.Write(letter + "SI_Count\n");
			indentStreamWriter.Write("};\n\n");
			indentStreamWriter.Write("ShaderDesc::ShaderType      Type;\n");
			indentStreamWriter.Write("ShaderDesc::ShaderVersion   Version;\n");
			indentStreamWriter.Write("unsigned                    Flags;\n");
			string[] array;
			switch (CommandLineParser.GetOption(CommandLineParser.Options.OutputType).ToLower())
			{
			case "binary":
			{
				string binaryShaderDeclaration = GetBinaryShaderDeclaration(pipeline2);
				array = binaryShaderDeclaration.Split("\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
				foreach (string text in array)
				{
					indentStreamWriter.Write(text + "\n");
				}
				break;
			}
			case "source":
				indentStreamWriter.Write("const char*     pSource;\n");
				break;
			}
			indentStreamWriter.Write("const UniformVar*          Uniforms;\n");
			indentStreamWriter.Write("const BatchVar*            BatchUniforms;\n\n");
			array = GeneratePipelineHeaderExtras(pipeline2).Split("\n".ToCharArray());
			foreach (string text in array)
			{
				indentStreamWriter.Write(text + "\n");
			}
			indentStreamWriter.Write("static const " + pipelineName + "ShaderDesc* Descs[" + letter + "SI_Count];\n");
			indentStreamWriter.Write("static const " + pipelineName + "ShaderDesc* GetDesc(ShaderDesc::ShaderType shader, ShaderDesc::ShaderVersion ver = ShaderDesc::ShaderVersion_Default);\n");
			indentStreamWriter.Write("static ShaderIndex GetShaderIndex(ShaderDesc::ShaderType shader, ShaderDesc::ShaderVersion ver = ShaderDesc::ShaderVersion_Default);\n");
			indentStreamWriter.Write("static unsigned    GetShaderComboIndex(ShaderDesc::ShaderType shader, ShaderDesc::ShaderVersion ver = ShaderDesc::ShaderVersion_Default);\n");
			indentStreamWriter.Write("static ShaderIndex GetShaderIndexForComboIndex(unsigned index, ShaderDesc::ShaderVersion ver = ShaderDesc::ShaderVersion_Default);\n");
			indentStreamWriter.Write("};\n\n");
		}
		indentStreamWriter.Write("static const unsigned UniqueShaderCombinations = " + RequestedShaderVersions.Max((ShaderVersion ver) => ver.LinkedSources.Max((ShaderLinkedSource s) => s.ShaderComboIndex) + 1) + ";\n\n");
		indentStreamWriter.Write("}}} // Scaleform::Render::" + PlatformBase + "\n\n");
		indentStreamWriter.Write("#endif\n");
		streamWriter.Close();
		if (CommandLineParser.GetOption<int>(CommandLineParser.Options.Verbosity) != 0)
		{
			Console.WriteLine("Wrote: " + PlatformHeaderFilename);
		}
	}

	public virtual void WriteSourceFile()
	{
		if (!Directory.Exists(Path.GetDirectoryName(PlatformSourceFilename)))
		{
			Directory.CreateDirectory(Path.GetDirectoryName(PlatformSourceFilename));
		}
		StreamWriter streamWriter = File.CreateText(PlatformSourceFilename);
		IndentStreamWriter indentStreamWriter = new IndentStreamWriter(streamWriter);
		indentStreamWriter.Write(CopyrightNotice(PlatformSourceFilename));
		indentStreamWriter.Write("#include \"Render/" + PlatformBase + "/" + PlatformBase + "_Shader.h\"\n");
		indentStreamWriter.Write("#include \"Render/" + PlatformBase + "/" + Path.GetFileName(PlatformHeaderFilename) + "\"\n\n");
		indentStreamWriter.Write("namespace Scaleform { namespace Render { namespace " + PlatformBase + " {\n\n");
		indentStreamWriter.CurrentIndent = "";
		indentStreamWriter.Write("const char* ShaderUniformNames[Uniform::SU_Count] = \n{\n");
		List<ShaderVariable> uniqueUniformList = UniqueUniformList;
		foreach (ShaderVariable item in uniqueUniformList)
		{
			indentStreamWriter.Write("\"" + item.ID + "\",\n");
		}
		indentStreamWriter.Write("};\n\n");
		indentStreamWriter.Write("const unsigned char Uniform::UniformFlags[Uniform::SU_Count] = \n{\n");
		foreach (ShaderVariable item2 in uniqueUniformList)
		{
			indentStreamWriter.Write("0,  // " + item2.ID + "\n");
		}
		indentStreamWriter.Write("};\n\n");
		uint num = 0u;
		Dictionary<int, string> unidict = new Dictionary<int, string>();
		Dictionary<int, string> unidict2 = new Dictionary<int, string>();
		indentStreamWriter.Write("bool ShaderDesc::IsShaderVersionSupported(ShaderVersion ver)\n");
		indentStreamWriter.Write("{\n");
		indentStreamWriter.Write("switch(ver)\n");
		indentStreamWriter.Write("{\n");
		foreach (ShaderVersion requestedShaderVersion in RequestedShaderVersions)
		{
			indentStreamWriter.Write("case ShaderVersion_" + requestedShaderVersion.ID + ": return true;\n");
		}
		indentStreamWriter.Write("default: return false;\n");
		indentStreamWriter.Write("}\n");
		indentStreamWriter.Write("};\n\n");
		indentStreamWriter.Write("ShaderDesc::ShaderType ShaderDesc::GetShaderTypeForComboIndex(unsigned comboIndex, ShaderVersion ver)\n");
		indentStreamWriter.Write("{\n");
		indentStreamWriter.Write("switch(ver)\n");
		indentStreamWriter.Write("{\n");
		foreach (ShaderVersion requestedShaderVersion2 in RequestedShaderVersions)
		{
			indentStreamWriter.Write("case ShaderDesc::ShaderVersion_" + requestedShaderVersion2.ID + ":\n");
			if (!requestedShaderVersion2.RequireShaderCombos)
			{
				indentStreamWriter.Write("{\n");
				indentStreamWriter.Write("SF_UNUSED(comboIndex);\n");
				indentStreamWriter.Write("SF_DEBUG_ASSERT(1, \"" + requestedShaderVersion2.ID + " indicated that ShaderCombo information was not required.\");\n");
				indentStreamWriter.Write("return ST_None;\n");
				indentStreamWriter.Write("}\n");
				continue;
			}
			indentStreamWriter.Write("switch(comboIndex)\n");
			indentStreamWriter.Write("{\n");
			Dictionary<uint, string> dictionary = new Dictionary<uint, string>();
			foreach (ShaderLinkedSource linkedSource in requestedShaderVersion2.LinkedSources)
			{
				if (!dictionary.TryGetValue(linkedSource.ShaderComboIndex, out var _))
				{
					dictionary.Add(linkedSource.ShaderComboIndex, linkedSource.ID.Remove(0, 1));
				}
			}
			List<uint> list = dictionary.Keys.ToList();
			list.Sort();
			foreach (uint item3 in list)
			{
				indentStreamWriter.Write("case " + item3 + ": return ST_" + dictionary[item3] + ";\n");
			}
			indentStreamWriter.Write("default: SF_DEBUG_ASSERT1(1, \"Invalid shader combo index provided (%d)\", comboIndex); return ST_None;\n");
			indentStreamWriter.Write("}\n");
		}
		indentStreamWriter.Write("default: SF_DEBUG_ASSERT1(1, \"Invalid shader platform provided (%d)\", ver); return ST_None;\n");
		indentStreamWriter.Write("}\n};\n\n");
		indentStreamWriter.Write("ShaderDesc::ShaderIndex ShaderDesc::GetShaderIndex(ShaderType type)\n{\n");
		indentStreamWriter.Write("switch(type)\n{\n");
		foreach (ShaderGroup shaderGroup in ShaderGroups)
		{
			foreach (ShaderPermutation permutation in shaderGroup.Permutations)
			{
				indentStreamWriter.Write("case ST_" + permutation.GetPermutationName() + ": " + ("return STI_" + permutation.GetPermutationName() + ";\n").PadLeft(60));
			}
		}
		indentStreamWriter.Write("default: SF_DEBUG_ASSERT1(1, \"Invalid ShaderType (%d)\", type);\n");
		indentStreamWriter.Write("}\n");
		indentStreamWriter.Write("return STI_None;\n");
		indentStreamWriter.Write("}\n\n");
		List<ShaderPipeline> UsedPipelineList = new List<ShaderPipeline>();
		RequestedShaderVersions.ForEach(delegate(ShaderVersion v)
		{
			UsedPipelineList.AddRange(v.Pipelines);
		});
		UsedPipelineList = UsedPipelineList.Distinct(new ShaderPipelineCompareTypes()).ToList();
		indentStreamWriter.Write("struct ShaderIndexEntry\n");
		indentStreamWriter.Write("{\n");
		foreach (ShaderPipeline item4 in UsedPipelineList)
		{
			indentStreamWriter.Write(item4.Name + "ShaderDesc::ShaderIndex " + item4.Name + "Index;\n");
		}
		indentStreamWriter.Write("};\n\n");
		indentStreamWriter.Write("static const ShaderIndexEntry ShaderIndexingData[ShaderDesc::STI_Count][ShaderDesc::ShaderVersion_Count] = \n{\n");
		indentStreamWriter.Write("{ // ST_None\n");
		foreach (ShaderVersion possibleShaderVersion in PossibleShaderVersions)
		{
			indentStreamWriter.Write("{ // ShaderVersion_" + possibleShaderVersion.ID + "\n");
			foreach (ShaderPipeline item5 in UsedPipelineList)
			{
				indentStreamWriter.Write(item5.Name + "ShaderDesc::" + item5.Letter + "SI_None");
				indentStreamWriter.Write((item5 != UsedPipelineList.Last()) ? ",\n" : "\n");
			}
			indentStreamWriter.Write("},\n");
		}
		indentStreamWriter.Write("},\n");
		foreach (ShaderGroup shaderGroup2 in ShaderGroups)
		{
			foreach (ShaderPermutation permutation2 in shaderGroup2.Permutations)
			{
				indentStreamWriter.Write("{ // ST_" + permutation2.GetPermutationName() + "\n");
				foreach (ShaderVersion possibleShaderVersion2 in PossibleShaderVersions)
				{
					indentStreamWriter.Write("{ // ShaderVersion_" + possibleShaderVersion2.ID + "\n");
					ShaderPipeline pipeline;
					foreach (ShaderPipeline item6 in UsedPipelineList)
					{
						pipeline = item6;
						string text = ((!permutation2.IsValid(possibleShaderVersion2) || permutation2.LinkedSources.Find((ShaderLinkedSource src) => src.Pipeline.Type == pipeline.Type) == null) ? (pipeline.Name + "ShaderDesc::" + pipeline.Letter + "SI_None") : (pipeline.Name + "ShaderDesc::" + pipeline.Letter + "SI_" + possibleShaderVersion2.ID + "_" + pipeline.Letter + permutation2.GetPermutationName()));
						indentStreamWriter.Write(text + ((pipeline != UsedPipelineList.Last()) ? ",\n" : "\n"));
					}
					indentStreamWriter.Write((possibleShaderVersion2 == RequestedShaderVersions.Last()) ? "}\n" : "},\n");
				}
				indentStreamWriter.Write("},\n");
			}
		}
		indentStreamWriter.Write("};\n");
		ShaderPipeline pipeline2;
		foreach (ShaderPipeline item7 in UsedPipelineList)
		{
			pipeline2 = item7;
			string name = pipeline2.Name;
			char c = name[0];
			foreach (ShaderVersion requestedShaderVersion3 in RequestedShaderVersions)
			{
				List<ShaderLinkedSource> list2 = new List<ShaderLinkedSource>();
				foreach (List<ShaderLinkedSource> value2 in requestedShaderVersion3.LinkedSourceUniqueDescs.Values)
				{
					if (value2.Count <= 0 || value2.First().Pipeline.Type != pipeline2.Type)
					{
						continue;
					}
					list2.Add(value2.First());
					switch (CommandLineParser.GetOption(CommandLineParser.Options.OutputType).ToLower())
					{
					case "binary":
					{
						string binaryShaderExtern = GetBinaryShaderExtern(pipeline2, requestedShaderVersion3.ID + "_" + value2.First().SourceCodeDuplicateID);
						string[] array = binaryShaderExtern.Split("\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
						foreach (string text2 in array)
						{
							indentStreamWriter.Write(text2 + "\n");
						}
						break;
					}
					case "source":
						indentStreamWriter.Write("extern const char* pSource_" + requestedShaderVersion3.ID + "_" + value2.First().SourceCodeDuplicateID + ";\n");
						break;
					}
				}
				indentStreamWriter.Write("\n");
				foreach (ShaderLinkedSource item8 in list2)
				{
					string uniformDefs = "";
					ulong num3 = GenerateUniformDefs(ref unidict, ref uniqueUniformList, item8, num, ref uniformDefs);
					if (uniformDefs != null)
					{
						indentStreamWriter.Write("UniformVar Uniforms_" + num3 + "[Uniform::SU_Count] = \n{\n");
						string[] array = uniformDefs.Split("\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
						foreach (string text2 in array)
						{
							indentStreamWriter.Write(text2 + "\n");
						}
						indentStreamWriter.Write("};\n\n");
					}
					num3 = GenerateBatchUniformDefs(ref unidict2, ref uniqueUniformList, item8, ref uniformDefs);
					if (uniformDefs != null)
					{
						indentStreamWriter.Write("BatchVar BatchUniforms_" + num3 + "[Uniform::SU_Count] = \n{\n");
						string[] array = uniformDefs.Split("\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
						foreach (string text2 in array)
						{
							indentStreamWriter.Write(text2 + "\n");
						}
						indentStreamWriter.Write("};\n\n");
					}
				}
				foreach (ShaderLinkedSource item9 in list2)
				{
					indentStreamWriter.Write("static " + name + "ShaderDesc ShaderDesc_" + c + "S_" + requestedShaderVersion3.ID + "_" + item9.ID + " = \n{\n");
					indentStreamWriter.Write("/* Type */          ShaderDesc::ST_" + item9.ID.Remove(0, 1) + ",\n");
					indentStreamWriter.Write("/* Version */       ShaderDesc::ShaderVersion_" + requestedShaderVersion3.ID + ",\n");
					indentStreamWriter.Write("/* Flags */         ");
					if (item9.Flags.Count == 0)
					{
						indentStreamWriter.Write("0,\n");
					}
					else
					{
						bool flag = true;
						foreach (string flag3 in item9.Flags)
						{
							if (!flag)
							{
								indentStreamWriter.Write(" | ");
							}
							indentStreamWriter.Write("Shader_" + flag3);
						}
						indentStreamWriter.Write(",\n");
					}
					string[] array;
					switch (CommandLineParser.GetOption(CommandLineParser.Options.OutputType).ToLower())
					{
					case "binary":
					{
						string binaryShaderReference = GetBinaryShaderReference(pipeline2, requestedShaderVersion3.ID + "_" + item9.SourceCodeDuplicateID);
						array = binaryShaderReference.Split("\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
						foreach (string text2 in array)
						{
							indentStreamWriter.Write("/* pBinary */       " + text2 + "\n");
						}
						break;
					}
					case "source":
						indentStreamWriter.Write("/* pSource */       pSource_" + requestedShaderVersion3.ID + "_" + item9.SourceCodeDuplicateID + ",\n");
						break;
					}
					string uniformDefs = "";
					indentStreamWriter.Write("/* Uniforms */      Uniforms_" + GenerateUniformDefs(ref unidict, ref uniqueUniformList, item9, num, ref uniformDefs) + ",\n");
					indentStreamWriter.Write("/* BatchUniforms */ BatchUniforms_" + GenerateBatchUniformDefs(ref unidict2, ref uniqueUniformList, item9, ref uniformDefs) + ",\n");
					array = GeneratePipelineSourceExtras(requestedShaderVersion3, pipeline2, item9).Split("\n".ToCharArray());
					foreach (string text2 in array)
					{
						indentStreamWriter.Write(text2 + "\n");
					}
					indentStreamWriter.Write("};\n\n");
				}
			}
			indentStreamWriter.Write("const " + name + "ShaderDesc* " + name + "ShaderDesc::Descs[" + c + "SI_Count] = {\n");
			indentStreamWriter.Write("0,\n");
			int num4 = 1;
			foreach (ShaderVersion requestedShaderVersion4 in RequestedShaderVersions)
			{
				foreach (List<ShaderLinkedSource> value3 in requestedShaderVersion4.LinkedSourceUniqueDescs.Values)
				{
					ShaderLinkedSource current3 = value3.FindAll((ShaderLinkedSource s) => s.Pipeline.Type == pipeline2.Type).FirstOrDefault();
					if (current3 != null)
					{
						indentStreamWriter.Write(("&ShaderDesc_" + c + "S_" + requestedShaderVersion4.ID + "_" + current3.ID + ",").PadRight(60) + " // " + num4 + "\n");
						num4++;
					}
				}
			}
			indentStreamWriter.Write("};\n\n");
			indentStreamWriter.Write("const " + name + "ShaderDesc* " + name + "ShaderDesc::GetDesc(ShaderDesc::ShaderType shader, ShaderDesc::ShaderVersion ver)\n");
			indentStreamWriter.Write("{\n");
			indentStreamWriter.Write("return Descs[GetShaderIndex(shader, ver)];\n");
			indentStreamWriter.Write("};\n\n");
			indentStreamWriter.Write(name + "ShaderDesc::ShaderIndex " + name + "ShaderDesc::GetShaderIndex(ShaderDesc::ShaderType shader, ShaderDesc::ShaderVersion ver)\n{\n");
			indentStreamWriter.Write("ShaderDesc::ShaderIndex index = ShaderDesc::GetShaderIndex(shader);\n");
			indentStreamWriter.Write("return ShaderIndexingData[index][ver]. " + name + "Index;\n");
			indentStreamWriter.Write("};\n\n");
			indentStreamWriter.Write(name + "ShaderDesc::ShaderIndex " + name + "ShaderDesc::GetShaderIndexForComboIndex(unsigned index, ShaderDesc::ShaderVersion ver) \n");
			indentStreamWriter.Write("{\n");
			indentStreamWriter.Write("switch(ver)\n");
			indentStreamWriter.Write("{\n");
			foreach (ShaderVersion requestedShaderVersion5 in RequestedShaderVersions)
			{
				indentStreamWriter.Write("case ShaderDesc::ShaderVersion_" + requestedShaderVersion5.ID + ":\n");
				if (!requestedShaderVersion5.RequireShaderCombos)
				{
					indentStreamWriter.Write("{\n");
					indentStreamWriter.Write("SF_UNUSED(index);\n");
					indentStreamWriter.Write("SF_DEBUG_ASSERT(1, \"" + requestedShaderVersion5.ID + " indicated that ShaderCombo information was not required.\");\n");
					indentStreamWriter.Write("return " + c + "SI_None;\n");
					indentStreamWriter.Write("}\n");
					continue;
				}
				indentStreamWriter.Write("switch(index)\n");
				indentStreamWriter.Write("{\n");
				uint comboIndex = 0u;
				while (true)
				{
					ShaderLinkedSource current3 = requestedShaderVersion5.LinkedSources.Find((ShaderLinkedSource s) => s.ShaderComboIndex == comboIndex && s.Pipeline.Type == pipeline2.Type);
					if (current3 == null)
					{
						break;
					}
					indentStreamWriter.Write(("case " + comboIndex + ": ").PadRight(20) + "return " + c + "SI_" + requestedShaderVersion5.ID + "_" + current3.ID + ";\n");
					comboIndex++;
					bool flag2 = true;
				}
				indentStreamWriter.Write("default: SF_ASSERT(0); return " + c + "SI_None;\n");
				indentStreamWriter.Write("}\n");
			}
			indentStreamWriter.Write("default: SF_ASSERT(0); return " + c + "SI_None;\n");
			indentStreamWriter.Write("}\n");
			indentStreamWriter.Write("}\n\n");
			indentStreamWriter.Write("unsigned " + name + "ShaderDesc::GetShaderComboIndex(ShaderDesc::ShaderType shader, ShaderDesc::ShaderVersion ver)\n");
			indentStreamWriter.Write("{\n");
			indentStreamWriter.Write("switch(ver)\n");
			indentStreamWriter.Write("{\n");
			foreach (ShaderVersion requestedShaderVersion6 in RequestedShaderVersions)
			{
				indentStreamWriter.Write("case ShaderDesc::ShaderVersion_" + requestedShaderVersion6.ID + ":\n");
				if (!requestedShaderVersion6.RequireShaderCombos)
				{
					indentStreamWriter.Write("{\n");
					indentStreamWriter.Write("SF_UNUSED(shader);\n");
					indentStreamWriter.Write("SF_DEBUG_ASSERT(1, \"" + requestedShaderVersion6.ID + " indicated that ShaderCombo information was not required.\");\n");
					indentStreamWriter.Write("return 0;\n");
					indentStreamWriter.Write("}\n");
					continue;
				}
				indentStreamWriter.Write("switch(shader)\n");
				indentStreamWriter.Write("{\n");
				foreach (ShaderGroup shaderGroup3 in ShaderGroups)
				{
					shaderGroup3.Permutations.Sort();
					ShaderPermutation permu;
					foreach (ShaderPermutation permutation3 in shaderGroup3.Permutations)
					{
						permu = permutation3;
						foreach (ShaderLinkedSource item10 in requestedShaderVersion6.LinkedSources.FindAll((ShaderLinkedSource s) => s.ID.Remove(0, 1) == permu.GetPermutationName() && s.Pipeline.Type == pipeline2.Type))
						{
							indentStreamWriter.Write("case " + ("ShaderDesc::ST_" + permu.GetPermutationName() + ": ").PadRight(60) + "return " + item10.ShaderComboIndex + ";\n");
						}
					}
				}
				indentStreamWriter.Write("default: SF_ASSERT(0); return 0;\n");
				indentStreamWriter.Write("}\n");
			}
			indentStreamWriter.Write("default: SF_ASSERT(0); return 0;\n");
			indentStreamWriter.Write("}\n");
			indentStreamWriter.Write("}\n\n");
			num += RequestedShaderVersions.Max((ShaderVersion ver) => ver.LinkedSources.FindAll((ShaderLinkedSource src) => src.Pipeline.Type == pipeline2.Type).DefaultIfEmpty().Max((ShaderLinkedSource src) => (uint)((src != null) ? src.VariableList.Sum((ShaderVariable v) => v.RegisterCount * 4) : 0u)));
		}
		indentStreamWriter.Write("}}} // Scaleform::Render::" + PlatformBase + "\n\n");
		streamWriter.Close();
		if (CommandLineParser.GetOption<int>(CommandLineParser.Options.Verbosity) != 0)
		{
			Console.WriteLine("Wrote: " + PlatformSourceFilename);
		}
	}

	private ulong GenerateUniformDefs(ref Dictionary<int, string> unidict, ref List<ShaderVariable> uniqueUniformList, ShaderLinkedSource src, uint shadowOffsetStart, ref string uniformDefs)
	{
		uniformDefs = "";
		ShaderVariable var;
		foreach (ShaderVariable item in uniqueUniformList.Distinct())
		{
			var = item;
			uniformDefs = uniformDefs + "/* " + var.ID + "*/".PadRight(13 - var.ID.Length);
			ShaderVariable shaderVariable = src.VariableList.Find((ShaderVariable v) => v.ID == var.ID && v.VarType == ShaderVariable.VariableType.Variable_Uniform);
			if (shaderVariable == null)
			{
				uniformDefs += "{ -1, 0, 0, 0, 0, 0 },\n";
				continue;
			}
			List<ShaderVariable> source = src.VariableList.FindAll((ShaderVariable v) => Regex.IsMatch(v.Semantic, "^" + var.ID));
			uint num = (uint)source.Sum((ShaderVariable v) => v.ArraySize);
			string text = uniformDefs;
			uniformDefs = text + "{" + shaderVariable.BaseRegister + ", " + (shadowOffsetStart + shaderVariable.BaseRegister * 4) + ", " + shaderVariable.ElementCount + ", " + shaderVariable.Size + ", " + shaderVariable.ElementCount + ", " + num + " },\n";
		}
		int hashCode = uniformDefs.GetHashCode();
		if (!unidict.ContainsKey(hashCode))
		{
			unidict.Add(hashCode, uniformDefs);
		}
		else
		{
			uniformDefs = null;
		}
		return Convert.ToUInt64(hashCode + uint.MaxValue);
	}

	private ulong GenerateBatchUniformDefs(ref Dictionary<int, string> unidict, ref List<ShaderVariable> uniqueUniformList, ShaderLinkedSource src, ref string batchUniformDefs)
	{
		batchUniformDefs = "";
		ShaderVariable var;
		foreach (ShaderVariable item in uniqueUniformList.Distinct())
		{
			var = item;
			batchUniformDefs = batchUniformDefs + "/* " + var.ID + "*/".PadRight(13 - var.ID.Length);
			ShaderVariable shaderVariable = src.VariableList.Find((ShaderVariable v) => v.ID == var.ID && v.VarType == ShaderVariable.VariableType.Variable_VirtualUniform);
			if (shaderVariable == null)
			{
				batchUniformDefs += " {Uniform::SU_Count, -1, 0},\n";
				continue;
			}
			string[] array = shaderVariable.Semantic.Split(":".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
			object obj = batchUniformDefs;
			batchUniformDefs = string.Concat(obj, " {Uniform::SU_", array[0], ", ", array[1], ", ", shaderVariable.ArraySize, "},\n");
		}
		int hashCode = batchUniformDefs.GetHashCode();
		if (!unidict.ContainsKey(hashCode))
		{
			unidict.Add(hashCode, batchUniformDefs);
		}
		else
		{
			batchUniformDefs = null;
		}
		return Convert.ToUInt64(hashCode + uint.MaxValue);
	}

	public virtual string GeneratePipelineHeaderExtras(ShaderPipeline pipeline)
	{
		string text = "";
		switch (pipeline.Type)
		{
		case ShaderPipeline.PipelineType.Vertex:
		{
			int num2 = 0;
			foreach (ShaderVersion requestedShaderVersion in RequestedShaderVersions)
			{
				foreach (List<ShaderLinkedSource> value in requestedShaderVersion.LinkedSourceUniqueDescs.Values)
				{
					num2 = Math.Max(num2, value.Max((ShaderLinkedSource src) => src.VariableList.FindAll((ShaderVariable v) => v.VarType == ShaderVariable.VariableType.Variable_Attribute || v.VarType == ShaderVariable.VariableType.Variable_VirtualAttribute).Count));
				}
			}
			text += "struct VertexAttrDesc\n";
			text += "{\n";
			text += "    const char*   Name;\n";
			text += "    unsigned      Attr;\n";
			text += "};\n\n";
			text += "char           NumAttribs;\n";
			text += "enum {\n";
			object obj = text;
			text = string.Concat(obj, "    MaxVertexAttributes = ", num2, "\n");
			text += "};\n";
			text += "VertexAttrDesc Attributes[MaxVertexAttributes];\n";
			break;
		}
		case ShaderPipeline.PipelineType.Fragment:
		{
			int num = 0;
			foreach (ShaderVersion requestedShaderVersion2 in RequestedShaderVersions)
			{
				foreach (List<ShaderLinkedSource> value2 in requestedShaderVersion2.LinkedSourceUniqueDescs.Values)
				{
					num = Math.Max(num, value2.Max((ShaderLinkedSource src) => src.VariableList.FindAll((ShaderVariable v) => v.Type.Contains("sampler")).Sum((ShaderVariable var) => (int)((var.ArraySize == 0) ? 1 : var.ArraySize))));
				}
			}
			object obj = text;
			text = string.Concat(obj, "enum { MaxTextureSamplers = ", num, " };\n");
			break;
		}
		}
		return text;
	}

	public virtual string GeneratePipelineSourceExtras(ShaderVersion ver, ShaderPipeline pipeline, ShaderLinkedSource src)
	{
		string text = "";
		string text2 = "";
		if (pipeline.Type == ShaderPipeline.PipelineType.Vertex)
		{
			List<ShaderVariable> list = src.VariableList.FindAll((ShaderVariable v) => v.VarType == ShaderVariable.VariableType.Variable_Attribute);
			text = text + "/* NumAttribs */    " + list.Count + ",\n";
			text += "/* Attributes */    {\n";
			text2 += "                      ";
			foreach (ShaderVariable item in list)
			{
				string text3 = "VET_Color";
				string semantic = item.Semantic;
				text3 = Regex.Replace(semantic, "\\d+$", "") switch
				{
					"COLOR" => "VET_Color", 
					"FACTOR" => "VET_Color | (1 << VET_Index_Shift)", 
					"TEXCOORD" => "VET_TexCoord", 
					"INSTANCE" => "VET_Instance8", 
					_ => "VET_Pos", 
				};
				object obj = text;
				text = string.Concat(obj, "{ \"", item.ID, "\", ".PadRight(13 - item.ID.Length), item.ElementCount, " | ", text3, "},\n");
			}
			text += "},\n";
			text2 = text2.Remove(0, 22);
		}
		return text;
	}

	public virtual void WriteShaderSources()
	{
		if (!Directory.Exists(PlatformSourceDirectory))
		{
			Directory.CreateDirectory(PlatformSourceDirectory);
		}
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		foreach (ShaderVersion requestedShaderVersion in RequestedShaderVersions)
		{
			foreach (ShaderLinkedSource value2 in requestedShaderVersion.LinkedSourceDuplicates.Values)
			{
				string text = Path.Combine(PlatformSourceDirectory, value2.ID + requestedShaderVersion.SourceExtension);
				if (dictionary.TryGetValue(text, out var _))
				{
					throw new Exception("Two shader sources share the same filename (" + text + ")");
				}
				StreamWriter streamWriter = File.CreateText(text);
				streamWriter.Write(value2.SourceCode);
				streamWriter.Close();
			}
		}
	}

	public void CreateBinarySource()
	{
		if (!Directory.Exists(Path.GetDirectoryName(PlatformBinarySourceFilename)))
		{
			Directory.CreateDirectory(Path.GetDirectoryName(PlatformBinarySourceFilename));
		}
		File.Delete(PlatformBinarySourceFilename);
		StreamWriter streamWriter = File.CreateText(PlatformBinarySourceFilename);
		streamWriter.Write(CopyrightNotice(PlatformBinarySourceFilename) + "\n");
		WriteBinarySource(streamWriter);
		streamWriter.Close();
		if (CommandLineParser.GetOption<int>(CommandLineParser.Options.Verbosity) != 0)
		{
			Console.WriteLine("Wrote: " + PlatformBinarySourceFilename);
		}
	}

	protected virtual void WriteBinarySource(StreamWriter sourceFile)
	{
		foreach (ShaderVersion requestedShaderVersion in RequestedShaderVersions)
		{
			requestedShaderVersion.WriteBinaryShaderSource(sourceFile);
		}
	}

	public void CreateSourceSource()
	{
		File.Delete(PlatformSourceSourceFilename);
		StreamWriter streamWriter = File.CreateText(PlatformSourceSourceFilename);
		streamWriter.Write(CopyrightNotice(PlatformSourceSourceFilename) + "\n");
		streamWriter.WriteLine("namespace Scaleform { namespace Render { namespace " + PlatformBase + " {\n\n");
		foreach (ShaderVersion requestedShaderVersion in RequestedShaderVersions)
		{
			foreach (ShaderLinkedSource value in requestedShaderVersion.LinkedSourceDuplicates.Values)
			{
				string path = Path.Combine(PlatformSourceDirectory, value.ID + requestedShaderVersion.SourceExtension);
				StreamReader streamReader = File.OpenText(path);
				string text = streamReader.ReadToEnd();
				streamWriter.Write("extern const char* pSource_" + requestedShaderVersion.ID + "_" + value.ID + ";\n");
				streamWriter.Write("const char* pSource_" + requestedShaderVersion.ID + "_" + value.ID + " = ");
				string[] array = text.Split("\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
				foreach (string text2 in array)
				{
					string text3 = text2.Trim();
					if (text3.Length != 0)
					{
						streamWriter.Write("\n\"" + text3 + "\\n\"");
					}
				}
				streamWriter.Write(";\n\n");
				streamReader.Close();
			}
		}
		streamWriter.WriteLine("}}}; // Scaleform::Render::" + PlatformBase + "\n\n");
		streamWriter.Close();
		if (CommandLineParser.GetOption<int>(CommandLineParser.Options.Verbosity) != 0)
		{
			Console.WriteLine("Wrote: " + PlatformSourceSourceFilename);
		}
	}

	public abstract void CreateShaderOutput(ShaderOutputType type);

	protected virtual void CompileSingleShaderImpl(CompileThreadData data)
	{
		throw new Exception("Platform does not support compiling binary shaders.");
	}

	protected int launchProcess(string executable, string args, out string stdout, out string stderr)
	{
		Process process = new Process();
		process.StartInfo.FileName = executable;
		process.StartInfo.Arguments = args;
		process.StartInfo.ErrorDialog = false;
		process.StartInfo.CreateNoWindow = true;
		process.StartInfo.UseShellExecute = false;
		process.StartInfo.RedirectStandardError = true;
		process.StartInfo.RedirectStandardOutput = true;
		StringBuilder stringBuilder = new StringBuilder();
		StringBuilder stringBuilder2 = new StringBuilder();
		if (CommandLineParser.GetOption<int>(CommandLineParser.Options.Verbosity) >= 2)
		{
			Console.WriteLine(executable + " " + args);
		}
		process.OutputDataReceived += proc_OutputDataReceived;
		process.ErrorDataReceived += proc_ErrorDataReceived;
		process.Start();
		lock (launchProcessStdOuts)
		{
			launchProcessStdOuts.Add(process.Id, stringBuilder);
		}
		lock (launchProcessStdErrs)
		{
			launchProcessStdErrs.Add(process.Id, stringBuilder2);
		}
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();
		process.WaitForExit();
		stdout = stringBuilder.ToString();
		stderr = stringBuilder2.ToString();
		lock (launchProcessStdOuts)
		{
			launchProcessStdOuts.Remove(process.Id);
		}
		lock (launchProcessStdErrs)
		{
			launchProcessStdErrs.Remove(process.Id);
		}
		return process.ExitCode;
	}

	private static void proc_OutputDataReceived(object sender, DataReceivedEventArgs e)
	{
		Process process = sender as Process;
		if (e.Data == null)
		{
			return;
		}
		lock (launchProcessStdOuts)
		{
			if (launchProcessStdOuts.TryGetValue(process.Id, out var value))
			{
				value.Append(e.Data + "\n");
			}
		}
	}

	private static void proc_ErrorDataReceived(object sender, DataReceivedEventArgs e)
	{
		Process process = sender as Process;
		if (e.Data == null)
		{
			return;
		}
		lock (launchProcessStdErrs)
		{
			if (launchProcessStdErrs.TryGetValue(process.Id, out var value))
			{
				value.Append(e.Data + "\n");
			}
		}
	}

	protected void CompileSingleShader(object data)
	{
		CompileThreadData compileThreadData = data as CompileThreadData;
		try
		{
			compileThreadData.This.CompileSingleShaderImpl(compileThreadData);
		}
		catch (Exception exception)
		{
			if (compileThreadData != null)
			{
				compileThreadData.Exception = exception;
			}
		}
	}

	protected void CompileShadersThreaded(List<CompileThreadData> threadData)
	{
		List<Thread> list = new List<Thread>();
		foreach (CompileThreadData threadDatum in threadData)
		{
			Thread thread = new Thread(CompileSingleShader);
			thread.Start(threadDatum);
			list.Add(thread);
			foreach (Thread item in list)
			{
				if (!item.IsAlive)
				{
					list.Remove(item);
					break;
				}
			}
			if (list.Count > MaximumSimultaneousCompileThreads)
			{
				list[0].Join();
				Debug.Assert(!list[0].IsAlive);
				list.RemoveAt(0);
			}
		}
		while (list.Count > 0)
		{
			list[0].Join();
			Debug.Assert(!list[0].IsAlive);
			list.RemoveAt(0);
		}
		bool flag = false;
		foreach (CompileThreadData threadDatum2 in threadData)
		{
			if (threadDatum2.Exception != null)
			{
				throw threadDatum2.Exception;
			}
			if (CommandLineParser.GetOption<int>(CommandLineParser.Options.Verbosity) >= 2)
			{
				Console.WriteLine("Compiling Shader: " + threadDatum2.ShaderFilename);
				if (CommandLineParser.GetOption<int>(CommandLineParser.Options.Verbosity) >= 3)
				{
					Console.WriteLine(threadDatum2.CommandLine);
					Console.WriteLine(threadDatum2.StdOutput);
				}
			}
			if (threadDatum2.ExitCode != 0)
			{
				Console.WriteLine(threadDatum2.ShaderFilename + "(1) : error compiling shader");
				Console.WriteLine(threadDatum2.StdError);
				Console.WriteLine(threadDatum2.StdOutput);
				flag = true;
			}
		}
		if (flag)
		{
			throw new Exception("Error encountered while compiling shaders.");
		}
	}

	public static List<string> SplitStringToList(string attrName, XmlElement root, XmlElement feature, string delimiter = ";")
	{
		List<string> list = new List<string>();
		string attribute = root.GetAttribute(attrName);
		if (string.IsNullOrEmpty(attribute) && feature != null)
		{
			attribute = feature.GetAttribute(attrName);
		}
		if (!string.IsNullOrEmpty(attribute))
		{
			list.AddRange(attribute.Split(delimiter.ToCharArray(), StringSplitOptions.RemoveEmptyEntries));
		}
		return list;
	}

	protected List<ShaderVersion> ExtractPossibleVersions(Type versionEnumeration, string opt)
	{
		List<ShaderVersion> list = new List<ShaderVersion>();
		string[] array = ((opt == null) ? Enum.GetNames(versionEnumeration) : opt.Split(",".ToCharArray()));
		string[] array2 = array;
		foreach (string text in array2)
		{
			object obj = Enum.Parse(versionEnumeration, text);
			if (Enum.IsDefined(versionEnumeration, obj))
			{
				ShaderVersionAttribute shaderVersionAttribute = versionEnumeration.GetMember(obj.ToString())[0].GetCustomAttributes(typeof(ShaderVersionAttribute), inherit: false)[0] as ShaderVersionAttribute;
				ShaderVersion item = (ShaderVersion)Activator.CreateInstance(shaderVersionAttribute.ShaderVersion, this);
				list.Add(item);
				continue;
			}
			throw new Exception("Unknown shader version specified: " + text);
		}
		return list;
	}
}
