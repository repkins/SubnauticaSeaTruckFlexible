using System;
using System.Diagnostics;
using System.Reflection;

namespace SubnauticaSeaTruckFlexible
{
    class Logger
    {
        private static string assemblyName = Assembly.GetCallingAssembly().GetName().Name;

        static public void Info(string message)
        {
            Console.WriteLine("[{0}/Info] {1}", assemblyName, message);
        }
        static public void Warning(string message)
        {
            Console.WriteLine("[{0}/Warn] {1}", assemblyName, message);
        }
        static public void Error(string message)
        {
            Console.WriteLine("[{0}/ERROR] {1}", assemblyName, message);
        }

        [Conditional("DEBUG")]
        static public void Debug(string message)
        {
            var callingMethod = new StackFrame(1).GetMethod();
            var callingMethodName = callingMethod.Name;
            var callingMethodClassName = callingMethod.DeclaringType.Name;

            foreach (var messageLine in message.Split('\n'))
            {
                Console.WriteLine("[{0}/Debug]: {1}.{2}(): {3}", assemblyName, callingMethodClassName, callingMethodName, messageLine);
            }
        }
    }
}
