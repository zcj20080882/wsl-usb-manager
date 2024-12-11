using System;
using System.IO;
using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;
using System.Text;
using System.Reflection.Emit;
using log4net;
using log4net.Repository.Hierarchy;
using log4net.Core;
using log4net.Layout;
using log4net.Appender;

namespace UninstallCleanup
{
    class Program
    {
        static void Main(string[] args)
        {
            ConfigureLog4Net();
            log.Debug($"args: {string.Join(", ", args)}");
            if (args.Length < 2)
            {
                log.Error(args.Length < 2 ? "Invalid arguments" : "Invalid arguments");
                Console.WriteLine("Usage: UninstallCleanup <folderPath> <processName>");
                return;
            }

            string folderPath = args[0];
            string processName = args[1];

            if (Directory.Exists(folderPath))
            {
                var processes = Process.GetProcessesByName(processName);
                foreach (Process process in processes) { 
                    KillProcess(process);
                }

                try
                {
                    DeleteDirectory(folderPath);
                    log.Info($"Folder '{folderPath}' has been deleted.");
                }
                catch (Exception ex)
                {
                    log.Error($"Error deleting folder '{folderPath}': {ex.Message}");
                }
            }
            else
            {
                log.Warn($"Folder does not exist: {folderPath}, do nothing.");
            }
            log.Info("exit...");
        }

        private static readonly ILog log = LogManager.GetLogger(typeof(Program));
        private static readonly string LogConversionPattern = "%date [%thread] %-5level %logger:%line - %message%newline";
        private const string LogDivider = "\r\n-----------------------------------------------------------------------------------------------------------------------------------------";
        private static void ConfigureLog4Net()
        {
            string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs/");
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            // Get logger repository
            Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();

            // Create PatternLayout
            PatternLayout patternLayout = new()
            {
                ConversionPattern = LogConversionPattern
            };
            patternLayout.ActivateOptions();

            RollingFileAppender rollingFileAppender = new()
            {
                File = logDirectory,
                AppendToFile = true,
                RollingStyle = RollingFileAppender.RollingMode.Date,
                StaticLogFileName = false,
                Layout = patternLayout,
                DatePattern = "yyyyMMdd'.log'"
            };
            rollingFileAppender.ActivateOptions();

            // Create ConsoleAppender
            ConsoleAppender consoleAppender = new()
            {
                Layout = patternLayout
            };
            consoleAppender.ActivateOptions();

            hierarchy.Root.AddAppender(rollingFileAppender);
            hierarchy.Root.AddAppender(consoleAppender);

#if DEBUG
            // Create DebugAppender
            DebugAppender debugAppender = new DebugAppender
            {
                Layout = patternLayout
            };
            debugAppender.ActivateOptions();

            // Add DebugAppender to Root
            hierarchy.Root.AddAppender(debugAppender);
#endif

            // Set log level
            hierarchy.Root.Level = Level.Debug;

            // Apply config
            hierarchy.Configured = true;
            log.Info(LogDivider);
            log.Info("Start log server...");
        }

        static bool KillProcess(Process process)
        {
            for (int i = 0; i<3; i++ )
            {
                try
                {
                    log.Info($"Attempting to close process '{process.ProcessName}'(ID={process.Id})...");
                    process.Kill();
                    process.WaitForExit();
                    Console.WriteLine($"Process '{process.ProcessName}' has been terminated.");
                    return true;
                }
                catch (Exception ex)
                {
                    if (process.HasExited)
                    {
                        Console.WriteLine($"Process (ID: {process.ProcessName}) has already exited.");
                        return true;
                    }
                    Console.WriteLine($"Error terminating process (ID: {process.Id}): {ex.Message}");
                }
            }
            
            return false;
        }
        static void DeleteDirectory(string directoryPath)
        {
            foreach (string file in Directory.GetFiles(directoryPath))
            {
                var processes = GetProcessesUsingFile(file);

                foreach (Process process in processes)
                {
                    Console.WriteLine($"Process {process.ProcessName} is using file '{file}'");
                    KillProcess(process);
                }

                try
                {
                    log.Info("Deleting file: " + file);
                    File.Delete(file);
                }
                catch (IOException e)
                {
                    Debug.WriteLine($"Failed to delete '{file}': {e.Message}");
                }
            }

            foreach (string dir in Directory.GetDirectories(directoryPath))
            {
                DeleteDirectory(dir);
            }
            log.Info("Deleting directory: " + directoryPath);
            Directory.Delete(directoryPath, true);
        }

