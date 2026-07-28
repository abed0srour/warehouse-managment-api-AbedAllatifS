using System.Net;
using FluentAssertions;

namespace Warehouse.Api.IntegrationTests;

public class SwaggerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SwaggerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GET_SwaggerJson_ReturnsValidDocument_200()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrWhiteSpace();
        body.Should().Contain("\"openapi\"");
        body.Should().Contain("\"paths\"");
    }
}
