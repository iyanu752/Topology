using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
namespace TopologyServer;

public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id {get; set;}
    [BsonElement("username")]
    public string UserName {get; set;} = string.Empty;
    public string Password {get; set;} = string.Empty;



}
