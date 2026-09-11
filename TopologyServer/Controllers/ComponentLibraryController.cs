using Microsoft.AspNetCore.Mvc;

namespace TopologyServer;

[ApiController]
[Route("api/components")]
public class ComponentLibraryController : ControllerBase
{
    private readonly IComponentLibraryService _componentLibraryService;
    private readonly ILogger<ComponentLibraryController> _logger;

    public ComponentLibraryController(
        IComponentLibraryService componentLibraryService,
        ILogger<ComponentLibraryController> logger)
    {
        _componentLibraryService = componentLibraryService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetComponents()
    {
        var components = await _componentLibraryService.GetComponentsAsync();
        return Ok(components);
    }

    [HttpGet("{type}")]
    public async Task<IActionResult> GetComponent(ComponentType type)
    {
        var component = await _componentLibraryService.GetComponentByTypeAsync(type);

        if (component is null)
        {
            _logger.LogWarning("Component type {ComponentType} was not found", type);
            return NotFound("Component not found");
        }

        return Ok(component);
    }

    [HttpPost("seed")]
    public async Task<IActionResult> SeedComponents()
    {
        await _componentLibraryService.SeedDefaultComponentsAsync();
        return NoContent();
    }
}