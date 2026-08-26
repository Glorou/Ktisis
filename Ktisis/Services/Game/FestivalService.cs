using Dalamud.Plugin.Services;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;

using Ktisis.Core.Attributes;

namespace Ktisis.Services.Game;

[Singleton]
public unsafe class FestivalService {
	private readonly IFramework _framework;
	public FestivalService(
		IFramework framework
	) {
		this._framework = framework;
		this.BaseFestivals = GameMain.Instance()->ActiveFestivals.ToArray();
		this.ActiveFestivals = this.BaseFestivals;
	}
	
	private GameMain.Festival[] BaseFestivals;
	private GameMain.Festival[] ActiveFestivals;

	public void SetFestivals(GameMain.Festival festival) {
		this.ActiveFestivals[0] = festival;
		var instance = LayoutWorld.Instance();
		instance->ActiveLayout->FestivalStatus = 5;
		instance->ActiveLayout->FestivalLayersAddTimer = 20;

		fixed (GameMain.Festival* fst = this.ActiveFestivals)
			instance->ActiveLayout->SetActiveFestivals(fst);

		this._framework.DelayTicks(25);
		var layer = instance->ActiveLayout->FestivalLayersToAdd.First->Value;
		var e = layer->Instances.GetEnumerator();
		foreach (var i in e) {
			if (!i.Item2.IsNull) {
				i.Item2.Value->SetColliderActive(false);
				i.Item2.Value->UpdateCollider();
			}
		}
		e.Dispose();
	}
}