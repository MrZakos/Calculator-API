using System.Text.Json.Serialization;

namespace IO.Swagger.Models.Kafka;

public class CalculationStartedEvent
{
    [JsonPropertyName("operationId")]
    public string OperationId { get; set; } = string.Empty;
    
    [JsonPropertyName("operation")]
    public string Operation { get; set; } = string.Empty;
    
    [JsonPropertyName("x")]
    public double X { get; set; }
    
    [JsonPropertyName("y")]
    public double Y { get; set; }
    
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    [JsonPropertyName("userId")]
    public string? UserId { get; set; }
}

public class CalculationCompletedEvent
{
    [JsonPropertyName("operationId")]
    public string OperationId { get; set; } = string.Empty;
    
    [JsonPropertyName("operation")]
    public string Operation { get; set; } = string.Empty;
    
    [JsonPropertyName("x")]
    public double X { get; set; }
    
    [JsonPropertyName("y")]
    public double Y { get; set; }
    
    [JsonPropertyName("result")]
    public double? Result { get; set; }
    
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("error")]
    public string? Error { get; set; }
    
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    [JsonPropertyName("executionTimeMs")]
    public long ExecutionTimeMs { get; set; }
    
    [JsonPropertyName("cacheHit")]
    public bool CacheHit { get; set; }
    
    [JsonPropertyName("userId")]
    public string? UserId { get; set; }
}
