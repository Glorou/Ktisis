using System.Runtime.InteropServices;

namespace Ktisis.Structs.Common;

[StructLayout(LayoutKind.Explicit, Size = 0x60)]
public unsafe struct DataHandle {
	[FieldOffset(0x10)] public byte* FileHandle;  //This is the file 1:1 from disk
}
