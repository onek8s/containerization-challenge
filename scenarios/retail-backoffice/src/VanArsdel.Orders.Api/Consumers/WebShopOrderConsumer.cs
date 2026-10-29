using MassTransit;
using VanArsdel.Contracts.Orders;
using VanArsdel.Orders.Api.Domain;

namespace VanArsdel.Orders.Api.Consumers;

/// <summary>Runs on the web shop bus (Azure Service Bus queue "webshop-orders").</summary>
public class WebShopOrderConsumer : IConsumer<WebShopOrderSubmitted>
{
    private readonly OrderService _orders;
    private readonly ILogger<WebShopOrderConsumer> _logger;

    public WebShopOrderConsumer(OrderService orders, ILogger<WebShopOrderConsumer> logger)
    {
        _orders = orders;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<WebShopOrderSubmitted> context)
    {
        var order = await _orders.CreateFromWebShopAsync(context.Message, context.CancellationToken);
        if (order != null)
        {
            _logger.LogInformation("Web order {OrderNo} received: {Lines} lines, {Total} {Currency}, {Delivery}",
                order.OrderNo, order.Lines.Count, order.Total, order.Currency, order.DeliveryMethod);
        }
    }
}
