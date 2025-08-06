using Confluent.Kafka;
using IO.Swagger.Models.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace IO.Swagger.Services.Kafka;

public class KafkaConsumerService : BackgroundService {
	private readonly IConsumer<string,string> _consumer;
	private readonly ILogger<KafkaConsumerService> _logger;
	private readonly IServiceProvider _serviceProvider;
	private readonly string[] _topics;

	public KafkaConsumerService(IConfiguration configuration,
								ILogger<KafkaConsumerService> logger,
								IServiceProvider serviceProvider) {
		_logger = logger;
		return;
		_serviceProvider = serviceProvider;
		var calculationStartedTopic = configuration.GetValue<string>("Kafka:Topics:CalculationStarted") ?? "calculation-started";
		var calculationCompletedTopic = configuration.GetValue<string>("Kafka:Topics:CalculationCompleted") ?? "calculation-completed";
		_topics = [calculationStartedTopic,calculationCompletedTopic];
		var config = new ConsumerConfig {
			BootstrapServers = configuration.GetConnectionString("kafka") ?? "localhost:9092",
			GroupId = "io-swagger-consumer-group",
			ClientId = "io-swagger-consumer",
			AutoOffsetReset = AutoOffsetReset.Earliest,
			EnableAutoCommit = false,
			EnableAutoOffsetStore = false,
			SessionTimeoutMs = 30000,
			HeartbeatIntervalMs = 10000,
			MaxPollIntervalMs = 300000,
			FetchMinBytes = 1,
		};
		_consumer = new ConsumerBuilder<string,string>(config).SetErrorHandler((_,e) => _logger.LogError("Kafka consumer error: {Error}",e))
															  .SetLogHandler((_,log) => {
																  if (log.Level <= SyslogLevel.Warning)
																	  _logger.LogWarning("Kafka consumer log: {Message}",log.Message);
															  })
															  .SetPartitionsAssignedHandler((c,partitions) => {
																  _logger.LogInformation("Kafka consumer assigned partitions: {Partitions}",string.Join(", ",partitions.Select(p => $"{p.Topic}[{p.Partition}]")));
															  })
															  .SetPartitionsRevokedHandler((c,partitions) => {
																  _logger.LogInformation("Kafka consumer revoked partitions: {Partitions}",string.Join(", ",partitions.Select(p => $"{p.Topic}[{p.Partition}]")));
															  })
															  .Build();
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
		try {
			return;
			_consumer.Subscribe(_topics);
			_logger.LogInformation("Kafka consumer started. Subscribed to topics: {Topics}",string.Join(", ",_topics));
			while (!stoppingToken.IsCancellationRequested) {
				try {
					var consumeResult = _consumer.Consume(stoppingToken);
					if (consumeResult?.Message != null) {
						await ProcessMessageAsync(consumeResult,stoppingToken);
						_consumer.Commit(consumeResult);
						_consumer.StoreOffset(consumeResult);
					}
				}
				catch (ConsumeException ex) {
					_logger.LogError(ex,"Kafka consume error: {Error}",ex.Error.Reason);
				}
				catch (OperationCanceledException) {
					// Expected when cancellation is requested
					break;
				}
				catch (Exception ex) {
					_logger.LogError(ex,"Unexpected error in Kafka consumer");
					// Don't break the loop for unexpected errors, just log and continue
					await Task.Delay(TimeSpan.FromSeconds(5),stoppingToken);
				}
			}
		}
		catch (Exception ex) {
			_logger?.LogCritical(ex,"Fatal error in Kafka consumer service");
		}
		finally {
			try {
				_consumer?.Close();
				_consumer?.Dispose();
				_logger?.LogInformation("Kafka consumer service stopped");
			}
			catch (Exception ex) {
				_logger?.LogError(ex,"Error disposing Kafka consumer");
			}
		}
	}

