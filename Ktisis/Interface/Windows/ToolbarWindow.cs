using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

using FFXIVClientStructs;

using GLib.Widgets;

using Ktisis.Editor.Context.Types;
using Ktisis.Interface.Components.Transforms;
using Ktisis.Interface.Components.Workspace;
using Ktisis.Interface.Editor.Types;
using Ktisis.Interface.Types;
using Ktisis.Interface.Windows.Editors;
using Ktisis.Interface.Windows.ToolbarModules;
using Ktisis.Scene.Entities.Game;
using Ktisis.Scene.Entities.Skeleton;
using Ktisis.Scene.Modules;

namespace Ktisis.Interface.Windows;
internal record WindowButtons(DrawContentDelegate Window, FontAwesomeIcon Icon, string TooltipText);
internal delegate void DrawContentDelegate();

public class ToolbarWindow : KtisisWindow {
	private readonly IEditorContext _ctx;
	private readonly GuiManager _gui;
	private KtisisWindow? _subWindow;
	private readonly WorkspaceState _workspace;
	private IEditorInterface Interface => this._ctx.Interface;

	private List<WindowButtons> _buttons; 
	public ToolbarWindow(
		IEditorContext ctx,
		GuiManager gui
	) : base("Ktisis Toolbar") {
		this._ctx = ctx;
		this._gui = gui;
		this.Flags = ImGuiWindowFlags.AlwaysAutoResize | this.Flags;
		this._workspace = new WorkspaceState(ctx);
		this._buttons =  new() {
			new(this.DrawWorkspaceWindow, FontAwesomeIcon.PersonThroughWindow, "Workspace"),
			new(this.DrawObjectWindow, FontAwesomeIcon.ArrowsSplitUpAndLeft, "Object Editor"),
			new(this.DrawActorWindow, FontAwesomeIcon.PersonChalkboard, "Actor Editor"),
			new(this.DrawPosingWindow, FontAwesomeIcon.PersonBooth, "Pose View"),
			new(this.DrawEnvWindow, FontAwesomeIcon.CloudSun, "Environment Editor"),
			new(this.DrawCameraWindow, FontAwesomeIcon.CameraRetro, "Camera Editor"),
			new(this.DrawConfigWindow, FontAwesomeIcon.Cogs, "Settings"),
		};
		this.SizeConstraints = new WindowSizeConstraints(){MaximumSize = new Vector2(-1, float.MaxValue),  MinimumSize = new Vector2(-1, 0)};
		
	}

	public override void PreDraw() {
		base.PreDraw();
		this.Size = Vector2.Zero;
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
		foreach (var button in _buttons) {
			if (Buttons.IconButtonTooltip(button.Icon, button.TooltipText, new Vector2(48, 48)))
				button.Window();
			ImGui.SameLine(0, spacing * 2);
		}
		/*
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
		ImGui.SameLine(0, spacing * 2);*/

		// Subwindow
		if (this._subWindow != null) {
			ImGui.Spacing();
			ImGui.Spacing();
			this.Size += new Vector2(0, 300);
			using var _frame = ImRaii.Group();
			this._subWindow.Draw();
		} 
	}

	private void DrawWorkspaceWindow() => this.SetSubWindow<Workspace>();
	private void DrawObjectWindow() => this.SetSubWindow<ObjectWindow>();
	private void DrawActorWindow() => this.SetSubWindow<ActorWindow>();
	private void DrawPosingWindow() => this.SetSubWindow<PosingWindow>();
	private void DrawEnvWindow() => this.SetSubWindow<Env>();
	private void DrawCameraWindow() => this.SetSubWindow<Camera>();
	private void DrawConfigWindow() => this.SetSubWindow<ConfigWindow>();
	
	private void SetSubWindow<T>() where T : KtisisWindow {
		if (this._subWindow?.GetType() == typeof(T)) {
			this._subWindow.OnClose();
			this._subWindow = null; // unset subwindow if same button clicked
			return;
		}

		if (typeof(T) == typeof(Env)) {
			var module = this._ctx.Scene.GetModule<EnvModule>();
			this._subWindow = this._gui.GetOrCreate<Env>(this._ctx.Scene, module);
		} else if (typeof(T) == typeof(ObjectWindow)) {
			this._subWindow = this.Interface.GetObjectWindow();
		} else if (typeof(T) == typeof(ConfigWindow)) {
			this._subWindow = this._gui.GetOrCreate<ConfigWindow>();
		} else {
			this._subWindow = this._gui.GetOrCreate<T>(this._ctx);
		}

		
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
