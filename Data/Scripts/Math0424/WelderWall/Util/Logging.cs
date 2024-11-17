using Sandbox.ModAPI;
using System;
using VRage.Utils;

namespace Voidfront
{
    internal class Logging
    {
        static int _indentLevel = 0;
        static bool _debug = true;

        public enum LoggingMode
        {
            Debug,
            Info,
            Warning,
            Error,
            Keen,
        }

        public static void IncreaseIndent()
        {
            _indentLevel++;
        }

        public static void DecreaseIndent()
        {
            _indentLevel = Math.Max(0, --_indentLevel);
        }

        public static void Log(LoggingMode mode, object data)
        {
            if (!_debug && mode == LoggingMode.Debug)
                return;

            string indent = "".PadRight(_indentLevel);
            string[] arr = (data ?? 0).ToString().Split('\n');
            string dateTime = $"[{DateTime.Now}] [{mode.ToString().ToUpper()}] ";

            string combined = "";
            for (int i = 0; i < arr.Length; i++)
                if (i == 0)
                    combined += indent + arr[i] + "\n";
                else
                    combined += indent.PadRight(dateTime.Length) + arr[i] + "\n";
            MyLog.Default.WriteLineAndConsole($"{dateTime}{combined.Substring(0, combined.Length - 1)}");
        }

        public static void Info(object data)
        {
            Log(LoggingMode.Info, data);
        }

        public static void Warning(object data)
        {
            Log(LoggingMode.Warning, data);
        }

        public static void Debug(object data)
        {
            Log(LoggingMode.Debug, data);
        }

        public static void Error(object data)
        {
            Log(LoggingMode.Error, data);
        }

        public static void Error(Exception ex)
        {
            Log(LoggingMode.Error, $"{ex.Source}: {ex.Message}\n{ex.StackTrace}");
        }

        public static void Keen(object data)
        {
            Log(LoggingMode.Keen, data);
        }
    }
}
