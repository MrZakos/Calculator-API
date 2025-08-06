using Confluent.Kafka;
using IO.Swagger.Models.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace IO.Swagger.Services.Kafka;

public interface IKafkaProducerService {
	Task SendCalculationStartedAsync(CalculationStartedEvent calculationEvent);
	Task SendCalculationCompletedAsync(CalculationCompletedEvent calculationEvent);
}

public class KafkaProducerService : IKafkaProducerService,IDisposable {
	private readonly IProducer<string,string> _producer;
	private readonly ILogger<KafkaProducerService> _logger;
	private readonly string _calculationStartedTopic;
	private readonly string _calculationCompletedTopic;

	public KafkaProducerService(IConfiguration configuration,ILogger<KafkaProducerService> logger) {
		_logger = logger;
		_calculationStartedTopic = configuration.GetValue<string>("Kafka:Topics:CalculationStarted") ?? "calculation-started";
		_calculationCompletedTopic = configuration.GetValue<string>("Kafka:Topics:CalculationCompleted") ?? "calculation-completed";
		var config = new ProducerConfig {
			BootstrapServers = configuration.GetConnectionString("kafka") ?? "localhost:9092",
			ClientId = "io-swagger-producer",
			Acks = Acks.All, // Required when EnableIdempotence is true
			EnableIdempotence = true,
			MessageTimeoutMs = 30000,
			RequestTimeoutMs = 30000,
			DeliveryReportFields = "all",
			CompressionType = CompressionType.Snappy
		};
		_producer = new ProducerBuilder<string,string>(config).SetErrorHandler((_,e) => _logger.LogError("Kafka producer error: {Error}",e))
															  .SetLogHandler((_,log) => {
																  if (log.Level <= SyslogLevel.Warning)
																	  _logger.LogWarning("Kafka log: {Message}",log.Message);
															  })
															  .Build();
	}

	public async Task SendCalculationStartedAsync(CalculationStartedEvent calculationEvent) {
		try {
			var json = JsonSerializer.Serialize(calculationEvent);
			var message = new Message<string,string> {
				Key = calculationEvent.OperationId,
				Value = json,
				Headers = new Headers {
					{ "eventType",System.Text.Encoding.UTF8.GetBytes("CalculationStarted") },
					{ "timestamp",System.Text.Encoding.UTF8.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()) }
				}
			};
			var result = await _producer.ProduceAsync(_calculationStartedTopic,message);
			_logger.LogInformation("Calculation started event sent to Kafka. Topic: {Topic}, Partition: {Partition}, Offset: {Offset}, OperationId: {OperationId}",
								   result.Topic,
								   result.Partition.Value,
								   result.Offset.Value,
								   calculationEvent.OperationId);
		}
		catch (Exception ex) {
			_logger.LogError(ex,"Failed to send calculation started event to Kafka for operation {OperationId}",calculationEvent.OperationId);
			throw;
		}
	}

	public async Task SendCalculationCompletedAsync(CalculationCompletedEvent calculationEvent) {
		try {
			var json = JsonSerializer.Serialize(calculationEvent);
			var message = new Message<string,string> {
				Key = calculationEvent.OperationId,
				Value = json,
				Headers = new Headers {
					{ "eventType",System.Text.Encoding.UTF8.GetBytes("CalculationCompleted") },
					{ "timestamp",System.Text.Encoding.UTF8.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()) }
				}
			};
			var result = await _producer.ProduceAsync(_calculationCompletedTopic,message);
			_logger.LogInformation("Calculation completed event sent to Kafka. Topic: {Topic}, Partition: {Partition}, Offset: {Offset}, OperationId: {OperationId}, Success: {Success}",
								   result.Topic,
								   result.Partition.Value,
								   result.Offset.Value,
								   calculationEvent.OperationId,
								   calculationEvent.Success);
		}
		catch (Exception ex) {
			_logger.LogError(ex,"Failed to send calculation completed event to Kafka for operation {OperationId}",calculationEvent.OperationId);
			throw;
		}
	}

	public void Dispose() {
		_producer?.Flush();
		_producer?.Dispose();
	}
}
