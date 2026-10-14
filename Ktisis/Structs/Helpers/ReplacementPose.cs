using Dalamud.Game.ClientState.Objects.Types;

using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.Havok.Animation.Rig;

using Ktisis.Scene.Entities.Game;

namespace Ktisis.Structs.Helpers;

public unsafe struct ReplacementPose {
	public hkaPose* Pose;
	public hkaPose* OriginalPose;
	public ushort PartialIndex;
	public int BoneCount;
	public CharacterBase* Owner;
}