        static Process[] GetProcessesUsingFile(string filePath)
        {
            var processes = new List<Process>();
            var handleInfo = new NativeMethods.SYSTEM_HANDLE_INFORMATION();
            int handleInfoSize = Marshal.SizeOf(handleInfo);
            IntPtr handleInfoPtr = Marshal.AllocHGlobal(handleInfoSize);
            int length = 0;

            while (NativeMethods.NtQuerySystemInformation(NativeMethods.SystemHandleInformation, handleInfoPtr, handleInfoSize, ref length) == NativeMethods.STATUS_INFO_LENGTH_MISMATCH)
            {
                handleInfoSize = length;
                Marshal.FreeHGlobal(handleInfoPtr);
                handleInfoPtr = Marshal.AllocHGlobal(length);
            }

#pragma warning disable CS8605 // Unboxing a possibly null value.
            handleInfo = (NativeMethods.SYSTEM_HANDLE_INFORMATION)Marshal.PtrToStructure(handleInfoPtr, typeof(NativeMethods.SYSTEM_HANDLE_INFORMATION));
#pragma warning restore CS8605 // Unboxing a possibly null value.
            int handleCount = handleInfo.HandleCount;
            IntPtr handlePtr = new IntPtr(handleInfoPtr.ToInt64() + Marshal.OffsetOf(typeof(NativeMethods.SYSTEM_HANDLE_INFORMATION), "Handles").ToInt64());

            for (int i = 0; i < handleCount; i++)
            {
#pragma warning disable CS8605 // Unboxing a possibly null value.
                var handle = (NativeMethods.SYSTEM_HANDLE)Marshal.PtrToStructure(handlePtr, typeof(NativeMethods.SYSTEM_HANDLE));
#pragma warning restore CS8605 // Unboxing a possibly null value.
                handlePtr = new IntPtr(handlePtr.ToInt64() + Marshal.SizeOf(handle));

                if (handle.ObjectTypeNumber != NativeMethods.OB_TYPE_FILE)
                    continue;

                IntPtr processHandle = NativeMethods.OpenProcess(NativeMethods.PROCESS_DUP_HANDLE, false, handle.ProcessId);
                if (processHandle == IntPtr.Zero)
                    continue;

                IntPtr fileHandle;
                if (!NativeMethods.DuplicateHandle(processHandle, new IntPtr(handle.Handle), NativeMethods.GetCurrentProcess(), out fileHandle, 0, false, NativeMethods.DUPLICATE_SAME_ACCESS))
                {
                    NativeMethods.CloseHandle(processHandle);
                    continue;
                }

                var fileName = new StringBuilder(1024);
                int fileNameSize = fileName.Capacity;
                if (NativeMethods.NtQueryObject(fileHandle, NativeMethods.ObjectNameInformation, fileName, fileNameSize, ref fileNameSize) == 0)
                {
                    string fileNameStr = fileName.ToString();
                    if (fileNameStr.Contains(filePath))
                    {
                        try
                        {
                            var process = Process.GetProcessById(handle.ProcessId);
                            processes.Add(process);
                        }
                        catch
                        {
                            // Ignore processes that we can't access
                        }
                    }
                }

                NativeMethods.CloseHandle(fileHandle);
                NativeMethods.CloseHandle(processHandle);
            }

            Marshal.FreeHGlobal(handleInfoPtr);
            return [.. processes];
        }

        internal static class NativeMethods
        {
            public const int SystemHandleInformation = 16;
            public const int STATUS_INFO_LENGTH_MISMATCH = unchecked((int)0xC0000004);
            public const int PROCESS_DUP_HANDLE = 0x0040;
            public const int DUPLICATE_SAME_ACCESS = 0x00000002;
            public const int OB_TYPE_FILE = 28;
            public const int ObjectNameInformation = 1;

            [StructLayout(LayoutKind.Sequential)]
            public struct SYSTEM_HANDLE_INFORMATION
            {
                public int HandleCount;
                public SYSTEM_HANDLE Handles;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct SYSTEM_HANDLE
            {
                public int ProcessId;
                public byte ObjectTypeNumber;
                public byte Flags;
                public ushort Handle;
                public IntPtr Object;
                public uint GrantedAccess;
            }

            [DllImport("ntdll.dll")]
            public static extern int NtQuerySystemInformation(int SystemInformationClass, IntPtr SystemInformation, int SystemInformationLength, ref int ReturnLength);

            [DllImport("kernel32.dll")]
            public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

            [DllImport("kernel32.dll")]
            public static extern bool DuplicateHandle(IntPtr hSourceProcessHandle, IntPtr hSourceHandle, IntPtr hTargetProcessHandle, out IntPtr lpTargetHandle, uint dwDesiredAccess, bool bInheritHandle, uint dwOptions);

            [DllImport("kernel32.dll")]
            public static extern IntPtr GetCurrentProcess();

            [DllImport("ntdll.dll")]
            public static extern int NtQueryObject(IntPtr ObjectHandle, int ObjectInformationClass, StringBuilder ObjectInformation, int ObjectInformationLength, ref int ReturnLength);

            [DllImport("kernel32.dll")]
            public static extern bool CloseHandle(IntPtr hObject);
        }
    }
}