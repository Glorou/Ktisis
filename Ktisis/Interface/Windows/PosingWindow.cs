using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Bindings.ImGuizmo;
using Dalamud.Interface.Windowing;

using GLib.Widgets;

using Ktisis.Common.Extensions;
using Ktisis.Common.Utility;
using Ktisis.Data.Config.Pose2D;
using Ktisis.Data.Serialization;
using Ktisis.Editor.Camera.Types;
using Ktisis.Editor.Context.Types;
using Ktisis.Editor.Transforms.Types;
using Ktisis.Interface.Components.Posing;
using Ktisis.Interface.Components.Posing.Types;
using Ktisis.Interface.Components.Transforms;
using Ktisis.Interface.Types;
using Ktisis.Localization;
using Ktisis.Scene.Entities.Game;
using Ktisis.Services.Game;

namespace Ktisis.Interface.Windows;

public class PosingWindow : KtisisWindow {
	private readonly IEditorContext _ctx;
	private readonly LocaleManager _locale;
	private readonly GPoseService _gpose;
	private readonly PoseViewRenderer _render;
	private readonly Gizmo2D _gizmo;
	private readonly TransformTable _table;


	private PoseViewSchema? _schema;
	private ViewEnum _view = ViewEnum.Body;

	internal ActorEntity? _target;
	private ITransformMemento? Transform;
	private bool FlyoutOpen;

	private enum ViewEnum {
		Body,
		Face
	}
	
	public PosingWindow(
		IEditorContext ctx,
		ITextureProvider tex,
		LocaleManager locale,
		GPoseService gpose,
		TransformTable table,
		Gizmo2D gizmo
	) : base(
		"Pose View###KtisisPoseView"
	) {
		this._ctx = ctx;
		this._locale = locale;
		this._gpose = gpose;
		this._render = new PoseViewRenderer(ctx.Config, tex);
		this._table = table;
		this._gizmo = gizmo;
		this.FlyoutOpen = false;
	}

	public override void OnOpen() {
		this._schema = SchemaReader.ReadPoseView();
	}
	
	public override void PreOpenCheck() {
		if (this._ctx.IsValid) return;
		Ktisis.Log.Verbose("Context for posing window is stale, closing...");
		this.Close();
	}

	public override void PreDraw() {
		this.SizeConstraints = new WindowSizeConstraints {
			MinimumSize = new Vector2(500, 350)
		};
	}

	public override void Draw() {
		var target = this._ctx.Transform.Target;
		if (this._ctx.Config.Editor.UseLegacyPoseViewTabs && !this._ctx.Config.Editor.UseToolbar) {
			this.DrawLegacyTabs();
			return;
		}

		if (this._ctx.Config.Editor.UseLegacyWindowBehavior) {
			this.DrawLegacyTarget();
			return;
		}

		if (this.UpdateTarget())
			this.WindowName = $"Pose View - {this._target!.Name}###KtisisPoseView";

		if (this._target is not { IsValid: true }) {
			ImGui.Text("Select an actor to start editing its pose.");
			return;
		}

		this.DrawWindow(this._target);

		
		if (this._ctx.Config.Editor.UseToolbar) {
			if (this.FlyoutOpen) {
				ImGui.SameLine();
				using var _ = ImRaii.Group();
				this.DrawTransform(target);
				ImGui.SetCursorPos((ImGui.GetContentRegionMax().Sub(Buttons.CalcSize()) - ImGui.GetStyle().WindowPadding).SubX(TransformTable.CalcWidth() + ImGui.GetStyle().WindowPadding.X * 2));
				if (ImGui.Button("<")) {
					ImGui.SetWindowSize(ImGui.GetWindowSize().SubX(TransformTable.CalcWidth() + ImGui.GetStyle().WindowPadding.X * 2));
					this.FlyoutOpen = false;
				}
			} else {
				ImGui.SetCursorPos(ImGui.GetContentRegionMax().Sub(Buttons.CalcSize()) - ImGui.GetStyle().WindowPadding );
				if (ImGui.Button(">")) {
					ImGui.SetWindowSize(ImGui.GetWindowSize().AddX(TransformTable.CalcWidth() + ImGui.GetStyle().WindowPadding.X * 2));
					this.FlyoutOpen = true;
				}
			}
		}
	}

