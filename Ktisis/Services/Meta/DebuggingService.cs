using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;

using FFXIVClientStructs.FFXIV.Client.System.Framework;

using Ktisis.Core.Attributes;

namespace Ktisis.Services.Meta;


[Singleton]
public unsafe class DebuggingService : IDisposable {
	
	#region Native Code
	
	const int PAGE_EXECUTE_READ = 0x20;
	const int PAGE_GUARD = 0x100;
	const uint STATUS_GUARD_PAGE_VIOLATION = (UInt32)0x80000001L;// Exception code = 0x80000001
	const uint STATUS_SINGLE_STEP = (UInt32)0x80000004L; // Exception code = 0x80000004
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

	// Get context of thread x64, in x64 application
	[DllImport("kernel32.dll", SetLastError = true)]
	static extern bool GetThreadContext(IntPtr hThread, ref CONTEXT64 lpContext);
	
	[DllImport("kernel32.dll")]
	private static extern bool Wow64SetThreadContext(IntPtr thread, int[] context);
	
	#endregion
	
	#region Handler Stuff
	public delegate long VectoredExceptionHandlerDelegate(EXCEPTION_POINTERS *ExceptionInfo_Ptr);

	internal static VectoredExceptionHandlerDelegate handler;
	internal static nint handlerPtr;
	public long retrn;
	public static int counter = 0;
	internal static bool _requestedUnhook;
	public int Count => counter;
	
	public unsafe static long VectoredExceptionHandler(EXCEPTION_POINTERS *ExceptionInfo_Ptr) {
		
		Ktisis.Log.Debug("Hit VEH");
		byte[] data1 = new byte[Marshal.SizeOf(typeof(IntPtr))];
		


		if (ExceptionInfo_Ptr->exceptionRecord->ExceptionCode == STATUS_GUARD_PAGE_VIOLATION) {
			counter++;
			Ktisis.Log.Debug("Caught Guard");
			if (!_requestedUnhook) 
				ExceptionInfo_Ptr->contextRecord->EFlags |= 0x100; //Set Trap flag
			else 
				RemoveVectoredExceptionHandler(handlerPtr);
			return EXCEPTION_CONTINUE_EXECUTION; //Continue Execution
		} else if (ExceptionInfo_Ptr->exceptionRecord->ExceptionCode == STATUS_SINGLE_STEP) {
			Ktisis.Log.Debug("Caught Trap");
			ResetGuardForAddress();
			Ktisis.Log.Debug("Reset Guard");
			ExceptionInfo_Ptr->contextRecord->EFlags &= 0x100;
			Ktisis.Log.Debug("Cleared Trap");
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
public unsafe struct EXCEPTION_RECORD
{
	public uint ExceptionCode;
	public uint ExceptionFlags;
	public IntPtr ExceptionRecord;
	public IntPtr ExceptionAddress;
	public uint NumberParameters;
	private uint __alignment;
	public IntPtr ExceptionInformation;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct EXCEPTION_POINTERS
{
	public EXCEPTION_RECORD *exceptionRecord;
	public CONTEXT *contextRecord;

}
[StructLayout(LayoutKind.Sequential)]
public struct CONTEXT
{
	public UInt32 ContextFlags;
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
	public UInt32 EFlags;
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

/// <summary>
/// x64
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct CONTEXT64
{
	public ulong P1Home;
	public ulong P2Home;
	public ulong P3Home;
	public ulong P4Home;
	public ulong P5Home;
	public ulong P6Home;

	public CONTEXT_FLAGS ContextFlags;
	public uint MxCsr;

	public ushort SegCs;
	public ushort SegDs;
	public ushort SegEs;
	public ushort SegFs;
	public ushort SegGs;
	public ushort SegSs;
	public uint EFlags;

	public ulong Dr0;
	public ulong Dr1;
	public ulong Dr2;
	public ulong Dr3;
	public ulong Dr6;
	public ulong Dr7;

	public ulong Rax;
	public ulong Rcx;
	public ulong Rdx;
	public ulong Rbx;
	public ulong Rsp;
	public ulong Rbp;
	public ulong Rsi;
	public ulong Rdi;
	public ulong R8;
	public ulong R9;
	public ulong R10;
	public ulong R11;
	public ulong R12;
	public ulong R13;
	public ulong R14;
	public ulong R15;
	public ulong Rip;

	public XSAVE_FORMAT64 DUMMYUNIONNAME;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 26)]
	public M128A[] VectorRegister;
	public ulong VectorControl;

	public ulong DebugControl;
	public ulong LastBranchToRip;
	public ulong LastBranchFromRip;
	public ulong LastExceptionToRip;
	public ulong LastExceptionFromRip;
}

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct XSAVE_FORMAT64
{
	public ushort ControlWord;
	public ushort StatusWord;
	public byte TagWord;
	public byte Reserved1;
	public ushort ErrorOpcode;
	public uint ErrorOffset;
	public ushort ErrorSelector;
	public ushort Reserved2;
	public uint DataOffset;
	public ushort DataSelector;
	public ushort Reserved3;
	public uint MxCsr;
	public uint MxCsr_Mask;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
	public M128A[] FloatRegisters;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
	public M128A[] XmmRegisters;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 96)]
	public byte[] Reserved4;
}

[StructLayout(LayoutKind.Sequential)]
public struct M128A
{
	public ulong High;
	public long Low;

	public override string ToString()
	{
		return string.Format("High:{0}, Low:{1}", this.High, this.Low);
	}
}

public enum CONTEXT_FLAGS : uint
{
	CONTEXT_i386 = 0x10000,
	CONTEXT_i486 = 0x10000,   //  same as i386
	CONTEXT_CONTROL = CONTEXT_i386 | 0x01, // SS:SP, CS:IP, FLAGS, BP
	CONTEXT_INTEGER = CONTEXT_i386 | 0x02, // AX, BX, CX, DX, SI, DI
	CONTEXT_SEGMENTS = CONTEXT_i386 | 0x04, // DS, ES, FS, GS
	CONTEXT_FLOATING_POINT = CONTEXT_i386 | 0x08, // 387 state
	CONTEXT_DEBUG_REGISTERS = CONTEXT_i386 | 0x10, // DB 0-3,6,7
	CONTEXT_EXTENDED_REGISTERS = CONTEXT_i386 | 0x20, // cpu specific extensions
	CONTEXT_FULL = CONTEXT_CONTROL | CONTEXT_INTEGER | CONTEXT_SEGMENTS,
	CONTEXT_ALL = CONTEXT_CONTROL | CONTEXT_INTEGER | CONTEXT_SEGMENTS |  CONTEXT_FLOATING_POINT | CONTEXT_DEBUG_REGISTERS |  CONTEXT_EXTENDED_REGISTERS
}
#endregion