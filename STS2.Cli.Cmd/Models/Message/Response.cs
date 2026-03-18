using System.Text.Json.Serialization;

namespace STS2.Cli.Cmd.Models.Message;

/// <summary>
/// Response model from the mod.
/// </summary>
public class Response
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("data")]
    public object? Data { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
