using System;
using Sandbox.ModAPI;
using VRage.Utils;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter.SessionComponent
{
    public static class Logger
    {
        public static bool IsEnabled = true;
        public static void Log(string originClass, string message)
        {
            if (!IsEnabled) return;
            const MyLogSeverity serenity = MyLogSeverity.Debug;
            message = $"{DateTime.Now}::{originClass}: {message}";

            try
            {
                MyLog.Default.Log(serenity, message);
               
            }
            catch (Exception ex)
            {
                MyAPIGateway.Utilities.ShowMessage("Logger", $"Error writing log: {ex.Message}");
            }
        }
        public static void Log(string originClass, string message, MyLogSeverity serenity)
        {
            if (!IsEnabled) return;
            message = $"{DateTime.Now}::{originClass}: {message}";

            try
            {
                MyLog.Default.Log(serenity, message);

            }
            catch (Exception ex)
            {
                MyAPIGateway.Utilities.ShowMessage("Logger", $"Error writing log: {ex.Message}");
            }
        }

        public static void LogWarning(string originClass, string message)
        {
            Log(originClass, "[WARNING] " + message,MyLogSeverity.Warning);
        }

        public static void LogError(string originClass, string message)
        {
            Log(originClass, "[ERROR] " + message,MyLogSeverity.Error);
        }
    }
}