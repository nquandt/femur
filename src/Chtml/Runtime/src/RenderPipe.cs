

using System.Threading.Tasks;

namespace Femur.Chtml.Runtime;


/// <summary>
/// Delegate that writes to a PipeWriter directly for low-allocation rendering.
/// </summary>
public delegate ValueTask RenderPipe<TGlobalProps>(RenderContext<TGlobalProps> renderContext)
    where TGlobalProps : class;
