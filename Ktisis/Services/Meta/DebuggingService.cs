using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

using FFXIVClientStructs.FFXIV.Client.System.Framework;

using Ktisis.Core.Attributes;

namespace Ktisis.Services.Meta;


[Singleton]
public unsafe class DebuggingService : IDisposable {
	
	#region Native Code
	
	const int PAGE_EXECUTE_READ = 0x20;
	const int PAGE_GUARD = 0x100;
	const uint STATUS_GUARD_PAGE_VIOLATION = 2147483649;// Exception code = 0x80000001
	const uint EXCEPTION_SINGLE_STEP = 2147483653; // Exception code = 0x80000004
	const long EXCEPTION_CONTINUE_EXECUTION = -1;
	const long EXCEPTION_CONTINUE_SEARCH = 0;
	[DllImport("kernel32.dll", SetLastError = true)]
	static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION64 lpBuffer, uint dwLength); 
	
	//dwSize should be IntPtr because the underlying type is SIZE_T and varies with the platform.
	[DllImport("kernel32.dll", SetLastError = true)]
	static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress,
		UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);
	
	[DllImport("kernel32.dll")]
	static extern nint AddVectoredExceptionHandler(long First, nint Handler);

	[DllImport("kernel32.dll")]
	static extern ulong RemoveVectoredExceptionHandler(nint Handler);
	
	[DllImport("kernel32.dll")]
	static extern nint GetCurrentProcess();
	
	[DllImport("kernel32.dll", SetLastError = true)]
	public static extern  bool ReadProcessMemory(IntPtr hProcess,
		IntPtr lpBaseAddress,
		[Out] byte[] lpBuffer,
		int dwSize,
		out IntPtr lpNumberOfBytesRead);
	#endregion
	
	#region Handler Stuff
	public delegate long VectoredExceptionHandlerDelegate(IntPtr ExceptionInfo_Ptr);

	internal static VectoredExceptionHandlerDelegate handler;
	internal static nint handlerPtr;
	public long retrn;
	public static int counter = 0;
	public bool requestedUnhook => _requestedUnhook;
	internal static bool _requestedUnhook ;
	public int Count => counter;
	public unsafe static long VectoredExceptionHandler(IntPtr ExceptionInfo_Ptr) {
		byte[] data1 = new byte[Marshal.SizeOf(typeof(IntPtr))];
		ReadProcessMemory(GetCurrentProcess(), ExceptionInfo_Ptr, data1, data1.Length, out _);

		IntPtr aux = MarshalBytesTo<IntPtr>(data1);
		
		byte[] data2 = new byte[Marshal.SizeOf(typeof(EXCEPTION_POINTERS))];
		ReadProcessMemory(GetCurrentProcess(), aux, data2, data2.Length, out _);

		EXCEPTION_POINTERS ExceptionInfo = MarshalBytesTo<EXCEPTION_POINTERS>(data2);
		if (ExceptionInfo.exceptionRecord.ExceptionCode == STATUS_GUARD_PAGE_VIOLATION) {
			counter++;
			if (!_requestedUnhook) {
							//Set Trap flag
			} else {
				RemoveVectoredExceptionHandler(handlerPtr);
			}
			return EXCEPTION_CONTINUE_EXECUTION; //Continue Execution
		} else if (ExceptionInfo.exceptionRecord.ExceptionCode == EXCEPTION_SINGLE_STEP) {
			ResetGuardForAddress();
			return EXCEPTION_CONTINUE_EXECUTION;
		}
		return EXCEPTION_CONTINUE_SEARCH; //We arent handling this, so let it try and find another exception handler
	}
	public void RegisterExceptionHandler() {
		if (this.isHandlerSetup)
			return;
		
		try {
			handler = VectoredExceptionHandler;
			handlerPtr = Marshal.GetFunctionPointerForDelegate(handler);
			this.retrn = AddVectoredExceptionHandler(1, handlerPtr);
			if (this.retrn == IntPtr.Zero) {
				Ktisis.Log.Debug("Failed to add veh");

				this.isHandlerSetup = false;
			} else {
				Ktisis.Log.Debug("added veh");
				this.isHandlerSetup = true;
			}
		} catch(Exception e) {
			
		}

	}
	public void UnregisterExceptionHandler() {
		if (!this.isHandlerSetup)
			return;
		_requestedUnhook = true;
		this.isHandlerSetup = false;
	}
	
	#endregion

	internal List<IntPtr> WatchAddresses = new List<nint>();
	internal static Tuple<nint, nint, uint> PageInfo = new(0, 0, 0);
	internal bool isHandlerSetup;
	public bool SetupGuardForAddress(IntPtr address) {
		if (PageInfo.Item1 != 0) {
			
		}
		MEMORY_BASIC_INFORMATION64 pageInfo = new MEMORY_BASIC_INFORMATION64();
		var processHandle = GetCurrentProcess();
		var e = VirtualQueryEx(processHandle, address, out pageInfo, (uint)sizeof(MEMORY_BASIC_INFORMATION64));
		if (e == 0) {
			Ktisis.Log.Debug($"{Marshal.GetLastWin32Error()}");
			return false;
		}
		
		var newOption = pageInfo.AllocationProtect | AllocationProtect.PAGE_GUARD;
		Ktisis.Log.Debug($"{processHandle} {pageInfo.BaseAddress:X8} {pageInfo.RegionSize} {pageInfo.AllocationBase:X8} {pageInfo.Protect} {newOption}");
		var res = VirtualProtectEx(processHandle, (nint)pageInfo.BaseAddress, (nuint)1, (uint)newOption, out var _);
		if (!res) {
			var error = Marshal.GetLastWin32Error();
			Ktisis.Log.Debug($"{error}");
			return false;
		}
		PageInfo = new((nint)processHandle, (nint)pageInfo.BaseAddress, (uint)newOption);
		return true;
	}

	public static bool ResetGuardForAddress() {
		var res = VirtualProtectEx(PageInfo.Item1, (nint)PageInfo.Item2, (nuint)1, (uint)PageInfo.Item3, out var _);
		if (!res) {
			var error = Marshal.GetLastWin32Error();
			Ktisis.Log.Debug($"{error}");
			return false;
		}
		return true;
	}
	static T MarshalBytesTo<T>(byte[] bytes)
	{
		GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
		T theStructure = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
		handle.Free();
		return theStructure;
	}

	public void Dispose() {
		if(this.isHandlerSetup)
			this.UnregisterExceptionHandler();
	}
	
}

