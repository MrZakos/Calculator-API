using IO.Swagger.Models.Math;
using StackExchange.Redis;

namespace IO.Swagger.Services;

public class RedisService(IConnectionMultiplexer redis) {
	private string GetMathKey(MathRequest model) => $"{model.Operation}:{model.X}:{model.Y}";
	
	public Task<bool> IsMathKeyExistsAsync(string key) => redis.GetDatabase().KeyExistsAsync(key);
	public Task SetMathDataAsync(MathRequest data,double result,TimeSpan ttl) => redis.GetDatabase().StringSetAsync(GetMathKey(data),result,ttl);
	public async Task<double?> GetMathDataAsync(MathRequest model) => 
		await redis.GetDatabase().
					StringGetAsync(GetMathKey(model)).
					ContinueWith(x => x.Result.IsNullOrEmpty 
										  ? null 
										  : double.TryParse(x.Result.ToString(),out var result) ? result : default(double?));
}
