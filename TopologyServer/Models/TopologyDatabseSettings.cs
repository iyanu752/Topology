using System;

namespace TopologyServer;

public class ToplogyDatabseSettings
{
    public string ConnectionString {get; set;} = null!;
    public string DatabaseName {get; set;} = null!;
    public string ToplogyCollectionName {get; set; } = null!;

}
