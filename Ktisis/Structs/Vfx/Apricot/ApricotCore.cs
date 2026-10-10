using System;
using System.Runtime.InteropServices;

using Dalamud.Utility.Signatures;

using InteropGenerator.Runtime.Attributes;

namespace Ktisis.Structs.Vfx.Apricot;

[GenerateInterop]
[StructLayout(LayoutKind.Explicit)]
public unsafe partial struct ApricotCore {
	[FieldOffset(0xD30)] public unsafe DataContainer* Data;

	[StructLayout(LayoutKind.Explicit)]
	public struct DataContainer {
		[FieldOffset(0x2030)] public unsafe fixed byte Instances[2048 * InstanceContainer.Size];

		public unsafe InstanceContainer* GetIndex(uint index) {
			if (index > 2048)
				throw new IndexOutOfRangeException($"Index {index} is out of range.");
			
			fixed (byte* data = this.Instances) {
				return (InstanceContainer*)data + index;
			}
		}

		public unsafe Span<InstanceContainer> GetInstancesSpan() {
			fixed (byte* data = this.Instances) {
				return new Span<InstanceContainer>(data, InstanceContainer.Size);
			}
		}
	}
	
	[StaticAddress("48 8D 0D ?? ?? ?? ?? 66 44 89 A3", 0, isPointer: true)]
	public static partial ApricotCore* Instance();
}
