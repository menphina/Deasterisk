using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Deasterisk
{
    internal class MemoryService
    {
        public MemoryInfo Info;

        //public Process CurrentContext => _info.Process;

        public MemoryService(Process process)
        {
            Info = new MemoryInfo
            {
                ProcHandle = MemorySvc.OpenProc(process.Id),
                BaseAddr = process.MainModule.BaseAddress,
                Process = process
            };
        }

        public void Dispose()
        {
            MemorySvc.CloseProc(Info.ProcHandle);
            Info = null;
        }

        public IntPtr TraceTree(IntPtr entry, int[] offsets, int finalOffset)
        {
            var pointer = new IntPtr(entry.ToInt64() + Info.BaseAddr.ToInt64());
            if (offsets.Length == 0)
                return pointer + finalOffset;
            return MemorySvc.TraceTree(Info.ProcHandle, pointer, offsets, finalOffset);
        }

        public byte[] Read(IntPtr address, long size)
        {
            return MemorySvc.Read(Info.ProcHandle, address, size);
        }

        public int Write(IntPtr address, byte[] data)
        {
            return MemorySvc.Write(Info.ProcHandle, address, data);
        }
    }

    internal class MemoryInfo
    {
        public IntPtr ProcHandle;
        public IntPtr BaseAddr;
        public Process Process;
    }

    internal static class MemorySvc
    {
        private const ProcessAccessFlags ProcessFlags =
            ProcessAccessFlags.VirtualMemoryRead
            | ProcessAccessFlags.VirtualMemoryWrite
            | ProcessAccessFlags.VirtualMemoryOperation
            | ProcessAccessFlags.QueryInformation;

        public static IntPtr OpenProc(int pid)
        {
            var ptr = OpenProcess(ProcessFlags, false, pid);
            if (ptr == IntPtr.Zero)
            {
                throw new Exception("Could not open handle: " + Marshal.GetLastWin32Error());
            }
            return ptr;
        }

        public static void CloseProc(IntPtr ptr)
        {
            if (!CloseHandle(ptr))
            {
                throw new Exception("Could not close handle: " + Marshal.GetLastWin32Error());
            }
        }

        public static int Write(IntPtr handle, IntPtr address, byte[] buffer)
        {
            if (!WriteProcessMemory(handle, address, buffer, buffer.Length, out _))
            {
                throw new Exception("Could not write process memory: " + Marshal.GetLastWin32Error());
            }
            return buffer.Length;
        }

        public static byte[] Read(IntPtr handle, IntPtr address, long size)
        {
            var buffer = new byte[size];
            if (!ReadProcessMemory(handle, address, buffer, buffer.Length, out _))
            {
                throw new Exception("Could not read process memory: " + Marshal.GetLastWin32Error());
            }
            return buffer;
        }

        public static IntPtr TraceTree(IntPtr handle, IntPtr baseAddr, int[] offsets, int finalOffset)
        {
            const int size = 8;
            //var addr = Process.GetProcessById(pid).MainModule.BaseAddress; // Very Expensive
            var buffer = new byte[size];
            foreach (var offset in offsets)
            {
                if (!ReadProcessMemory(handle, IntPtr.Add(baseAddr, offset), buffer, size, out _))
                {
                    throw new Exception("Unable to calculate address: " + Marshal.GetLastWin32Error());
                }
                baseAddr = new IntPtr(BitConverter.ToInt64(buffer, 0));
                if (baseAddr == IntPtr.Zero) return IntPtr.Zero;
            }
            return IntPtr.Add(baseAddr, finalOffset);
        }

        #region NativeMethods

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(ProcessAccessFlags processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, long dwSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, long nSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);

        [Flags]
        public enum ProcessAccessFlags : uint
        {
            VirtualMemoryOperation = 0x00000008,
            VirtualMemoryRead = 0x00000010,
            VirtualMemoryWrite = 0x00000020,
            QueryInformation = 0x00000400
        }

        #endregion
    }
}
