using System.Runtime.InteropServices;

using FFXIVClientStructs.Havok.Common.Base.Math.Vector;

namespace Ktisis.Structs.Havok;

[StructLayout(LayoutKind.Explicit)]
public struct LookAtIkRange {
	/// Limiting angles in the up direction; must be in range [ -pi/2, pi/2 ]
	[FieldOffset(0x0)] public float m_limitAngleUp;   // Example:  pi/4  (45deg up)
	[FieldOffset(4)] public float m_limitAngleDown; // Example: -pi/6  (30deg down)

	/// Limiting angle in the side direction; must be in range [ -pi, pi ]
	[FieldOffset(0x8)] public float m_limitAngleLeft; // Example:  pi/2  (90deg left)
	[FieldOffset(0xC)] public float m_limitAngleRight;// Example: -pi/2  (90deg right)

	/// Up unit vector in model space, must be
	/// perpendicular to setup.m_limitAxisMS.
	/// Side vector is calculated as cross( setup.m_limitAxisMS, m_upAxisMS )
	[FieldOffset(0x10)] public hkVector4f m_upAxisMS;
}
