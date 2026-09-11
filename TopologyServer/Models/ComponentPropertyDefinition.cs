using System;

namespace TopologyServer;

public class ComponentPropertyDefinition
{
    public string Key {get; set;} = string.Empty;
    public string Label {get; set;} = string.Empty;
    public string? InputType {get; set;}
    public object? DefaultValue {get; set;}
    public List<String> Options {get; set;} = [];
    public double? Min {get; set;}
    public double? Max {get; set;}
    public bool Required {get; set;}

}
