using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OrderService.Api.Contracts;
using OrderService.Application.Auth;
using OrderService.Application.Orders.Dtos;

namespace OrderService.Api.IntegrationTests;

internal static class Users
{
    public static readonly Guid Customer1Id = Guid.Parse("c1a1e7b4-0000-4000-8000-000000000001");
    public static readonly Guid Customer2Id = Guid.Parse("c1a1e7b4-0000-4000-8000-000000000002");

    public static readonly TokenRequest Admin = new("admin", "Admin@123");
    public static readonly TokenRequest Customer1 = new("customer1", "Customer@123");
    public static readonly TokenRequest Customer2 = new("customer2", "Customer@123");
}

internal static class ApiClient
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<HttpClient> AuthenticatedAsync(this OrderServiceApiFactory factory, TokenRequest user)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/token", user);
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenDto>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return client;
    }

    public static Task<HttpResponseMessage> CreateOrderAsync(this HttpClient client, Guid customerId, params (Guid ProductId, int Quantity)[] items) =>
        client.PostAsJsonAsync("/orders", new CreateOrderRequest(
            customerId,
            "BRL",
            items.Select(i => new CreateOrderItemRequest(i.ProductId, i.Quantity)).ToArray()));

    public static async Task<OrderDto> CreateOrderOkAsync(this HttpClient client, Guid customerId, params (Guid ProductId, int Quantity)[] items)
    {
        var response = await client.CreateOrderAsync(customerId, items);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderDto>(Json))!;
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;
}
