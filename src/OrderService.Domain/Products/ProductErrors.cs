using OrderService.Domain.Common;

namespace OrderService.Domain.Products;

public static class ProductErrors
{
    public static Error NotFound(Guid productId) =>
        Error.Validation("product.not_found", $"Product '{productId}' does not exist.");

    public static Error InvalidQuantity(int quantity) =>
        Error.Validation("product.invalid_quantity", $"Quantity must be greater than zero (received {quantity}).");

    public static Error InsufficientStock(Guid productId, int available, int requested) =>
        Error.Conflict(
            "product.insufficient_stock",
            $"Insufficient stock for product '{productId}': requested {requested}, available {available}.");

    public static Error CurrencyMismatch(Guid productId, Currency productCurrency, Currency orderCurrency) =>
        Error.Validation(
            "product.currency_mismatch",
            $"Product '{productId}' is priced in {productCurrency} but the order currency is {orderCurrency}.");
}
