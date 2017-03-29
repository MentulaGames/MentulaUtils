﻿namespace Mentula.Utilities.Logging
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    public static partial class Log
    {
        private static Thread logThread;
        private static List<LogMessage> preBuffer;
        private static Queue<LogMessage> msgbuffer;

        private static bool running, stop;

        static Log()
        {
            obj = new EnsureDisposeObj();
            handler += new NativeMethods.ConsoleExitHandler(OnConsoleExit);
            NativeMethods.SetConsoleCtrlHandler(OnConsoleExit, true);

            preBuffer = new List<LogMessage>();
            msgbuffer = new Queue<LogMessage>();

            logThread = Threading.CreateSTA(PipeRun);
            logThread.Start();
        }

        private static void Message(LogMessageType type, string tag, string message)
        {
            lock (preBuffer)
            {
                preBuffer.Add(new LogMessage(type, Threading.GetCurrentProcessId(), Threading.GetCurrentThreadId(), tag, message));
            }
        }

        [STAThread]
        private static void PipeRun()
        {
            running = true;
            Info(nameof(Log), "Starting log thread");

            while (!stop)
            {
                lock (preBuffer)
                {
                    lock (msgbuffer)
                    {
                        while (preBuffer.Count > 0)
                        {
                            msgbuffer.Enqueue(GetNext());
                        }
                    }
                }
            }

            Info(nameof(Log), "Stopping log thread");
            running = false;
        }

        private static LogMessage GetNext()
        {
            LogMessage result = preBuffer[0];

            for (int i = 0; i < preBuffer.Count; i++)
            {
                LogMessage cur = preBuffer[i];
                if ((int)cur.Type > (int)result.Type) result = cur;
            }

            preBuffer.Remove(result);
            return result;
        }

        private static void LogException(string tag, int pId, int tId, Exception e)
        {
            Fatal_private(tag, pId, tId, $"Exception: {e.GetType().Name}");
            Fatal_private(tag, pId, tId, $"Full name: {e.GetType().AssemblyQualifiedName}");
            Fatal_private(tag, pId, tId, $"Message: {e.Message}");

            if (e.Data.Count > 0)
            {
                Fatal_private(tag, pId, tId, $"Additional information:");
                foreach (object cur in e.Data)
                {
                    Fatal_private(tag, pId, tId, cur.ToString());
                }
            }

            if (!string.IsNullOrEmpty(e.StackTrace))
            {
                Fatal_private(tag, pId, tId, "Stacktrace:");
                Fatal_private(tag, pId, tId, e.StackTrace);
            }

            if (e.InnerException != null)
            {
                Fatal_private(tag, pId, tId, "Inner exception:");
                LogException(tag, pId, tId, e.InnerException);
            }
        }

        private static void Fatal_private(string tag, int pId, int tId, string message)
        {
            preBuffer.Add(new LogMessage(LogMessageType.Fatal, pId, tId, tag, message));
        }
    }
}