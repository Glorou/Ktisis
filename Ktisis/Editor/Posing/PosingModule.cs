using System;
using System.Runtime.CompilerServices;

using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using Dalamud.Plugin;
using Dalamud.Utility.Signatures;

using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.Havok.Animation.Rig;
using FFXIVClientStructs.Havok.Common.Base.Math.QsTransform;

using Ktisis.Data.Json;
using Ktisis.Editor.Context;
using Ktisis.Interop.Hooking;
using Ktisis.Interop.Ipc;
using Ktisis.Scene.Entities.Game;
using Ktisis.Services.Game;

namespace Ktisis.Editor.Posing;

public unsafe delegate void SkeletonInitHandler(IGameObject owner, Skeleton* skeleton, ushort partialId);

public sealed class PosingModule : HookModule {
	private readonly PosingManager Manager;
	private readonly ActorService _actors;
	private readonly IpcProvider _ipc;

	public event SkeletonInitHandler? OnSkeletonInit;
	public event Action? OnDisconnect;

	public PosingModule(
		IHookMediator hook,
		PosingManager manager,
		ActorService actors,
		ContextManager contextManager,
		IDalamudPluginInterface dpi,
		JsonFileSerializer fileSerializer
	) : base(hook) {
		this.Manager = manager;
		this._actors = actors;
		this._ipc = new IpcProvider(contextManager, dpi, fileSerializer);
	}
	
	// Module interface
	
	public bool IsEnabled { get; private set; }

	public override void EnableAll() {
		base.EnableAll();
		this.IsEnabled = true;
		this._ipc.InvokePosingChanged(this.IsEnabled);
	}

	public override void DisableAll() {
		base.DisableAll();
		this.IsEnabled = false;
		this._ipc.InvokePosingChanged(this.IsEnabled);
	}
	
	// Posing hooks - thanks to perchbird (@lmcintyre) for his initial implementation of these.
	// https://github.com/ktisis-tools/Ktisis/pull/8
	
	// SetBoneModelSpace
	
	[Signature("48 8B C4 48 89 58 18 55 56 57 41 54 41 55 41 56 41 57 48 81 EC ?? ?? ?? ?? 0F 29 70 B8 0F 29 78 A8 44 0F 29 40 ?? 44 0F 29 48 ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 48 8B B1", DetourName = nameof(SetBoneModelSpace))]
	private Hook<SetBoneModelSpaceDelegate> _setBoneModelSpaceHook = null!;
	private delegate ulong SetBoneModelSpaceDelegate(nint partial, ushort boneId, nint transform, bool enableSecondary, bool enablePropagate);

	private ulong SetBoneModelSpace(nint partial, ushort boneId, nint transform, bool enableSecondary, bool enablePropagate) => this._setBoneModelSpaceHook.Original.Invoke(partial, boneId, transform, enableSecondary, enablePropagate);
	
	// SyncModelSpace

	[Signature("48 83 EC 18 80 79 38 00", DetourName = nameof(SyncModelSpace))]
	private Hook<SyncModelSpaceDelegate> _syncModelSpaceHook = null!;
	private unsafe delegate void SyncModelSpaceDelegate(hkaPose* pose);

	private unsafe void SyncModelSpace(hkaPose* pose) {
		if (this.Manager.IsSolvingIk)
			this._syncModelSpaceHook.Original(pose);
		if(pose == this.oldPose)
			this.OnTick();
		// do nothing
	}

	// call externally to overwrite posed face with unfrozen model pose
	public unsafe void SyncFaceModelSpace(ActorEntity actor) {
		var cBase = actor.GetCharacter();
		var skeleton = cBase->Skeleton;
		var pose = skeleton->PartialSkeletons[1].GetHavokPose(0);
		this._syncModelSpaceHook.Original(pose);
	}
	
	// CalcBoneModelSpace
	/*
	[Signature("40 53 48 83 EC 10 4C 8B 49 28", DetourName = nameof(CalcBoneModelSpace))]
	private Hook<CalcBoneModelSpaceDelegate> _calcBoneModelSpaceHook = null!;
	private unsafe delegate hkQsTransformf* CalcBoneModelSpaceDelegate(hkaPose* pose, int boneIdx);

	private unsafe hkQsTransformf* CalcBoneModelSpace(hkaPose* pose, int boneIdx) {
		if (this.Manager.IsSolvingIk)
			return this._calcBoneModelSpaceHook.Original(pose, boneIdx);

		if (boneIdx == 1 && pose->Skeleton->Bones[boneIdx].Name.String == "n_hara") {
			HavokPosing.CalcCachedAbdomenModelTransform(pose, boneIdx);
		}
		return pose->ModelPose.Data + boneIdx;
	}
	*/
	// LookAtIK

	[Signature("48 8B C4 48 89 58 08 48 89 70 10 F3 0F 11 58", DetourName = nameof(LookAtIK))]
	private Hook<LookAtIKDelegate> _lookAtIKHook = null!;
	private delegate nint LookAtIKDelegate(nint a1, nint a2, nint a3, float a4, nint a5, nint a6);

	private nint LookAtIK(nint a1, nint a2, nint a3, float a4, nint a5, nint a6) => nint.Zero;
	
	// KineDriver

