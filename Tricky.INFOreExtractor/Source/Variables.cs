using System;
using System.IO;
using System.Reflection;
using System.Threading;

internal class Variables
{
	public static bool ModDebug = true;

	public static string FCEModPathOLD = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\ProjectorGames\\FortressCraft\\Mods\\ModLog";

	public static string FCEModPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\ModLog\\";

	public static string[] FCEModPath_split = Variables.FCEModPath.Split('\\');

	public static int FCEModPath_length = Variables.FCEModPath_split.Length - 1;

	public static string ModName = Path.GetFileName(Assembly.GetExecutingAssembly().Location).Split('.')[1];

	public static string Author = Path.GetFileName(Assembly.GetExecutingAssembly().Location).Split('.')[0].Split('_')[1];

	public static string ModVersion = Variables.FCEModPath_split[Variables.FCEModPath_length - 2];

	public static string LogFilePath = Variables.FCEModPath + "ModLog.log";

	public static string PreString;

	private static object locker;

	public static void Start()
	{
		if (Directory.Exists(Variables.FCEModPathOLD))
		{
			Directory.Delete(Variables.FCEModPathOLD, true);
		}
		if (!Directory.Exists(Variables.FCEModPath))
		{
			Directory.CreateDirectory(Variables.FCEModPath);
		}
		Variables.DelteLogFile();
		Variables.PrintLine();
		Variables.LogPlain("[" + Variables.ModName + "] Loaded!");
		Variables.LogPlain("Mod created by " + Variables.Author + "!");
		Variables.LogPlain("Version " + Variables.ModVersion + " loaded!");
		Variables.PrintLine();
	}

	public static void Log(object debug)
	{
		object[] obj = new object[13]
		{
			"[",
			Variables.ModName,
			"][V",
			Variables.ModVersion,
			"][",
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null
		};
		DateTime now = DateTime.Now;
		obj[5] = now.Hour;
		obj[6] = ":";
		now = DateTime.Now;
		obj[7] = now.Minute;
		obj[8] = ":";
		now = DateTime.Now;
		obj[9] = now.Second;
		obj[10] = ".";
		now = DateTime.Now;
		obj[11] = now.Millisecond;
		obj[12] = "]";
		Variables.PreString = string.Concat(obj);
		if (Variables.ModDebug)
		{
			debug = debug.ToString();
			string valueText = Variables.PreString + "***LOG***: " + debug;
			Variables.WriteStringToFile(valueText);
		}
	}

	public static void LogPlain(object debug)
	{
		object[] obj = new object[13]
		{
			"[",
			Variables.ModName,
			"][V",
			Variables.ModVersion,
			"][",
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null
		};
		DateTime now = DateTime.Now;
		obj[5] = now.Hour;
		obj[6] = ":";
		now = DateTime.Now;
		obj[7] = now.Minute;
		obj[8] = ":";
		now = DateTime.Now;
		obj[9] = now.Second;
		obj[10] = ".";
		now = DateTime.Now;
		obj[11] = now.Millisecond;
		obj[12] = "]";
		Variables.PreString = string.Concat(obj);
		if (Variables.ModDebug)
		{
			string valueText = debug.ToString();
			Variables.WriteStringToFile(valueText);
		}
	}

	public static void LogError(object debug)
	{
		object[] obj = new object[13]
		{
			"[",
			Variables.ModName,
			"][V",
			Variables.ModVersion,
			"][",
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null
		};
		DateTime now = DateTime.Now;
		obj[5] = now.Hour;
		obj[6] = ":";
		now = DateTime.Now;
		obj[7] = now.Minute;
		obj[8] = ":";
		now = DateTime.Now;
		obj[9] = now.Second;
		obj[10] = ".";
		now = DateTime.Now;
		obj[11] = now.Millisecond;
		obj[12] = "]";
		Variables.PreString = string.Concat(obj);
		if (Variables.ModDebug)
		{
			debug = debug.ToString();
			string valueText = Variables.PreString + "***ERROR LOG***: " + debug;
			Variables.WriteStringToFile(valueText);
		}
	}

