using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.ModAPI;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter
{
    internal class Logger
    {
        public static Logger Instance { get; private set; }

        public string BlockId;
        public const string Ending = "Logs.txt";
        public bool IsEnabled = true;

        private string FileName => BlockId + "_" + Ending;


        public Logger(string blockId)
        {

            Instance = this;
            BlockId = blockId;
            using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(FileName, typeof(Logger)))
            {
                writer.Write("--------Log start--------");
                writer.WriteLine();
            }

        }

        public void Log(string originClass, string message)
        {
            if (!IsEnabled) return;

            message = $"{DateTime.Now}::{originClass}: {message}"; // Add a newline to the end of each message

            try
            {
                string existingContent;
                using (var stream = MyAPIGateway.Utilities.ReadFileInWorldStorage(FileName, typeof(Logger)))
                {
                    existingContent = stream.ReadToEnd(); // Read the existing content
                    existingContent += $"{message}\n"; // Add new message with a newline
                }

                using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(FileName, typeof(Logger)))
                {
                    writer.Write(existingContent); // Write back all the content including the new message
                }
            }
            catch (Exception)
            {
                using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(FileName, typeof(Logger)))
                {
                    writer.Write(message); // Write back all the content including the new message
                }
            }
        }

        public void LogWarning(string originClass, string message)
        {
            Log(originClass, "[WARNING] " + message);
        }

        public void LogError(string originClass, string message)
        {
            Log(originClass, "[ERROR] " + message);
        }

    }
}