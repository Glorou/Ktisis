using System.Runtime.InteropServices;
using Ktisis.Structs.Vfx.Apricot.Data;

namespace Ktisis.Structs.Vfx.Apricot;

[StructLayout(LayoutKind.Explicit, Size = 0x238)]
public struct ApricotParticle {
	[FieldOffset(0x70)] public unsafe RgbFunctionCurve* Rgb;
}