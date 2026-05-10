using System;
using System.Numerics;

using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using FFXIVClientStructs.Havok.Animation.Rig;

using Ktisis.Interop;
using Ktisis.Structs.Animation;
using Ktisis.Structs.Havok;

namespace Ktisis.Editor.Posing.Ik.LookAt;

public class LookAtSolver(IkModule module) : IDisposable  {
	private readonly Alloc<LookAtIkSetup> AllocIkSetup = new(16);
	private readonly Alloc<LookAtIkRange> AllocIkRange = new(8);

	public unsafe LookAtIkSetup* LookAtSetup => this.AllocIkSetup.Data;
	public unsafe LookAtIkRange* LookAtRange => this.AllocIkRange.Data;

	public unsafe void Setup() {
		if (this.AllocIkSetup.Address == nint.Zero)
			throw new Exception("Allocation for IkSetup failed.");
		if (this.AllocIkRange.Address == nint.Zero)
			throw new Exception("Allocation for IkRange failed.");

		*this.LookAtSetup = new LookAtIkSetup();
		*this.LookAtRange = new LookAtIkRange();
	}

	public unsafe void Solve(PartialSkeleton partialSkeleton, Vector3 lookAt, Skeleton.Bone bone) {

		SkeletonParameterResourceHandle* skp = partialSkeleton.SkeletonParameterResourceHandle;
		var boneName = bone.BoneName;

		SkeletonParameterResourceHandle.Element? element = null;
		for (short i = 0; i < skp->GroupCount; i++) {
			for (short j = 0; i < skp->Groups[i].ElementCount; j++) {
				if (boneName.Equals(skp->Groups[i].Elements[j].BoneName)) {
					element = skp->Groups[i].Elements[j];
				}
			}
		}

		if (element == null)
			return;

		var setupParam = skp->Parameters[element.Value.SetupParameterIndex];

		LookAtRange.
		this.LookAtSetup->m_limitAngle = setupParam.Limit_Angle;
		this.LookAtSetup.

	}
	
	public bool IsDisposed { get; private set; }
	
	public void Dispose() {
		this.IsDisposed = true;
		this.AllocIkSetup.Dispose();
		this.AllocIkRange.Dispose();
		GC.SuppressFinalize(this);
	}
}
