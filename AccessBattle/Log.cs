﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccessBattle
{
    public enum LogMode
    {
        Debug,
        Trace,
        Console,
        File
    }

    // TODO: Implement mode file, use TraceSource class
    public static class Log
    {
        private static LogMode Mode = LogMode.Debug;

        /// <summary>
        /// Set logging mode.
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="filename">Filename. Only required when file mode is used.</param>
        public static void SetMode(LogMode mode, string filename = null)
        {
            Mode = mode;
        }

        public static void WriteLine()
        {
            switch (Mode)
            {
                case LogMode.Console: Console.WriteLine(); break;
                case LogMode.Debug: Debug.WriteLine(""); break;
                default:
                    Trace.WriteLine(""); break;                
            }
        }

        public static void WriteLine(string message)
        {
            switch (Mode)
            {
                case LogMode.Console: Console.WriteLine(message); break;
                case LogMode.Debug: Debug.WriteLine(message); break;
                default:
                    Trace.WriteLine(message); break;
            }
        }

        public static void WriteLine(string format, params object[] args)
        {
            switch (Mode)
            {
                case LogMode.Console: Console.WriteLine(format, args); break;
                case LogMode.Debug: Debug.WriteLine(format, args); break;
                default:
                    Trace.WriteLine(string.Format(format, args)); break;
            }
        }

        public static void Write(string message)
        {
            switch (Mode)
            {
                case LogMode.Console: Console.Write(message); break;
                case LogMode.Debug: Debug.Write(message); break;
                default:
                    Trace.Write(message); break;
            }
        }

        public static void Write(string format, params object[] args)
        {
            switch (Mode)
            {
                case LogMode.Console: Console.Write(format, args); break;
                case LogMode.Debug: Debug.Write(string.Format(format, args)); break;
                default:
                    Trace.Write(string.Format(format, args)); break;
            }
        }
    }
}
