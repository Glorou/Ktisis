using System.Numerics;
using System.Runtime.InteropServices;

namespace Ktisis.Structs.Vfx.Apricot.Data;

[StructLayout(LayoutKind.Explicit)]
public struct RgbFunctionCurve {
	[FieldOffset(0x00)] public uint Data;
	[FieldOffset(0x04)] public unsafe fixed byte Keys[0x10];

	public ushort GetKeyCount() => (ushort)(this.Data >> 9);

	public unsafe RgbKey* GetKey(int index) {
		fixed (void* ptr = this.Keys) {
			return (RgbKey*)ptr + index;
		}
	}

	[StructLayout(LayoutKind.Explicit, Size = 0x10)]
	public struct RgbKey {
		[FieldOffset(0)] public uint Data;
		[FieldOffset(4)] public Vector3 Color;
	}
}