using System.Security.Claims;

namespace TopologyServer;

public interface IDesignService
{
    Task<Design?> GetDesignAsync(ClaimsPrincipal principal, string roomId);
    Task<Design> SaveDesignAsync(ClaimsPrincipal principal, string roomId, SaveDesignDto saveDesignDto);
    Task<Design> CreateInitialDesignAsync(string roomId, string userId);
    Task<bool> DeleteDesignAsync(ClaimsPrincipal principal, string roomId);
}