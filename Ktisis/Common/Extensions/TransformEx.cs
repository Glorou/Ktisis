using System;
using System.Numerics;

using FFXIVClientStructs.Havok.Common.Base.Math.QsTransform;
using FFXIVClientStructs.Havok.Common.Base.Math.Quaternion;
using FFXIVClientStructs.Havok.Common.Base.Math.Vector;
using CSTransform = FFXIVClientStructs.FFXIV.Client.Graphics.Transform;

using Ktisis.Common.Utility;

namespace Ktisis.Common.Extensions;

public static class TransformEx {
	// Vector3

	public static Vector3 ModelToWorldPos(this Vector3 target, Transform offset)
		=> Vector3.Transform(target, offset.Rotation) * offset.Scale;

	public static Vector3 ModelToWorldPos(this Vector3 target, CSTransform offset)
		=> Vector3.Transform(target, offset.Rotation) * (Vector3)offset.Scale;
	public static Vector3 WorldToModelPos(this Vector3 target, Transform offset) {
		return Vector3.Transform(
			target - offset.Position,
			Quaternion.Inverse(offset.Rotation)
		) / offset.Scale;
	}
	
	// hkVector4f
	
	public static Vector3 ToVector3(this hkVector4f hkVec) => new(hkVec.X, hkVec.Y, hkVec.Z);
	public static hkVector4f ToHavok(this Vector3 v) => new() { X = v.X, Y = v.Y, Z = v.Z, W = 0.0f };
	public static hkVector4f ToHavokRounded(this Vector3 v) => new() {
		X = MathF.Round(v.X, 4),
		Y = MathF.Round(v.Y, 4),
		Z = MathF.Round(v.Z, 4),
		W = 0.0f
	};
	
	// hkQuaternionf
	
	public static Quaternion ToQuaternion(this hkQuaternionf hkQuat) => new(hkQuat.X, hkQuat.Y, hkQuat.Z, hkQuat.W);

	public static hkQuaternionf ToHavok(this Quaternion quat) => new() { X = quat.X, Y = quat.Y, Z = quat.Z, W = quat.W };
	public static hkQuaternionf ToHavokRounded(this Quaternion quat) => new() {
		X = MathF.Round(quat.X, 4),
		Y = MathF.Round(quat.Y, 4),
		Z = MathF.Round(quat.Z, 4),
		W = MathF.Round(quat.W, 4)
	};
	
	/*
	*	Todo: SetMulInverseMul needs the following instructions
	*	Todo: Transform.SetInverse(Transform self)
	*		Todo: Vector3.SetRotatedInverseDir(Quaternion rot, Vector3 pos)  -> hkVector4f.inl
	*		Todo: SetNeg<3/4(inhk)>(Vector3 Pos) -> hkSseVector4.inl
	*		Todo: Quaternion.SetInverse(Quaternion self)  -> hkQuaternionf.inl
	*		Todo: Vector3.SetReciprocal(Vector3 Scale) -> hkSseVector4.inl
	*
	*  Todo: SetMul(Transform a, Transform b)
	*		Todo:
	*
	*/
	extension(Transform) {
		public static Transform setMulInverseMul(Transform original, Transform target) {
			Transform o = new Transform();
			o.Position = target.Position - original.Position;
			o.Rotation = Quaternion.Multiply(target.Rotation, Quaternion.Inverse(original.Rotation));
			o.Scale =  target.Scale / original.Scale;
			return o;
		}
		public static hkQsTransformf setMul(hkQsTransformf original, Transform target) {
			hkQsTransformf o = new hkQsTransformf();
			o.Translation = original.Translation + target.Position.ToHavok();
			o.Rotation = Quaternion.Normalize(Quaternion.Multiply(original.Rotation.ToQuaternion(), target.Rotation)).ToHavok();
			o.Scale = original.Scale * target.Scale.ToHavok();
			return o;
		}
		public static Transform? setMul(Transform? original, Transform target) {
			if (original == null)
				return null;
			Transform o = new Transform();
			o.Position = original.Position + target.Position;
			o.Rotation = Quaternion.Normalize(Quaternion.Multiply(original.Rotation, target.Rotation));
			o.Scale = original.Scale * target.Scale;
			return o;
		}
	}
}
