using System;
using System.Collections.Generic;

using FFXIVClientStructs;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using FFXIVClientStructs.Interop;

using Ktisis.Common.Extensions;
using Ktisis.Common.Utility;
using Ktisis.Core.Attributes;
using Ktisis.Data.Files;
using Ktisis.Scene.Entities.Game;
using Ktisis.Structs.Common;

namespace Ktisis.Services.Data;

[Singleton]
public unsafe class PoseRemapperService {

	private List<SkeletonHelper> skeletons;
	private Pointer<SkeletonResourceHandle>[] _retargetSkeletonResourceHandles; // 0101, 0104, 0201, 0501, 0601, 0901, 1101
	public PoseRemapperService() {
		this._retargetSkeletonResourceHandles = CharacterUtility.Instance()->RetargetSkeletonResourceHandles.ToArray();
	}

	public Span<uint> GetConvertsOnSkeleton(ActorEntity actor) => actor.GetCharacter()->Skeleton->SkeletonResourceHandles[0]->SkeletonData.SkeletonMappers;

	
	//setMul
	public void Convert(Skeleton* to, uint from, ref PoseFile pose) {
		var handle = to->SkeletonResourceHandles[0];
		if (!handle->SkeletonData.SkeletonMappers.Contains(from))
			return;
		var mapper = to->SkeletonResourceHandles[0]->SkeletonMapperDict1[from].Value;
		for (var i = 0; i < mapper->Mapping.SkeletonA.ptr->Bones.Length; i++) {
			var name = mapper->Mapping.SkeletonA.ptr->Bones[i].Name;
			pose.Bones.TryGetValue(name.String, out var bone);
			if(bone == null)
				continue;
			var transform = mapper->Mapping.SimpleMappings[i].AFromBTransform;
			pose.Bones[name.String!] = Transform.SetMul(bone, transform);
		}
	}
	
	
}

public unsafe struct SkeletonHelper {
	public SkeletonResourceHandle* handle;
	public uint skeletonId;
}