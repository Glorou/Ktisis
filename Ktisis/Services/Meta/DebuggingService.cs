using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;
using FFXIVClientStructs.Havok.Common.Base.Math.QsTransform;
using DWORD64 = System.UInt64;
using DWORD = System.Int32;
using WORD = ushort;
using ULONGLONG = System.UInt64;
using LONGLONG = System.Int64;


using Ktisis.Core.Attributes;

namespace Ktisis.Services.Meta;


[Singleton]
public unsafe class DebuggingService {
	

	[DllImport("ipfd.dll")]
	static extern bool AttachVEH(); 
	[DllImport("ipfd.dll")]
	static extern bool DetachVEH();
	[DllImport("ipfd.dll")]
	static extern bool MonitorAddress(void* ptr, uint sizeOfObject);
	[DllImport("ipfd.dll")]
	static extern bool SetCallback(void* callback);
	
	public delegate void CallbackFunction(nint ReferencedAddress, nint[] FramePoiners);


	public static CallbackFunction c;
	public void RegisterExceptionHandler() => AttachVEH();
	public void UnregisterExceptionHandler() => DetachVEH();

	public void RegisterCallback()
	{
		c = Callback;
		SetCallback((void*)Marshal.GetFunctionPointerForDelegate(c));
	}
	
	public bool SetupGuardForAddress(IntPtr address) => MonitorAddress((void*)address, (uint)sizeof(hkQsTransformf));

	public void Callback(nint ReferencedAddress, nint[] FramePoiners)
	{
		Ktisis.Log.Debug("Callback Hit");
	}
	
	
}

