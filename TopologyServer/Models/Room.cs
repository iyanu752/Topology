using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
namespace TopologyServer;

public class Room
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id {get; set;}
    public string OwnerUserId {get; set;} = string.Empty;
    public List<string> MemberUserIds {get; set;} = [];
    public AccessType Type{get; set;}
    public int? RoomKey {get; set;}
    public DateTime CreatedAt {get; set;} = DateTime.UtcNow;

}
