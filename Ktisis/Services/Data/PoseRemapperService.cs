using System;

using FFXIVClientStructs;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

using Ktisis.Core.Attributes;
using Ktisis.Data.Files;
using Ktisis.Structs.Common;

namespace Ktisis.Services.Data;

[Singleton]
public unsafe class PoseRemapperService {

	private readonly PbdFile pbdFile;
	public PoseRemapperService() {
		var resourceHandle = CharacterUtility.Instance()->HumanBoneDeformerResourceHandle;
		this.pbdFile = new PbdFile(new ReadOnlySpan<byte>(((DataHandle*)resourceHandle->Data)->FileHandle, (int)resourceHandle->Length));
	}

	public void Convert(ushort from, ushort to, PoseFile pose) {
		var deformers = this.pbdFile.GetDeformers(from, to);
		foreach (var deformer in deformers) {
			
			
		}
	}
	
	
}
