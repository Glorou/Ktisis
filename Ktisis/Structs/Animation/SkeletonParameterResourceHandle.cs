using System;
using System.Numerics;
using System.Runtime.InteropServices;

using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using FFXIVClientStructs.Havok.Common.Base.Math.Vector;

namespace Ktisis.Structs.Animation;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct SkeletonParameterResourceHandle  {
	[FieldOffset(0x00)] public ResourceHandle ResourceHandle;

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

	public struct Group {
		
	}
	
}
