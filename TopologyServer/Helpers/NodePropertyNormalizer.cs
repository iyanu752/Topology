using System.Text.Json;

namespace TopologyServer;

public static class NodePropertyNormalizer
{
    public static List<Node> NormalizeNodes(IEnumerable<Node> nodes)
    {
        return nodes.Select(node => new Node
        {
            Id = string.IsNullOrWhiteSpace(node.Id) ? Guid.NewGuid().ToString() : node.Id,
            Type = node.Type,
            Label = node.Label,
            X = node.X,
            Y = node.Y,
            Properties = NormalizeProperties(node.Properties)
        }).ToList();
    }

    public static Dictionary<string, object> NormalizeProperties(Dictionary<string, object> properties)
    {
        return properties.ToDictionary(
            pair => pair.Key,
            pair => NormalizeValue(pair.Value));
    }

    private static object NormalizeValue(object? value)
    {
        if (value is null)
        {
            return null!;
        }

        if (value is JsonElement jsonElement)
        {
            return NormalizeJsonElement(jsonElement) ?? null!;
        }

        return value;
    }

    private static object? NormalizeJsonElement(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number when value.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when value.TryGetDouble(out var doubleValue) => doubleValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            JsonValueKind.Array => value.EnumerateArray().Select(NormalizeJsonElement).ToList(),
            JsonValueKind.Object => value.EnumerateObject().ToDictionary(property => property.Name, property => NormalizeJsonElement(property.Value)),
            _ => value.ToString()
        };
    }
}