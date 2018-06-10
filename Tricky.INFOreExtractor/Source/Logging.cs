using System;
using System.Text;
using System.Diagnostics;
using System.Reflection;
using Debug = UnityEngine.Debug;

namespace MadVandal.FortressCraft
{
    public static class Logging
    {
        /// <summary>
        /// Mod name.
        /// </summary>
        public static string ModName = string.Empty;

        /// <summary>
        /// Logging level.
        /// </summary>
        public static int LoggingLevel = 0;

        public static bool TestMode = false;


        /// <summary>
        /// Gets the current date/time string for logging.
        /// </summary>
        public static string LogDateTime
        {
            get
            {
                DateTime dateTime = DateTime.Now;
                return dateTime.ToShortDateString() + " " + dateTime.ToShortTimeString();
            }
        }


        /// <summary>
        /// Logs an exception.
        /// </summary>
        /// <param name="e">Exception.</param>
        /// <param name="action">Action message or null if none.</param>
        /// <param name="result">Result of the error message or null if none.</param>
        public static void LogException(Exception e, string action = null, string result = null)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("[MOD: ");
            if (ModName != string.Empty)
                stringBuilder.Append(ModName + " ");
            stringBuilder.Append(LogDateTime + "]\n*EXCEPTION* at ");
            MethodBase methodBase = new StackFrame(1).GetMethod();
            string typeName = methodBase.DeclaringType?.Name;
            stringBuilder.Append(typeName + "." + methodBase.Name+"\n");
            if (action != null)
                stringBuilder.Append("\nAction: " + action);
            if (result != null)
                stringBuilder.Append("\nResult:" + result);
            stringBuilder.Append("\n" + e);
            if (TestMode)
                System.Diagnostics.Debug.WriteLine(stringBuilder);
            else
                Debug.Log(stringBuilder);
        }


        /// <summary>
        /// Logs an event or delegate invoke exception.
        /// </summary>
        /// <param name="e">Exception.</param>
        /// <param name="name">Event or delegate name.</param>
        /// <param name="result">Result of the error message or null if none.</param>
        public static void LogInvokeException(string name, Exception e, string result = null)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("[MOD: ");
            if (ModName != string.Empty)
                stringBuilder.Append(ModName + " ");
            stringBuilder.Append(LogDateTime + "]\n");
            MethodBase methodBase = new StackFrame(1).GetMethod();
            string typeName = methodBase.DeclaringType?.Name;
            stringBuilder.Append(typeName + "." + methodBase.Name + " - ");
            stringBuilder.Append("Exception invoking " + name);
            if (result != null)
                stringBuilder.Append("\n" + result);
            stringBuilder.Append("\n");
            stringBuilder.Append(e);
            if (TestMode)
                System.Diagnostics.Debug.WriteLine(stringBuilder);
            else
                Debug.Log(stringBuilder);
        }


        /// <summary>
        /// Logs a non-exception error.
        /// </summary>
        /// <param name="error">Error message or null if none.</param>
        /// <param name="action">Action message or null if none.</param>
        /// <param name="includeClassMethod">If true include the class and method in the log entry.</param>
        /// <param name="result">Result of the error message or null if none.</param>
        public static void LogError(string error, string action=null, bool includeClassMethod = false, string result = null)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("[MOD: ");
            if (ModName != string.Empty)
                stringBuilder.Append(ModName + " ");
            stringBuilder.Append(LogDateTime + "]\n");
            if (includeClassMethod)
            {
                MethodBase methodBase = new StackFrame(1).GetMethod();
                string typeName = methodBase.DeclaringType?.Name;
                stringBuilder.Append(typeName + "." + methodBase.Name + " - ");
            }

            stringBuilder.Append("Error: " + error);
            if (action!=null)
                stringBuilder.Append("\nAction: " + action);
            if (result != null)
                stringBuilder.Append("\nResult:" + result);
            if (TestMode)
                System.Diagnostics.Debug.WriteLine(stringBuilder);
            else
                Debug.Log(stringBuilder);
        }


        /// <summary>
        /// Log missing cube key message.
        /// </summary>
        /// <param name="keyName">Key name.</param>
        public static void LogMissingCubeKey(string keyName)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("[MOD: ");
            if (ModName != string.Empty)
                stringBuilder.Append(ModName + " ");
            stringBuilder.Append(LogDateTime + "]\n");
            stringBuilder.Append("Error: Missing '" + keyName + "' cube key");
            if (TestMode)
                System.Diagnostics.Debug.WriteLine(stringBuilder);
            else
                Debug.Log(stringBuilder);
        }


        /// <summary>
        /// Log missing item key message.
        /// </summary>
        /// <param name="keyName">Key name.</param>
        public static void LogMissingItemKey(string keyName)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("[MOD: ");
            if (ModName != string.Empty)
                stringBuilder.Append(ModName + " ");
            stringBuilder.Append(LogDateTime + "]\n");
            stringBuilder.Append("Error: Missing '" + keyName + "' item key");
            if (TestMode)
                System.Diagnostics.Debug.WriteLine(stringBuilder);
            else
                Debug.Log(stringBuilder);
        }


        /// <summary>
        /// Log missing research key message.
        /// </summary>
        /// <param name="keyName">Key name.</param>
        public static void LogMissingResearchKey(string keyName)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("[MOD: ");
            if (ModName != string.Empty)
                stringBuilder.Append(ModName + " ");
            stringBuilder.Append(LogDateTime + "]\n");
            stringBuilder.Append("Error: Missing '" + keyName + "' research key");
            if (TestMode)
                System.Diagnostics.Debug.WriteLine(stringBuilder);
            else
                Debug.Log(stringBuilder);
        }


        /// <summary>
        /// Log missing Unity object message.
        /// </summary>
        /// <param name="objectName">Object name.</param>
        public static void LogMissingUnityObject(string objectName)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("[MOD: ");
            if (ModName != string.Empty)
                stringBuilder.Append(ModName + " ");
            stringBuilder.Append(LogDateTime + "]\n");
            stringBuilder.Append("Error: Missing '" + objectName + "' Unity object");
            if (TestMode)
                System.Diagnostics.Debug.WriteLine(stringBuilder);
            else
                Debug.Log(stringBuilder);
        }


        /// <summary>
        /// Logs a message associated to the mod.
        /// </summary>
        /// <param name="message">message to log.</param>
        /// <param name="loggingLevel">Logging level for this message. Message will only be logged if this parameter is equal of greater than the current LoggingLevel field value.</param>
        public static void LogMessage(string message, int loggingLevel = 0)
        {
            if (loggingLevel > LoggingLevel)
                return;

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("[MOD: ");
            if (ModName != string.Empty)
                stringBuilder.Append(ModName + " ");
            stringBuilder.Append(LogDateTime + "]\n");
            stringBuilder.Append(message);
            if (TestMode)
                System.Diagnostics.Debug.WriteLine(stringBuilder);
            else
                Debug.Log(stringBuilder);
        }

    }
}