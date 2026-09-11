using System;

namespace TopologyServer;

public class ComponentDefinition
{
    public string? Id {get; set;}
    public ComponentType Type {get; set;} 
    public string? Label {get; set;}
    public ComponentCategory Category {get; set;}
    public string Description { get; set; } = string.Empty;
    public List<ComponentPropertyDefinition> Properties {get; set;} = [];
     
}
