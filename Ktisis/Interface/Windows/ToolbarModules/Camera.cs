using Ktisis.Editor.Context.Types;
using Ktisis.Interface.Components.Transforms;

namespace Ktisis.Interface.Windows.ToolbarModules;

public class Camera : CameraWindow {

	public Camera(IEditorContext ctx, TransformTable fixedPos, TransformTable relativePos) : base(ctx, fixedPos, relativePos)
	{
	}
	
	private new const TransformTableFlags TransformFlags = TransformTableFlags.Default & ~TransformTableFlags.Operation;
}