	public static void LogValue(object ValueText, object Value)
	{
		object[] obj = new object[13]
		{
			"[",
			Variables.ModName,
			"][V",
			Variables.ModVersion,
			"][",
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null
		};
		DateTime now = DateTime.Now;
		obj[5] = now.Hour;
		obj[6] = ":";
		now = DateTime.Now;
		obj[7] = now.Minute;
		obj[8] = ":";
		now = DateTime.Now;
		obj[9] = now.Second;
		obj[10] = ".";
		now = DateTime.Now;
		obj[11] = now.Millisecond;
		obj[12] = "]";
		Variables.PreString = string.Concat(obj);
		if (Variables.ModDebug)
		{
			ValueText = ValueText.ToString();
			Value = Value.ToString();
			string valueText = Variables.PreString + "***VALUE LOG***: " + ValueText + " = " + Value;
			Variables.WriteStringToFile(valueText);
		}
	}

	public static void PrintLine()
	{
		Variables.WriteStringToFile("*******************************************************************************************");
	}

	public static void LogValue(object ValueText, object Value, bool Error)
	{
		object[] obj = new object[13]
		{
			"[",
			Variables.ModName,
			"][V",
			Variables.ModVersion,
			"][",
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null
		};
		DateTime now = DateTime.Now;
		obj[5] = now.Hour;
		obj[6] = ":";
		now = DateTime.Now;
		obj[7] = now.Minute;
		obj[8] = ":";
		now = DateTime.Now;
		obj[9] = now.Second;
		obj[10] = ".";
		now = DateTime.Now;
		obj[11] = now.Millisecond;
		obj[12] = "]";
		Variables.PreString = string.Concat(obj);
		if (Variables.ModDebug)
		{
			ValueText = ValueText.ToString();
			Value = Value.ToString();
			string valueText = Variables.PreString + "***VALUE LOG***: " + ValueText + " = " + Value;
			string valueText2 = Variables.PreString + "***VALUE LOG***: " + ValueText + " = " + Value;
			if (Error)
			{
				Variables.WriteStringToFile(valueText);
			}
			else
			{
				Variables.WriteStringToFile(valueText2);
			}
		}
	}

	public static void WriteStringToFile(string ValueText)
	{
		try
		{
			object obj = Variables.locker;
			Monitor.Enter(obj);
			try
			{
				using (FileStream stream = new FileStream(Variables.LogFilePath, FileMode.Append, FileAccess.Write))
				{
					using (StreamWriter streamWriter = new StreamWriter(stream))
					{
						streamWriter.WriteLine(ValueText);
					}
				}
			}
			finally
			{
				Monitor.Exit(obj);
			}
		}
		catch (Exception)
		{
			throw;
		}
	}

	public static void DelteLogFile()
	{
		try
		{
			if (File.Exists(Variables.LogFilePath))
			{
				File.Delete(Variables.LogFilePath);
				File.Create(Variables.LogFilePath).Close();
			}
			else
			{
				File.Create(Variables.LogFilePath).Close();
			}
		}
		catch (Exception)
		{
			throw;
		}
	}

	static Variables()
	{
		object[] obj = new object[13]
		{
			"[",
			Variables.ModName,
			"][V",
			Variables.ModVersion,
			"][",
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null
		};
		DateTime now = DateTime.Now;
		obj[5] = now.Hour;
		obj[6] = ":";
		now = DateTime.Now;
		obj[7] = now.Minute;
		obj[8] = ":";
		now = DateTime.Now;
		obj[9] = now.Second;
		obj[10] = ".";
		now = DateTime.Now;
		obj[11] = now.Millisecond;
		obj[12] = "]";
		Variables.PreString = string.Concat(obj);
		Variables.locker = new object();
	}
}
