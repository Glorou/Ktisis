using Dalamud.Game.ClientState.Objects.Types;

using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.Havok.Animation.Rig;
using FFXIVClientStructs.Havok.Common.Base.Math.QsTransform;

using Ktisis.Common.Utility;
using Ktisis.Scene.Entities.Game;
using Ktisis.Common.Extensions;
using Ktisis.Scene.Entities.Skeleton;

namespace Ktisis.Structs.Helpers;

public unsafe struct ReplacementPose {
	public Transform[] Pose;
	public hkaPose* OriginalPose;
	public ushort PartialIndex;
	public int BoneCount;
	public EntityPose Owner;
	
}
