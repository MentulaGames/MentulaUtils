﻿namespace DeJong.Utilities.Logging
{
    using Core;
    using System;
    using Threading;
    using static NativeMethods;

    /// <summary>
    /// Defines a handler that displays logged messages to the console.
    /// </summary>
#if !DEBUG
    [System.Diagnostics.DebuggerStepThrough]
#endif
    public class ConsoleLogger : IFullyDisposable
    {
        /// <summary>
        /// The color used for logging verbose messages; default value=White
        /// </summary>
        public static ConsoleColor VerboseColor { get; set; } = ConsoleColor.White;
        /// <summary>
        /// The color used for logging debug messages; default value=Blue
        /// </summary>
        public static ConsoleColor DebugColor { get; set; } = ConsoleColor.Blue;
        /// <summary>
        /// The color used for logging info messages; default value=Green
        /// </summary>
        public static ConsoleColor InfoColor { get; set; } = ConsoleColor.Green;
        /// <summary>
        /// The color used for logging warning messages; default value=Yellow
        /// </summary>
        public static ConsoleColor WarningColor { get; set; } = ConsoleColor.Yellow;
        /// <summary>
        /// The color used for logging error messages; default value=Red
        /// </summary>
        public static ConsoleColor ErrorColor { get; set; } = ConsoleColor.Red;
        /// <summary>
        /// The color used for logging fatal messages; default value=DarkRed
        /// </summary>
        public static ConsoleColor FatalColor { get; set; } = ConsoleColor.Red;

        /// <summary>
        /// Gets or sets whether the <see cref="ConsoleLogger"/> should automaticly update itself.
        /// </summary>
        public bool AutoUpdate
        {
            get { return autoUpd; }
            set
            {
                if (value == autoUpd) return;

                if (value) updThread = StopableThread.StartNew(null, null, Update, nameof(ConsoleLogger));
                else if (updThread != null) updThread.Stop();

                autoUpd = value;
            }
        }

        /// <summary>
        /// Gets or set a value indicating whether the <see cref="ConsoleLogger"/>
        /// should dynamicaly pad the message header.
        /// </summary>
        public bool DynamicPadding { get; set; }

        /// <inheritdoc/>
        public bool Disposed { get; private set; }
        /// <inheritdoc/>
        public bool Disposing { get; private set; }

        private LogOutputType type;
        private StopableThread updThread;
        private ConsoleExitHandler hndlr;
        private bool autoUpd;
        private int hdrPad;

        /// <summary>
        /// Creates a new instance of the <see cref="ConsoleLogger"/> class with a specified output type.
        /// </summary>
        /// <param name="type"> The output type of the logger. </param>
        /// <param name="suppressConsoleResize"> Whether to suppress the console buffer from getting resized. </param>
        public ConsoleLogger(LogOutputType type = LogOutputType.ThreadTime, bool suppressConsoleResize = false)
        {
            this.type = type;
            if (hndlr == null)
            {
                AddConsoleHandle(hndlr += OnConsoleExit);
            }
            if (!suppressConsoleResize)
            {
                Console.BufferHeight = short.MaxValue - 1;
                Console.BufferWidth = short.MaxValue - 1;
            }
        }

        /// <summary>
        /// Disposes and finalizes the <see cref="ConsoleLogger"/>
        /// </summary>
        ~ConsoleLogger()
        {
            Dispose(false);
        }

        /// <summary>
        /// Writes the buffer contents to the console.
        /// </summary>
        public void Update()
        {
            ConsoleColor oldColor = Console.ForegroundColor;

            LogMessage msg;
            while ((msg = Log.PopLog()).Type != LogMessageType.None)
            {
                switch (msg.Type)
                {
                    case LogMessageType.Verbose: Console.ForegroundColor = VerboseColor; break;
                    case LogMessageType.Debug: Console.ForegroundColor = DebugColor; break;
                    case LogMessageType.Info: Console.ForegroundColor = InfoColor; break;
                    case LogMessageType.Warning: Console.ForegroundColor = WarningColor; break;
                    case LogMessageType.Error: Console.ForegroundColor = ErrorColor; break;
                    case LogMessageType.Fatal: Console.ForegroundColor = FatalColor; break;
                }

                if (DynamicPadding)
                {
                    string header = msg.GetLogHeaderLine(type);
                    if (header.Length + 2 > hdrPad) hdrPad = header.Length + 2;

                    Console.Write($"{header}: ".PadRight(hdrPad));
                    Console.Write($"{msg.Message}{Environment.NewLine}");
                }
                else Console.WriteLine(msg.GetLogLine(type));

                Log.FlushLog(msg);
            }

            Console.ForegroundColor = oldColor;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(false);
        }

        /// <summary>
        /// Disposes the managed and unmanaged data of the <see cref="ConsoleLogger"/>.
        /// </summary>
        /// <param name="disposing"> Whether the global log should be disposed. </param>
        protected virtual void Dispose(bool disposing)
        {
            if (!(Disposed || Disposing))
            {
                Disposing = true;

                if (disposing) Log.Dispose();
                if (hndlr != null)
                {
                    RemoveConsoleHandle(hndlr);
                    hndlr -= OnConsoleExit;
                }
                if (updThread != null) updThread.Dispose();

                Disposing = false;
                Disposed = true;
            }
        }

        private static bool OnConsoleExit(CtrlType sig)
        {
            Log.Verbose(nameof(ConsoleLogger), $"Console signal {sig} received, closing console.");

            switch (sig)
            {
                case CtrlType.CTRL_C_EVENT:
                case CtrlType.CTRL_CLOSE_EVENT:
                    return true;
                case CtrlType.CTRL_BREAK_EVENT:
                case CtrlType.CTRL_LOGOFF_EVENT:
                case CtrlType.CTRL_SHUTDOWN_EVENT:
                default:
                    return false;
            }
        }
    }
}