#region Structs

[StructLayout(LayoutKind.Sequential)]
public struct MEMORY_BASIC_INFORMATION64
{
	public ulong BaseAddress;
	public ulong AllocationBase;
	public AllocationProtect AllocationProtect;
	public int __alignment1;
	public ulong RegionSize;
	public int State;
	public int Protect;
	public int Type;
	public int __alignment2;
}

public enum AllocationProtect : uint
{
	PAGE_EXECUTE = 0x00000010,
	PAGE_EXECUTE_READ = 0x00000020,
	PAGE_EXECUTE_READWRITE = 0x00000040,
	PAGE_EXECUTE_WRITECOPY = 0x00000080,
	PAGE_NOACCESS = 0x00000001,
	PAGE_READONLY = 0x00000002,
	PAGE_READWRITE = 0x00000004,
	PAGE_WRITECOPY = 0x00000008,
	PAGE_GUARD = 0x00000100,
	PAGE_NOCACHE = 0x00000200,
	PAGE_WRITECOMBINE = 0x00000400
}
[StructLayout( LayoutKind.Sequential )]
public struct EXCEPTION_RECORD
{
	public uint ExceptionCode;
	public uint ExceptionFlags;
	public IntPtr ExceptionRecord;
	public IntPtr ExceptionAddress;
	public uint NumberParameters;
	[MarshalAs( UnmanagedType.ByValArray, SizeConst = 15, ArraySubType = UnmanagedType.U4 )] public uint[] ExceptionInformation;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct EXCEPTION_POINTERS
{
	public EXCEPTION_RECORD exceptionRecord;
	public CONTEXT contextRecord;

}
[StructLayout(LayoutKind.Sequential)]
public struct CONTEXT
{
	UInt32 ContextFlags;
	UInt32 Dr0;
	UInt32 Dr1;
	UInt32 Dr2;
	UInt32 Dr3;
	UInt32 Dr6;
	UInt32 Dr7;
	FLOATING_SAVE_AREA FloatSave;
	UInt32 SegGs;
	UInt32 SegFs;
	UInt32 SegEs;
	UInt32 SegDs;
	UInt32 Edi;
	UInt32 Esi;
	UInt32 Ebx;
	UInt32 Edx;
	UInt32 Ecx;
	UInt32 Eax;
	UInt32 Ebp;
	UInt32 Eip;
	UInt32 SegCs;
	UInt32 EFlags;
	UInt32 Esp;
	UInt32 SegSs;
};

[StructLayout(LayoutKind.Sequential)]
struct FLOATING_SAVE_AREA
{
	UInt32 ControlWord;
	UInt32 StatusWord;
	UInt32 TagWord;
	UInt32 ErrorOffset;
	UInt32 ErrorSelector;
	UInt32 DataOffset;
	UInt32 DataSelector;
	byte RegisterArea;
	UInt32 Cr0NpxState;
};

[Flags]
public enum ProcessAccessFlags : uint
{
	All = 0x001F0FFF,
	Terminate = 0x00000001,
	CreateThread = 0x00000002,
	VirtualMemoryOperation = 0x00000008,
	VirtualMemoryRead = 0x00000010,
	VirtualMemoryWrite = 0x00000020,
	DuplicateHandle = 0x00000040,
	CreateProcess = 0x000000080,
	SetQuota = 0x00000100,
	SetInformation = 0x00000200,
	QueryInformation = 0x00000400,
	QueryLimitedInformation = 0x00001000,
	Synchronize = 0x00100000
}


#endregion