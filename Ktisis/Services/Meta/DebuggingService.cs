using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using DWORD64 = System.UInt64;
using DWORD = System.Int32;
using WORD = System.SByte;
using ULONGLONG = System.UInt64;
using LONGLONG = System.Int64;

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
	static extern bool GetThreadContext(IntPtr hThread, ref CONTEXT lpContext);
	
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
			ExceptionInfo_Ptr->contextRecord->EFlags &= ~0x100;
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
		
		var newOption = pageInfo.Protect | 0x00000100;
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


  [StructLayout(LayoutKind.Sequential, Pack = 16)]
    public unsafe struct CONTEXT
    {
        //
        // Register parameter home addresses.
        //
        // N.B. These fields are for convience - they could be used to extend the
        //      context record in the future.
        //

        public DWORD64 P1Home;
        public DWORD64 P2Home;
        public DWORD64 P3Home;
        public DWORD64 P4Home;
        public DWORD64 P5Home;
        public DWORD64 P6Home;

        //
        // Control flags.
        //
        public DWORD ContextFlags;
        public DWORD MxCsr;

        //
        // Segment Registers and processor flags.
        //
        public WORD SegCs;
        public WORD SegDs;
        public WORD SegEs;
        public WORD SegFs;
        public WORD SegGs;
        public WORD SegSs;
        public DWORD EFlags;

        //
        // Debug registers
        //
        public DWORD64 Dr0;
        public DWORD64 Dr1;
        public DWORD64 Dr2;
        public DWORD64 Dr3;
        public DWORD64 Dr6;
        public DWORD64 Dr7;

        //
        // Integer registers.
        //
        public DWORD64 Rax;
        public DWORD64 Rcx;
        public DWORD64 Rdx;
        public DWORD64 Rbx;
        public DWORD64 Rsp;
        public DWORD64 Rbp;
        public DWORD64 Rsi;
        public DWORD64 Rdi;
        public DWORD64 R8;
        public DWORD64 R9;
        public DWORD64 R10;
        public DWORD64 R11;
        public DWORD64 R12;
        public DWORD64 R13;
        public DWORD64 R14;
        public DWORD64 R15;

        //
        // Program counter.
        //
        public DWORD64 Rip;

        //
        // Floating point state.
        //
        public DUMMYUNIONNAME dummyUnion;

        //
        // Vector registers.
        //
        M128A* VectorRegister;
        public DWORD64 VectorControl;

        //
        // Special debug control registers.
        //
        public DWORD64 DebugControl;
        public DWORD64 LastBranchToRip;
        public DWORD64 LastBranchFromRip;
        public DWORD64 LastExceptionToRip;
        public DWORD64 LastExceptionFromRip;
    }

    struct XMM_SAVE_AREA32
    {

    }

    public unsafe struct DUMMY
    {
        M128A* Header;
        M128A* Legacy;
        M128A Xmm0;
        M128A Xmm1;
        M128A Xmm2;
        M128A Xmm3;
        M128A Xmm4;
        M128A Xmm5;
        M128A Xmm6;
        M128A Xmm7;
        M128A Xmm8;
        M128A Xmm9;
        M128A Xmm10;
        M128A Xmm11;
        M128A Xmm12;
        M128A Xmm13;
        M128A Xmm14;
        M128A Xmm15;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct DUMMYUNIONNAME
    {
        [FieldOffset(0)]
        XMM_SAVE_AREA32 FltSave;
        [FieldOffset(1)]
        DUMMY Dummy;
    }

    public struct M128A
    {
        ULONGLONG Low;
        LONGLONG High;
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