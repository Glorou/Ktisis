using System.Diagnostics.CodeAnalysis;

using Ktisis.Data.Expressions;
using Ktisis.Scene.Entities.Game;
using Ktisis.Scene.Entities.Skeleton;
using Ktisis.Scene.Types;

namespace Ktisis.Editor.Expressions.Types;

public interface IExpressionManager {
	public void Initialize();

	public IExpressionController CreateController(ISceneManager scene);
	public bool RemoveController(IExpressionController controller);

	public void ResetBlendStates();

	public bool TryGetSchemaFile(ushort raceSexId, [NotNullWhen(true)] out ExpressionsSchemaFile? entry);
}
