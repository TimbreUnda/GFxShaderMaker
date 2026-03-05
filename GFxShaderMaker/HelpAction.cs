using System;
using System.Reflection;

namespace GFxShaderMaker;

public class HelpAction : CommandLineAction
{
	public void Execute()
	{
		FieldInfo[] fields = typeof(CommandLineParser.Options).GetFields();
		foreach (FieldInfo fieldInfo in fields)
		{
			object[] customAttributes = fieldInfo.GetCustomAttributes(typeof(CommandLineOptionAttribute), inherit: true);
			for (int j = 0; j < customAttributes.Length; j++)
			{
				CommandLineOptionAttribute commandLineOptionAttribute = (CommandLineOptionAttribute)customAttributes[j];
				Console.WriteLine("-{0,-10} : {1}", commandLineOptionAttribute.CommandFlag, commandLineOptionAttribute.Description);
			}
		}
		Console.WriteLine("");
	}
}