	private bool UpdateTarget() {
		var selected = (ActorEntity?)this._ctx.Selection.GetSelected()
			.FirstOrDefault(entity => entity is ActorEntity);

		if (selected == null || this._target == selected)
			return false;

		this._target = selected;
		return true;
	}

	private IEnumerable<ActorEntity> GetValidTargets() {
		return this._ctx.Scene.Children
			.Where(entity => entity is ActorEntity)
			.Cast<ActorEntity>();
	}

	private void DrawLegacyTabs() {
		using var _ = ImRaii.TabBar("##pose_tabs");
		
		var actors = this.GetValidTargets();
			
		foreach (var actor in actors) {
			using var tab = ImRaii.TabItem(actor.Name);
			if (!tab.Success) continue;
			
			ImGui.Spacing();
			
			this.DrawWindow(actor);
		}
	}
	
	private void DrawLegacyTarget() {
		var tarIndex = this._gpose.GPoseTarget?.ObjectIndex;
		if ((this._target == null || this._target.Actor.ObjectIndex != tarIndex) && tarIndex != null) {
			var actors = this.GetValidTargets();
			
			var targeted = actors.FirstOrDefault(actor => {
				return actor.Actor.ObjectIndex == tarIndex;
			});

			if (targeted != null)
				this._target = targeted;
		}

		if (this._target is not { IsValid: true }) {
			Ktisis.Log.Info("Targeted actor has no skeleton or is invalid.");
			return;
		}
		
		this.DrawWindow(this._target);
	}

	private unsafe void DrawWindow(ActorEntity target) {
		var avail = ImGui.GetContentRegionAvail();
		if (this._ctx.Config.Editor.UseToolbar && this.FlyoutOpen)
			avail = avail.SubX(TransformTable.CalcWidth() + ImGui.GetStyle().WindowPadding.X * 2);

		var width = avail.X * 0.90f;
		var spacing = ImGui.GetStyle().ItemSpacing.X * 2;
		
		var viewRegion = avail with { X = width - spacing };
		this.DrawView(target, viewRegion);
		ImGui.SameLine();
		if (!this._ctx.Config.Editor.UseToolbar || !this.FlyoutOpen)
			ImGui.SetCursorPosX(width);
		this.DrawSideMenu(target);
	}
	
	// Side

	private void DrawSideMenu(ActorEntity target) {
		using var _ = ImRaii.Group();
		
		this.DrawViewSelect();
		for (var i = 0; i < 3; i++) ImGui.Spacing();
		this.DrawImportExport(target);
	}

	private void DrawViewSelect() {
		using var _ = ImRaii.Group();

		ImGui.Text("View:");
		
		foreach (var value in Enum.GetValues<ViewEnum>()) {
			if (ImGui.RadioButton(value.ToString(), this._view == value))
				this._view = value;
		}
	}

	private void DrawImportExport(ActorEntity target) {
		if (target.Pose == null) return;

		if (ImGui.Button("Import"))
			this._ctx.Interface.OpenPoseImport(target);

		if (ImGui.Button("Export"))
			this._ctx.Interface.OpenPoseExport(target.Pose);
	}
	
	// View rendering
	
	private void DrawView(ActorEntity target, Vector2 region) {
		using var _ = ImRaii.Child("##viewFrame", region, false, ImGuiWindowFlags.NoScrollbar);

		var frame = this._render.StartFrame();
		
		switch (this._view) {
			case ViewEnum.Body:
				this.DrawView(frame, "Body", 0.35f);
				ImGui.SameLine();
				this.DrawView(frame, "Armor", 0.35f);
				ImGui.SameLine();
				using (ImRaii.Group()) {
					this.DrawView(frame, "Hands", 0.30f, 0.60f);
					
					ImGui.Spacing();
					
					var hasTail = target.Pose?.HasTail() ?? false;
					var isBunny = target.Pose?.HasBunnyEars() ?? false;
					
					var template = this._render.BuildTemplate(target);
					
					var width = (hasTail, isBunny) switch {
						(true, true) => 0.15f,
						(true, false) or (false, true) => 0.30f,
						_ => 0.00f
					};

					if (hasTail) {
						this.DrawView(frame, "Tail", width, 0.40f);
						if (isBunny) ImGui.SameLine();
					}
					
					if (isBunny) this.DrawView(frame, "Ears", width, 0.40f, template);
				}
				break;
			default:

					
				this.DrawView(frame, "Face", 0.65f );
				ImGui.SameLine();
				using (ImRaii.Group()) {
					this.DrawView(frame, "Lips", 0.35f, 0.50f);
					this.DrawView(frame, "Mouth", 0.35f, 0.50f);
				}
				break;
		}
		
		if (target.Pose != null)
			frame.DrawBones(target.Pose);
	}

