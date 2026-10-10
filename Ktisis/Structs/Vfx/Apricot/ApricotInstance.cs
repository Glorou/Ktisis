using System.Runtime.InteropServices;

namespace Ktisis.Structs.Vfx.Apricot;

[StructLayout(LayoutKind.Explicit, Size = 0x4A8)]
public struct ApricotInstance {
	[FieldOffset(0)] public unsafe nint* _vfTable;
	
	[FieldOffset(0x1D4)] public float LocalTime;
	[FieldOffset(0x1DC)] public float TimeScale;

	[FieldOffset(0x1F8)] public unsafe ApricotInstance* Root;
	[FieldOffset(0x200)] public unsafe ApricotInstance* Parent;
	[FieldOffset(0x208)] public unsafe ApricotDocument* Document;
	[FieldOffset(0x210)] public unsafe ApricotInstance* Child;
	[FieldOffset(0x218)] public unsafe ApricotInstance* ChildLast;
	[FieldOffset(0x220)] public unsafe ApricotInstance* Sibling;
	
	[FieldOffset(0x18C)] public float F1;
	[FieldOffset(0x1BC)] public float F2;
	[FieldOffset(0x1D4)] public float F3;
	[FieldOffset(0x1F4)] public float F4;
	[FieldOffset(0x49D)] public byte State;
}
