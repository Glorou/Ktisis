using System.Numerics;

using FFXIVClientStructs.Havok.Common.Base.Container.Array;
using FFXIVClientStructs.Havok.Common.Base.Math.Vector;

namespace Ktisis.Common.Extensions;

public static class HavokEx {
	public static T[] Copy<T>(this hkArray<T> array) where T : unmanaged {
		var len = array.Length;
		var result = new T[len];
		for (var i = 0; i < len; i++)
			result[i] = array[i];
		return result;
	}

	public unsafe static void Initialize<T>(hkArray<T>* array, T* data = null, int length = 0) where T : unmanaged {
		array->Data = data;
		array->Length = length;
		*(uint*)&(array->CapacityAndFlags) = 0x80000000;
	}

	extension(hkVector4f) {
		public static hkVector4f operator +(hkVector4f left, hkVector4f right) {
			var o = new hkVector4f();
			o.W = left.W + right.W;
			o.X = left.X + right.X;
			o.Y = left.Y + right.Y;
			o.Z = left.Z + right.Z;
			return o;
		}
		public static hkVector4f operator *(hkVector4f left, hkVector4f right) {
			var o = new hkVector4f();
			o.W = left.W * right.W;
			o.X = left.X * right.X;
			o.Y = left.Y * right.Y;
			o.Z = left.Z * right.Z;
			return o;
		}
	}
}
