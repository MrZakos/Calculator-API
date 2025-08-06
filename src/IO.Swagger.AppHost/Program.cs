var builder = DistributedApplication.CreateBuilder(args);

// Add Redis with RedisInsight
var redis = builder.AddRedis("redis")
                   .WithRedisInsight();

// Add Kafka with UI
var kafka = builder.AddKafka("kafka")
                   .WithKafkaUI();

// Add Mockoon CLI as Docker container
var mockoon = builder.AddContainer("mockoon", "mockoon/cli")
                     .WithHttpEndpoint(targetPort: 3000, name: "mockoon")
                     .WithBindMount("../../mockoon", "/data")
                     .WithArgs("--data", "/data/CalculatorMockoon.json", "--port", "3000")
					 .WithHttpEndpoint(port: 3000, targetPort: 3000);

// Add the API project with references to the services
var api = builder.AddProject<Projects.IO_Swagger>("io-swagger")
                 .WithExternalHttpEndpoints()
                 .WithReference(redis)
                 .WithReference(kafka);

builder.Build().Run();
