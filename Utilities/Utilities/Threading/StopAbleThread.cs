﻿namespace DeJong.Utilities.Threading
{
    using Logging;
    using Core;
    using System;
    using System.Threading;
    using System.Diagnostics;

    /// <summary>
    /// Contains a continually ticking STA background thread.
    /// </summary>
#if !DEBUG
    [DebuggerStepThrough]
#endif
    [DebuggerDisplay("{GetDebuggerString()}")]
    public sealed class StopableThread : IFullyDisposable
    {
        /// <summary>
        /// Gets or sets a value indicating the time in milliseconds the thread has to sleep after a tick.
        /// </summary>
        public int TickCooldown
        {
            get { return cooldown; }
            set
            {
                LoggedException.RaiseIf(value < 1, nameof(StopableThread), "Value must be larger than 0");
                cooldown = value;
            }
        }

        /// <summary>
        /// Gets whether this thread has started running.
        /// </summary>
        public bool Running { get { return running; } }

        /// <inheritdoc/>
        public bool Disposed { get; private set; }
        /// <inheritdoc/>
        public bool Disposing { get; private set; }

        /// <summary>
        /// Gets the name of this <see cref="StopableThread"/>.
        /// </summary>
        public string Name { get; private set; }

        private Thread thread;
        private bool running, stop, nameSet;
        private ThreadStart init, term, tick;
        private int cooldown;

        /// <summary>
        /// Initializes a new instance of the <see cref="StopableThread"/> class.
        /// </summary>
        /// <param name="init"> The function to call when initialzing the run loop (can be <see langword="null"/>). </param>
        /// <param name="term"> The function to call when terminating the run loop (can be <see langword="null"/>). </param>
        /// <param name="tick"> The function that handles the thread tick, cannot be <see langword="null"/>. </param>
        /// <param name="name"> The name of the thread. </param>
        public StopableThread(ThreadStart init, ThreadStart term, ThreadStart tick, string name = null)
        {
            LoggedException.RaiseIf(tick == null, nameof(StopableThread), "Unable to create thread!", new ArgumentNullException("tick", "tick cannot be null!"));
            TickCooldown = 10;
            this.init = init;
            this.term = term;
            this.tick = tick;
            Name = name;
            nameSet = !string.IsNullOrEmpty(name);
            thread = ThreadBuilder.CreateSTA(Run, name);
        }

        /// <summary>
        /// Disposes and finalizes the <see cref="StopableThread"/>.
        /// </summary>
        ~StopableThread()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StopableThread"/> class and starts it.
        /// </summary>
        /// <param name="init"> The function to call when initialzing the run loop (can be <see langword="null"/>). </param>
        /// <param name="term"> The function to call when terminating the run loop (can be <see langword="null"/>). </param>
        /// <param name="tick"> The function that handles the thread tick, cannot be <see langword="null"/>. </param>
        /// <param name="name"> The name of the thread. </param>
        /// <returns> The newly created thread. </returns>
        public static StopableThread StartNew(ThreadStart init, ThreadStart term, ThreadStart tick, string name = null)
        {
            StopableThread result = new StopableThread(init, term, tick, name);
            result.Start();
            return result;
        }


        /// <summary>
        /// Disposes the thread unsafely.
        /// </summary>
        public void Dispose()
        {
            Dispose(false);
        }

        /// <summary>
        /// Disposes the thread safely or unsafely.
        /// </summary>
        /// <param name="disposing"> Whether the method should wait for the thread to exit. </param>
        private void Dispose(bool disposing)
        {
            if (!(Disposed || Disposing))
            {
                Disposing = true;

                if (disposing) StopWait();
                else Stop();

                Disposing = false;
                Disposed = true;
            }
        }

        /// <summary>
        /// Starts the thread.
        /// </summary>
        public void Start()
        {
            if (running)
            {
                Log.Warning(nameof(StopableThread), "Attempted to start already running thread, call ignored.");
                return;
            }
            else if (thread == null || thread.ThreadState != System.Threading.ThreadState.Unstarted) thread = ThreadBuilder.CreateSTA(Run, Name);

            thread.Start();
        }

        /// <summary>
        /// Stops the thread.
        /// </summary>
        /// <remarks>
        /// Thread will continue running parallel untill it has completed it's current task.
        /// </remarks>
        public void Stop()
        {
            if (!running)
            {
                Log.Warning(nameof(StopableThread), "Attempted to stop not running thread, call ignored.");
                return;
            }

            stop = true;
        }

        /// <summary>
        /// Stops the thread and wait for it to close.
        /// </summary>
        public void StopWait()
        {
            Stop();
            while (running) Thread.Sleep(100);
        }

        [STAThread]
        private void Run()
        {
            running = true;
            Log.Info(nameof(StopableThread), $"Initializing thread{(nameSet ? $" '{Name}'" : $"({thread.ManagedThreadId})")}");
            init?.Invoke();

            while (!stop)
            {
                tick();
                Thread.Sleep(TickCooldown);
            }

            Log.Info(nameof(StopableThread), $"Terminating thread{(nameSet ? $" '{Name}'" : $"({thread.ManagedThreadId})")}");
            term?.Invoke();
            running = false;
        }

        private string GetDebuggerString()
        {
            return $"{Name}({thread.ManagedThreadId}) {(running ? "running" : "stopped")}";
        }
    }
}