	private void DrawView(
		IViewFrame frame,
		string name,
		float width = 1.0f,
		float height = 1.0f,
		IDictionary<string, string>? template = null
	) {
		if (this._schema == null) return;

		if (!this._schema.Views.TryGetValue(name, out var view))
			return;

		frame.DrawView(view, width, height, template);
	}
	
	//Gizmo

	private void DrawTransform(ITransformTarget? target) {
		var transform = target?.GetTransform() ?? new Transform();

		var disabled = target == null;
		using var _ = ImRaii.Disabled(disabled);

		var moved = this.DrawTransform(ref transform, out var isEnded, disabled);
		if (target != null && moved) {
			this.Transform ??= this._ctx.Transform.Begin(target);
			this.Transform.SetTransform(transform);
		}
		
		if (!isEnded) return;
		this.Transform?.Dispatch();
		this.Transform = null;
	}
	
	private bool DrawTransform(ref Transform transform, out bool isEnded, bool disabled) {
		isEnded = false;
		
		var gizmo = false;
		if (!this._ctx.Config.Editor.TransformHide) {
			gizmo = this.DrawGizmo(ref transform, ImGui.GetContentRegionAvail().X - (this._ctx.Config.Editor.UseToolbar? 0.1f: 0), disabled);
			isEnded = this._gizmo.IsEnded;
		}

		var table = this._table.Draw(
			transform,
			out var result,
			TransformTableFlags.Default | TransformTableFlags.UseAvailable | TransformTableFlags.Operation
		);
		if (table) transform = result;
		isEnded |= this._table.IsDeactivated;

		return gizmo || table;
	}
	private unsafe bool DrawGizmo(ref Transform transform, float width, bool disabled) {
		var size = new Vector2(width, 300);

		this._gizmo.Begin(size, "pose");
		this._gizmo.Mode = this._ctx.Config.Gizmo.Mode;
		this._gizmo.Operation = this._ctx.Config.Gizmo.Operation.HasFlag(ImGuizmoOperation.RotateX) && !this._ctx.Config.Gizmo.Operation.HasFlag(ImGuizmoOperation.RotateScreen)
			? ImGuizmoOperation.RotateX | ImGuizmoOperation.RotateY | ImGuizmoOperation.RotateZ
			: ImGuizmoOperation.Rotate;
		
		if (disabled) {
			this._gizmo.End();
			return false;
		}

		var cameraFov = 1.0f;
		var cameraPos = Vector3.Zero;
		if (this._ctx.Cameras.IsWorkCameraActive) {
			var freeCam = (WorkCamera)this._ctx.Cameras.Current;
			cameraFov = freeCam.Camera->RenderEx->FoV;
			cameraPos = freeCam.Position;
		} else {
			var camera = CameraService.GetGameCamera();
			if (camera != null) {
				cameraFov = camera->FoV;
				cameraPos = camera->CameraBase.SceneCamera.Object.Position;
			}
		}
		
		var matrix = transform.ComposeMatrix();
		this._gizmo.SetLookAt(cameraPos, transform.Position, cameraFov, (size.X - ImGui.GetStyle().WindowPadding.X * 2) / (size.Y - ImGui.GetStyle().WindowPadding.Y * 2));
		var result = this._gizmo.Manipulate(ref matrix, out _);
		
		this._gizmo.End();

		if (result)
			transform.DecomposeMatrixPrecise(matrix, transform);

		return result;
	}
}
