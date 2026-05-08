using System.Runtime.InteropServices;

namespace Ktisis.Structs.Havok;

[StructLayout(LayoutKind.Explicit)]
public struct LookAtIkRange {
	/// Limiting angles in the up direction; must be in range [ -pi/2, pi/2 ]
	[FieldOffset(0)] public float m_limitAngleUp;   // Example:  pi/4  (45deg up)
	[FieldOffset(8)] public float m_limitAngleDown; // Example: -pi/6  (30deg down)

	/// Limiting angle in the side direction; must be in range [ -pi, pi ]
	[FieldOffset(10)] public float m_limitAngleLeft; // Example:  pi/2  (90deg left)
	[FieldOffset(18)] public float m_limitAngleRight;// Example: -pi/2  (90deg right)

	/// Up unit vector in model space, must be
	/// perpendicular to setup.m_limitAxisMS.
	/// Side vector is calculated as cross( setup.m_limitAxisMS, m_upAxisMS )
	[FieldOffset(20)] public float m_upAxisMS;
}
