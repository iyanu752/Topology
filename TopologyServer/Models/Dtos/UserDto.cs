using System;

namespace TopologyServer;

public class UserDto
{
    public string? Id {get; set;}
    public string Auth0UserId {get; set;} = string.Empty;
    public string? UserName {get; set;}
    public string? Email {get; set;}
    public DateTime CreatedAt {get; set;}
}
