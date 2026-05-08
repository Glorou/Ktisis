using System;

using Ktisis.Interop;
using Ktisis.Structs.Havok;

namespace Ktisis.Editor.Posing.Ik.LookAt;

public class LookAtSolver(IkModule module) : IDisposable  {
	private readonly Alloc<LookAtIkSetup> AllocIkSetup = new(16);
	private readonly Alloc<LookAtIkRange> AllocIkRange = new(8);

	public unsafe LookAtIkSetup* LookAtSetup => this.AllocIkSetup.Data;
	public unsafe LookAtIkRange* LookAtRange => this.AllocIkRange.Data;

	public unsafe void Setup() {
		if (this.AllocIkSetup.Address == nint.Zero)
			throw new Exception("Allocation for IkSetup failed.");
		if (this.AllocIkRange.Address == nint.Zero)
			throw new Exception("Allocation for IkRange failed.");

		*this.LookAtSetup = new LookAtIkSetup() { };

	}
	
	public bool IsDisposed { get; private set; }
	
	public void Dispose() {
		this.IsDisposed = true;
		this.AllocIkSetup.Dispose();
		this.AllocIkRange.Dispose();
		GC.SuppressFinalize(this);
	}
}