	private async Task ProcessMessageAsync(ConsumeResult<string,string> result,CancellationToken cancellationToken) {
		return;
		try {
			var eventType = GetEventTypeFromHeaders(result.Message.Headers);
			_logger.LogDebug("Processing Kafka message. Topic: {Topic}, Key: {Key}, EventType: {EventType}",
							 result.Topic,
							 result.Message.Key,
							 eventType);
			switch (eventType) {
				case "CalculationStarted":
					await ProcessCalculationStartedEvent(result.Message.Value,cancellationToken);
					break;
				case "CalculationCompleted":
					await ProcessCalculationCompletedEvent(result.Message.Value,cancellationToken);
					break;
				default:
					_logger.LogWarning("Unknown event type: {EventType} for message key: {Key}",eventType,result.Message.Key);
					break;
			}
			_logger.LogDebug("Successfully processed Kafka message. Topic: {Topic}, Key: {Key}, Offset: {Offset}",
							 result.Topic,
							 result.Message.Key,
							 result.Offset);
		}
		catch (Exception ex) {
			_logger.LogError(ex,
							 "Error processing Kafka message. Topic: {Topic}, Key: {Key}, Offset: {Offset}",
							 result.Topic,
							 result.Message?.Key,
							 result.Offset);
			throw; // Re-throw to prevent commit
		}
	}

	private async Task ProcessCalculationStartedEvent(string messageValue,CancellationToken cancellationToken) {
		return;
		try {
			var calculationEvent = JsonSerializer.Deserialize<CalculationStartedEvent>(messageValue);
			if (calculationEvent == null)
				return;
			using var scope = _serviceProvider.CreateScope();
			var logger = scope.ServiceProvider.GetRequiredService<ILogger<KafkaConsumerService>>();
			logger.LogInformation("Processing calculation started event for operation {OperationId}: {Operation}({X}, {Y})",
								  calculationEvent.OperationId,
								  calculationEvent.Operation,
								  calculationEvent.X,
								  calculationEvent.Y);

			// Here you could add business logic like:
			// - Updating operation status in database
			// - Sending notifications
			// - Triggering other workflows
			// - Metrics collection
			await Task.CompletedTask; // Placeholder for actual processing
		}
		catch (JsonException ex) {
			_logger.LogError(ex,"Failed to deserialize CalculationStartedEvent: {MessageValue}",messageValue);
			throw;
		}
	}

	private async Task ProcessCalculationCompletedEvent(string messageValue,CancellationToken cancellationToken) {
		return;
		try {
			var calculationEvent = JsonSerializer.Deserialize<CalculationCompletedEvent>(messageValue);
			if (calculationEvent == null)
				return;
			using var scope = _serviceProvider.CreateScope();
			var logger = scope.ServiceProvider.GetRequiredService<ILogger<KafkaConsumerService>>();
			logger.LogInformation("Processing calculation completed event for operation {OperationId}: Success={Success}, Result={Result}, ExecutionTime={ExecutionTime}ms, CacheHit={CacheHit}",
								  calculationEvent.OperationId,
								  calculationEvent.Success,
								  calculationEvent.Result,
								  calculationEvent.ExecutionTimeMs,
								  calculationEvent.CacheHit);

			// Here you could add business logic like:
			// - Updating final operation status in database
			// - Sending completion notifications
			// - Updating analytics/metrics
			// - Triggering downstream processes
			// - Audit logging
			await Task.CompletedTask; // Placeholder for actual processing
		}
		catch (JsonException ex) {
			_logger.LogError(ex,"Failed to deserialize CalculationCompletedEvent: {MessageValue}",messageValue);
			throw;
		}
	}

	private static string GetEventTypeFromHeaders(Headers? headers) {
		if (headers == null)
			return "Unknown";
		var eventTypeHeader = headers.FirstOrDefault(h => h.Key == "eventType");
		return eventTypeHeader != null ? System.Text.Encoding.UTF8.GetString(eventTypeHeader.GetValueBytes()) : "Unknown";
	}
}