	[Signature("48 8B C4 55 57 48 83 EC 58", DetourName = nameof(KineDriverDetour))]
	private Hook<KineDriverDelegate> _kineDriverHook = null!;
	private delegate nint KineDriverDelegate(nint a1, nint a2);

	private nint KineDriverDetour(nint a1, nint a2) {
		return nint.Zero;
	}
	
	// AnimFrozen

	[Signature("E8 ?? ?? ?? ?? 0F B6 F8 84 C0 74 12", DetourName = nameof(AnimFrozen))]
	private Hook<AnimFrozenDelegate> _animFrozenHook = null!;
	private delegate byte AnimFrozenDelegate(nint a1, int a2);

	private byte AnimFrozen(nint a1, int a2) => this._animFrozenHook.Original.Invoke(a1, a2);
	
	// UpdatePos

	[Signature("E8 ?? ?? ?? ?? 84 DB 74 3A", DetourName = nameof(UpdatePosDetour))]
	private Hook<UpdatePosDelegate> _updatePosHook = null!;
	private delegate void UpdatePosDelegate(nint gameObject);

	private void UpdatePosDetour(nint gameObject) { }
	
	// Pose preservation handlers

	[Signature("E8 ?? ?? ?? ?? 48 C1 E5 08", DetourName = nameof(SetSkeletonDetour))]
	private Hook<SetSkeletonDelegate> _setSkeletonHook = null!;
	private unsafe delegate byte SetSkeletonDelegate(Skeleton* skeleton, ushort partialId, nint a3);
	private unsafe byte SetSkeletonDetour(Skeleton* skeleton, ushort partialId, nint a3) {
		var result = this._setSkeletonHook.Original(skeleton, partialId, a3);
		try {
			this.HandleRestoreState(skeleton, partialId);
		} catch (Exception err) {
			Ktisis.Log.Error($"Failed to handle SetSkeleton:\n{err}");
		}
		return result;
	}
	
	[Signature("48 8B C4 48 89 58 ?? 48 89 70 ?? 48 89 78 ?? 55 41 54 41 55 41 56 41 57 48 8D 68 ?? 48 81 EC ?? ?? ?? ?? 83 7D ?? ?? 4D 8B F1 0F 29 70 ?? 49 8B F8")]
	private hkaBlendDelegate _hkaBlend = null!;
	private unsafe delegate void hkaBlendDelegate(
		hkQsTransformf* dstOut,
		hkQsTransformf* srcL,
		hkQsTransformf* srcR,
		float* alpha,
		int n,
		int blendMode,
		int rotationMode
	);
	
	private unsafe hkaPose* newPose;
	private unsafe hkaPose* oldPose;
	private Single weight;


	public unsafe void OnTick() {
		if (this.newPose == null || this.oldPose == null)
			return;
		float* ptr = stackalloc float[1];
		*ptr = .5f;
		
		this.newPose->LocalPose[0] = this.oldPose->LocalPose[0];
		this.newPose->LocalPose[1] = this.oldPose->LocalPose[1];
		this.newPose->LocalPose[10] = this.oldPose->LocalPose[10];
		this.newPose->LocalPose[11] = this.oldPose->LocalPose[11];
		//set the root to be the same
		var a = this.oldPose->LocalPose[11];
		this._hkaBlend.Invoke(this.oldPose->LocalPose.Data, this.newPose->LocalPose.Data,this.oldPose->LocalPose.Data, ptr, this.oldPose->LocalPose.Length, 0x0, 0x0);
		var b = this.oldPose->LocalPose.Data[11];
	}
	public unsafe void SetupActorBlend(ActorEntity actor) {
		this.newPose = IMemorySpace.GetAnimationSpace()->Malloc<hkaPose>();
		this.oldPose = (hkaPose*)actor.GetHuman()->Skeleton->PartialSkeletons->HavokPoses[0];
		this.newPose->Ctor1(hkaPose.PoseSpace.LocalSpace, actor.GetHuman()->Skeleton->PartialSkeletons->Skeleton->PartialSkeletons[0].GetHavokPose(0)->Skeleton, &this.newPose->LocalPose);
	}


	private unsafe void HandleRestoreState(Skeleton* skeleton, ushort partialId) {
		if (!this.Manager.IsValid || !this.IsEnabled || skeleton->PartialSkeletons == null) return;
		
		var partial = skeleton->PartialSkeletons[partialId];
		var pose = partial.GetHavokPose(0);
		if (pose == null) return;

		this._syncModelSpaceHook.Original(pose);

		var actor = this._actors.GetSkeletonOwner(skeleton);
		if (actor == null) return;
		Ktisis.Log.Verbose($"Restoring partial {partialId} for {actor.Name} ({actor.ObjectIndex})");
		
		if (partialId == 0)
			this._updatePosHook.Original(actor.Address);
		this.OnSkeletonInit?.Invoke(actor, skeleton, partialId);
	}

	[Signature("E8 ?? ?? ?? ?? 84 C0 0F 44 FE", DetourName = nameof(DisconnectDetour))]
	private Hook<DisconnectDelegate> _disconnectHook = null!;
	private delegate nint DisconnectDelegate(nint a1);

	private nint DisconnectDetour(nint a1) {
		try {
			this.OnDisconnect?.Invoke();
		} catch (Exception err) {
			Ktisis.Log.Error(err.ToString());
		}
		return this._disconnectHook.Original(a1);
	}
}
