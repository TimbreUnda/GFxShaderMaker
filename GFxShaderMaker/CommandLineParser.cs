using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GFxShaderMaker;

public class CommandLineParser
{
	public enum Options
	{
		[CommandLineOption(CmdLineOptionType.OptionType_Action, "help", "", "Prints this help", typeof(HelpAction))]
		Help,
		[CommandLineOption(CmdLineOptionType.OptionType_Action, "list", "", "Lists known platforms", typeof(ListPlatformsAction))]
		ListPlatforms,
		[CommandLineOption(CmdLineOptionType.OptionType_String, "platform", "", "Selects the given platform (see -list).", null)]
		Platform,
		[CommandLineOption(CmdLineOptionType.OptionType_String, "type", "binary", "Selects an output type (binary or source).", null)]
		OutputType,
		[CommandLineOption(CmdLineOptionType.OptionType_String, "outdir", "./", "Selects an output directory.", null)]
		OutputDirectory,
		[CommandLineOption(CmdLineOptionType.OptionType_String, "libdir", "", "Location of binary shader library (if type=='binary')", null)]
		LibraryDirectory,
		[CommandLineOption(CmdLineOptionType.OptionType_String, "objdir", "", "Location of binary shader object files (if type=='binary')", null)]
		ObjectDirectory,
		[CommandLineOption(CmdLineOptionType.OptionType_String, "xml", "ShaderData.xml", "Input shader data file.", null)]
		SourceXML,
		[CommandLineOption(CmdLineOptionType.OptionType_String, "verbosity", "1", "Verbosity level (0=silent, 3=verbose).", null)]
		Verbosity,
		[CommandLineOption(CmdLineOptionType.OptionType_String, "config", "Release", "Build configuration type. May compile shaders with different optimization settings. Required on platforms that compile shaders directory to object files. (default='Release_NoRTTI')", null)]
		Config
	}

	private static CommandLineParser Instance = null;

	private static Dictionary<string, string> CommandLineOptions = new Dictionary<string, string>();

	public static void Initialize(string[] args)
	{
		Instance = new CommandLineParser();
		Instance.Parse(args);
	}

	public static bool GetFlag(Options opt)
	{
		return GetOption<bool>(opt.ToString());
	}

	public static string GetOption(Options opt)
	{
		return GetOption<string>(opt.ToString());
	}

	public static T GetOption<T>(Options opt)
	{
		return GetOption<T>(opt.ToString());
	}

	public static T GetOption<T>(string opt)
	{
		if (!CommandLineOptions.TryGetValue(opt, out var value))
		{
			CommandLineOptionAttribute commandLineOptionAttribute = typeof(Options).GetMember(opt.ToString())[0].GetCustomAttributes(typeof(CommandLineOptionAttribute), inherit: false)[0] as CommandLineOptionAttribute;
			value = commandLineOptionAttribute.DefaultValue;
		}
		return (T)Convert.ChangeType(value, typeof(T));
	}

	public void Parse(string[] args)
	{
		bool flag = false;
		FieldInfo[] fields = typeof(Options).GetFields();
		string text = Options.Help.ToString();
		List<Type> list = new List<Type>();
		list.Add(typeof(Options));
		CommandLineOptionAttribute platformAttr = typeof(Options).GetMember(Options.Platform.ToString())[0].GetCustomAttributes(typeof(CommandLineOptionAttribute), inherit: false)[0] as CommandLineOptionAttribute;
		IEnumerable<string> source = args.SkipWhile((string str) => !platformAttr.Match(str));
		if (source.Count() > 1)
		{
			string platformName = source.ElementAt(1);
			if (!string.IsNullOrEmpty(platformName))
			{
				ShaderPlatform shaderPlatform = ShaderPlatform.PlatformList.Find((ShaderPlatform plt) => plt.PlatformName == platformName);
				if (shaderPlatform == null)
				{
					throw new Exception("Unrecognized platform: " + platformName + " (use -list to see possible platforms).");
				}
				Type nestedType = shaderPlatform.GetType().GetNestedType("CommandLineOptions");
				if (nestedType != null && nestedType.IsEnum)
				{
					list.Add(nestedType);
				}
			}
		}
		string[] names;
		foreach (Type item in list)
		{
			names = Enum.GetNames(item);
			foreach (string text2 in names)
			{
				CommandLineOptionAttribute commandLineOptionAttribute = item.GetMember(text2.ToString())[0].GetCustomAttributes(typeof(CommandLineOptionAttribute), inherit: false)[0] as CommandLineOptionAttribute;
				CommandLineOptions.Add(text2, commandLineOptionAttribute.DefaultValue);
			}
		}
		CommandLineOptionAttribute commandLineOptionAttribute2 = null;
		names = args;
		foreach (string text3 in names)
		{
			if (commandLineOptionAttribute2 != null)
			{
				CommandLineOptions.Remove(text.ToString());
				CommandLineOptions.Add(text.ToString(), text3);
				commandLineOptionAttribute2 = null;
				continue;
			}
			foreach (Type item2 in list)
			{
				string[] names2 = Enum.GetNames(item2);
				foreach (string text2 in names2)
				{
					CommandLineOptionAttribute commandLineOptionAttribute = item2.GetMember(text2.ToString())[0].GetCustomAttributes(typeof(CommandLineOptionAttribute), inherit: false)[0] as CommandLineOptionAttribute;
					if (commandLineOptionAttribute.Match(text3) && commandLineOptionAttribute.Type != CmdLineOptionType.OptionType_Action)
					{
						if (commandLineOptionAttribute.Type != CmdLineOptionType.OptionType_Flag)
						{
							text = text2;
							commandLineOptionAttribute2 = commandLineOptionAttribute;
							break;
						}
						CommandLineOptions.Add(text2, bool.TrueString);
					}
				}
			}
			if (commandLineOptionAttribute2 != null)
			{
				continue;
			}
			foreach (Type item3 in list)
			{
				MemberInfo[] members = item3.GetMembers();
				foreach (MemberInfo memberInfo in members)
				{
					string text2 = memberInfo.GetType().Name;
					object[] customAttributes = memberInfo.GetCustomAttributes(typeof(CommandLineOptionAttribute), inherit: false);
					if (customAttributes.GetLength(0) == 0)
					{
						continue;
					}
					object[] array = customAttributes;
					foreach (object obj in array)
					{
						if (obj is CommandLineOptionAttribute commandLineOptionAttribute3 && commandLineOptionAttribute3.Match(text3) && commandLineOptionAttribute3.Type == CmdLineOptionType.OptionType_Action)
						{
							CommandLineAction commandLineAction = Activator.CreateInstance(commandLineOptionAttribute3.ActionType) as CommandLineAction;
							commandLineAction.Execute();
							flag = true;
						}
					}
				}
			}
		}
		if (!flag)
		{
			DefaultAction defaultAction = new DefaultAction();
			defaultAction.Execute();
		}
	}
}
