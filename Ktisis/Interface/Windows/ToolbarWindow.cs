using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using GLib.Widgets;

using Ktisis.Editor.Context.Types;
using Ktisis.Interface.Components.Transforms;
using Ktisis.Interface.Components.Workspace;
using Ktisis.Interface.Editor.Types;
using Ktisis.Interface.Types;
using Ktisis.Interface.Windows.Editors;
using Ktisis.Scene.Entities.Game;
using Ktisis.Scene.Entities.Skeleton;
using Ktisis.Scene.Modules;

namespace Ktisis.Interface.Windows;

public class ToolbarWindow : KtisisWindow {
	private readonly IEditorContext _ctx;
	private readonly GuiManager _gui;
	private KtisisWindow? _subWindow;
	private readonly WorkspaceState _workspace;
	private IEditorInterface Interface => this._ctx.Interface;

	public ToolbarWindow(
		IEditorContext ctx,
		GuiManager gui
	) : base("Ktisis Toolbar") {
		this._ctx = ctx;
		this._gui = gui;

		this._workspace = new WorkspaceState(ctx);
	}

	public override void PreOpenCheck() {
		if (this._ctx.IsValid) return;
		Ktisis.Log.Verbose("Context for toolbar window is stale, closing...");
		this.Close();
	}

	public override void Draw() {
		var spacing = ImGui.GetStyle().ItemInnerSpacing.X;

		// WorkspaceState
		this._workspace.Draw();
		ImGui.Spacing();

		// Subwindow Buttons
		if (Buttons.IconButtonTooltip(FontAwesomeIcon.PersonThroughWindow, "Workspace", new Vector2(48, 48)))
			this.SetSubWindow<WorkspaceWindow>();
		ImGui.SameLine(0, spacing * 2);
		if (Buttons.IconButtonTooltip(FontAwesomeIcon.ArrowsSplitUpAndLeft, "Object Editor", new Vector2(48, 48)))
			this.SetSubWindow<ObjectWindow>();
		ImGui.SameLine(0, spacing * 2);
		if (Buttons.IconButtonTooltip(FontAwesomeIcon.PersonChalkboard, "Actor Editor", new Vector2(48, 48)))
			this.SetSubWindow<ActorWindow>();
		ImGui.SameLine(0, spacing * 2);
		if (Buttons.IconButtonTooltip(FontAwesomeIcon.PersonBooth, "Pose View", new Vector2(48, 48)))
			this.SetSubWindow<PosingWindow>();
		ImGui.SameLine(0, spacing * 2);
		if (Buttons.IconButtonTooltip(FontAwesomeIcon.CloudSun, "Environment Editor", new Vector2(48, 48)))
			this.SetSubWindow<EnvWindow>();
		ImGui.SameLine(0, spacing * 2);
		if (Buttons.IconButtonTooltip(FontAwesomeIcon.CameraRetro, "Camera Editor", new Vector2(48, 48)))
			this.SetSubWindow<CameraWindow>();
		ImGui.SameLine(0, spacing * 2);
		if (Buttons.IconButtonTooltip(FontAwesomeIcon.Cogs, "Settings", new Vector2(48, 48)))
			this.SetSubWindow<ConfigWindow>();
		ImGui.SameLine(0, spacing * 2);

		// Subwindow
		if (this._subWindow != null) {
			ImGui.Spacing();
			ImGui.Spacing();
			using var _frame = ImRaii.Child("##SubWindowFrame", ImGui.GetContentRegionAvail(), true);
			this._subWindow.Draw();
		}
	}

	private void SetSubWindow<T>() where T : KtisisWindow {
		if (this._subWindow?.GetType() == typeof(T)) {
			this._subWindow.OnClose();
			this._subWindow = null; // unset subwindow if same button clicked
			return;
		}

		if (typeof(T) == typeof(EnvWindow)) {
			var module = this._ctx.Scene.GetModule<EnvModule>();
			this._subWindow = this._gui.GetOrCreate<EnvWindow>(this._ctx, module);
		} else if (typeof(T) == typeof(ObjectWindow)) {
			this._subWindow = this.Interface.GetObjectWindow();
		} else if (typeof(T) == typeof(ConfigWindow)) {
			this._subWindow = this._gui.GetOrCreate<ConfigWindow>();
		}
		this._subWindow = this._gui.GetOrCreate<T>(this._ctx);

		// handle window followup actions
		if (this._subWindow is ActorWindow win) {
			var target = this._ctx.Selection.GetFirstSelected();
			if (
				target switch {
					BoneNode node => node.Pose.Parent,
					BoneNodeGroup group => group.Pose.Parent,
					EntityPose pose => pose.Parent,
					_ => target
				} is ActorEntity actor
			) win.SetTarget(actor);
			else
				win.SetTarget(this._ctx.Scene.GetFirstActor());
		}

		this._subWindow.OnOpen();
	}
}
