using System;
using System.Numerics;
using System.Runtime.InteropServices;

using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using FFXIVClientStructs.Havok.Common.Base.Math.Vector;

using InteropGenerator.Runtime.Attributes;

namespace Ktisis.Structs.Animation;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct SkeletonParameterResourceHandle  {
	[FieldOffset(0x00)] public ResourceHandle ResourceHandle;

	//E0 some sort of flag, I think E0 and E1 are byte counters for how many objects the arrays have
	//E8 pointer to E1 bytes representing the # of elements in each group
	[FieldOffset(0xE0)] public byte ParameterCount;
	[FieldOffset(0xE1)] public byte GroupCount;
	[FieldOffset(0xE8)] public byte* GroupElementCount;
	[FieldOffset(0xF0)] public LookAtParam* Parameters;


	
	
	[StructLayout(LayoutKind.Sequential, Size = 0x56)]
	public struct LookAtParam {
		public hkVector4f m_fwdLS;
		public Vector3 ForwardRotation;
		public float Limit_Angle;
		public Vector3 Eye_positions;
		public Int32 Flags;
		public float Gain;
		public uint Index;
	}
	
	[StructLayout(LayoutKind.Explicit, Size = 0x30)]
	public struct Group {
		[FieldOffset(0x00), FixedSizeArray(isString: true)] public FixedSizeArray8<byte> GroupId; //char[8]
	}
	
}
