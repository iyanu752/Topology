using Microsoft.AspNetCore.Mvc;

namespace TopologyServer;

public sealed class SimulationPreviewDto
{
    public required SaveDesignDto Design { get; set; }
    public required SimulationConfiguration Configuration { get; set; }
}

[ApiController]
[Route("api/simulations/preview")]
public sealed class SimulationPreviewController : ControllerBase
{
    private static readonly SemaphoreSlim Slots = new(2);

    [HttpPost]
    [RequestSizeLimit(1_000_000)]
    public async Task<IActionResult> Run(SimulationPreviewDto request, CancellationToken cancellationToken)
    {
        if (request.Design == null || request.Configuration == null) return BadRequest(new { message = "Design and configuration are required." });
        if (!await Slots.WaitAsync(0, cancellationToken)) return StatusCode(429, new { message = "Simulations are busy. Try again shortly." });
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            return Ok(new SimulationRunner().Run(new Design { Nodes = request.Design.Nodes, Edges = request.Design.Edges }, request.Configuration, timeout.Token));
        }
        catch (SimulationCompilationException exception)
        {
            return BadRequest(new { message = exception.Message, issues = exception.Issues });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        finally { Slots.Release(); }
    }
}
