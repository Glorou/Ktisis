using FFXIVClientStructs.Havok.Animation.Rig;

using Ktisis.Scene.Entities.Game;

namespace Ktisis.Structs.Helpers;

public unsafe struct ReplacementPose {
	public hkaPose* Pose;
	public hkaPose* OriginalPose;
	public ushort PartitionIndex;
	public uint BoneCount;
	public ActorEntity Owner;
}
