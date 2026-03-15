using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

    public enum ELogLevel
    {
        Info,
        Warning,
        Error,
    }

    public class Logger
    {
        public static void Log( string message, ELogLevel logLevel = ELogLevel.Info)
        {
            switch (logLevel)
            {
                case ELogLevel.Info:
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
                case ELogLevel.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case ELogLevel.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
            }
            Console.WriteLine(message);
            Console.ResetColor();
        }

        public static void LogError(string message)
        {
            Log(message, ELogLevel.Error);
        }

    }

