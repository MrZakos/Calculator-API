using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IO.Swagger.Tests.Integration;

/// <summary>
/// Integration tests for Mockoon service endpoints
/// </summary>
public class MockoonIntegrationTests
{
    [Fact]
    public async Task MockoonService_CallMetaAddEndpoint_ReturnsExpectedResult()
    {
        // Arrange
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IO_Swagger_AppHost>();
        builder.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
        });

        await using var app = await builder.BuildAsync();
        await app.StartAsync();

        // Act
        var httpClient = app.CreateHttpClient("mockoon");
        var response = await httpClient.GetAsync("/api/meta/add/5/3");

        // Assert
        Assert.True(response.IsSuccessStatusCode, "Mockoon endpoint should respond successfully");
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
        Assert.NotEmpty(content);
    }
}
