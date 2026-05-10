using System.Runtime.InteropServices;

using FFXIVClientStructs.Havok.Common.Base.Math.Vector;

namespace Ktisis.Structs.Havok;

[StructLayout(LayoutKind.Explicit)]
public struct LookAtIkSetup {
	
	// Forward vector in the local space of the bone; must be a unit vector
	[FieldOffset(0x0)] public hkVector4f m_fwdLS;
	
	/*"Eye" position in the local space of the bone.
	 Defaults to (0,0,0). If the offset is not (0,0,0) (the eye is not located
	 at the joint's position), the required correction is calculated. */
	[FieldOffset(0x10)] public hkVector4f m_eyePositionLS;
	
	/// Axis of the limiting cone, specified in model space; must be a unit vector. Must be perpendicular to RangeLimits::m_upAxisMS (if specified).
	[FieldOffset(0x20)] public hkVector4f m_limitAxisMS;
	
	
	/// Angle of the limiting cone; must be in range [ 0, pi ]
	[FieldOffset(0x30)] public float m_limitAngle;
	
}
