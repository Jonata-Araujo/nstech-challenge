using System.Net;
using System.Net.Http.Json;
using OrderService.Api.Contracts;
using OrderService.Application.Auth;

namespace OrderService.Api.IntegrationTests;

public class AuthTests(OrderServiceApiFactory factory) : IClassFixture<OrderServiceApiFactory>
{
    [Fact]
    public async Task Issues_token_for_valid_credentials()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/auth/token", Users.Admin);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = await response.ReadAsync<TokenDto>();
        token.AccessToken.Should().NotBeNullOrWhiteSpace();
        token.TokenType.Should().Be("Bearer");
        token.ExpiresIn.Should().BePositive();
    }

    [Theory]
    [InlineData("admin", "wrong")]
    [InlineData("nobody", "Admin@123")]
    public async Task Rejects_invalid_credentials(string username, string password)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/auth/token", new TokenRequest(username, password));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Rejects_missing_credentials()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/auth/token", new TokenRequest("", ""));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("GET", "/orders")]
    [InlineData("POST", "/orders")]
    [InlineData("GET", "/orders/8f2b6c1e-0001-4a6e-9b1a-000000000001")]
    [InlineData("POST", "/orders/8f2b6c1e-0001-4a6e-9b1a-000000000001/confirm")]
    [InlineData("POST", "/orders/8f2b6c1e-0001-4a6e-9b1a-000000000001/cancel")]
    public async Task Order_endpoints_require_a_token(string method, string url)
    {
        var response = await factory.CreateClient().SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Rejects_tampered_token()
    {
        var client = await factory.AuthenticatedAsync(Users.Customer1);
        var token = client.DefaultRequestHeaders.Authorization!.Parameter!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", token[..^4] + "AAAA");

        var response = await client.GetAsync("/orders");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
