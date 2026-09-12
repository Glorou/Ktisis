using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Dalamud.Plugin.Services;

using Ktisis.Core.Attributes;

namespace Ktisis.Services.Meta;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct Access_Info {
	public bool inUse;
	public UInt64 addressAccessed;
	public UInt64* frameTrace;
}

[StructLayout(LayoutKind.Sequential)]
public struct Address_Info {
	public UInt64 addressToWatch;
	public UInt64 sizeOfType;
	public bool watched;
}
public struct Managed_Access {
	public UInt64 addressAccessed;
	public List<UInt64> frameTrace = new List<ulong>();
	public Managed_Access() {
		addressAccessed = 0;
	}
}
[Singleton]
public unsafe class DebuggingService {

	private readonly IFramework _framework;
	public DebuggingService(
		IFramework framework
	) {
		this._framework = framework;
	}
	

	[DllImport("ipfd.dll")]
	static extern bool AttachVEH(); 
	[DllImport("ipfd.dll")]
	static extern bool DetachVEH();
	[DllImport("ipfd.dll")]
	static extern bool RefreshAddresses();
	[DllImport("ipfd.dll")]
	static extern bool RemoveAddress(UInt64 address);
	
	[DllImport("ipfd.dll")]
	static extern void SetupParams(Access_Info* array, int sizeOfInfo, int framesToCap, Address_Info* addresses, int sizeOfAddresses);


	public List<Managed_Access> Accesses = new List<Managed_Access>();
	private Access_Info* array;
	private Address_Info* addresses;

	
	public void Setup() {
		AttachVEH();
		array = (Access_Info*)Marshal.AllocHGlobal(sizeof(Access_Info) * 20);
		addresses = (Address_Info*)Marshal.AllocHGlobal(sizeof(Address_Info) * 20);
		for (var i = 0; i < 20; i++) {
			addresses[i].addressToWatch = 0;
			array[i].frameTrace = (ulong*)Marshal.AllocHGlobal(sizeof(ulong) * 3);
		}

		
		SetupParams(array, 20, 3,  addresses, 20);
		this._framework.Update += this.Heartbeat;
	}

	public void Destroy() {
		this._framework.Update -= this.Heartbeat;
		DetachVEH();
		for (var i = 0; i < 20; i++) {
			if (addresses[i].watched)
				RemoveAddress(addresses[i].addressToWatch);
		}
		
	}

	public void SetupGuardForAddress(IntPtr address, UInt64 sizeOfType) {
		for (var i = 0; i < 20; i++) {
			if (addresses[i].addressToWatch != 0) {
				addresses[i].addressToWatch = (ulong)address;
				addresses[i].sizeOfType = sizeOfType;
				RefreshAddresses();
				return;
			}
		}
	}

	public void Heartbeat(IFramework framework) {
		for (var i = 0; i < 20; i++) {
			if (this.array[i].inUse) {
				Managed_Access a = new();
				a.addressAccessed = this.array[i].addressAccessed;
				this.array[i].addressAccessed = 0;
				for (int j = 0; j < 3; j++) {
					a.frameTrace.Add(this.array[i].frameTrace[j]);
					this.array[i].frameTrace[j] = 0;
				}
				this.Accesses.Add(a);
				Ktisis.Log.Debug($"{a.addressAccessed:X8} : [0]{a.frameTrace[0]:X8} [1]{a.frameTrace[1]:X8} [2]{a.frameTrace[2]:X8}");
				this.array[i].inUse = false;
			}
		}
	}
	
}

