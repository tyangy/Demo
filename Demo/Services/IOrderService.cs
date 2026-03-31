using Demo.Models;
using System.Diagnostics;

namespace Demo.Services;

public interface IOrderService
{
    Task<Order> CreateOrderAsync(CreateOrderRequest request);
    Task<Order?> GetOrderAsync(string orderId);
    Task<object> ProcessOrderAsync(string orderId, string operation);
    Task<List<Order>> GetUserOrdersAsync(string userId);
}

public class OrderService : IOrderService
{
    private readonly ILogger<OrderService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private static readonly Dictionary<string, Order> _orders = new();
    private static int _orderCounter = 0;

    public OrderService(ILogger<OrderService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<Order> CreateOrderAsync(CreateOrderRequest request)
    {
        using var activity = Activity.Current?.Source.StartActivity("OrderService.CreateOrder");

        var orderId = $"ORD-{DateTime.Now:yyyyMMdd}-{++_orderCounter:D4}";

        activity?.SetTag("order.id", orderId);
        activity?.SetTag("order.total_items", request.Items.Count);

        // 模拟调用外部API（演示跨服务追踪）
        using (var externalActivity = Activity.Current?.Source.StartActivity("ExternalAPI.ValidateInventory"))
        {
            externalActivity?.SetTag("product.count", request.Items.Count);

            // 模拟HTTP调用（实际会生成新的Span）
            var client = _httpClientFactory.CreateClient();
            await Task.Delay(100); // 模拟网络延迟

            externalActivity?.SetStatus(ActivityStatusCode.Ok);
        }

        var order = new Order
        {
            Id = orderId,
            UserId = request.UserId,
            Items = request.Items.Select(i => new OrderItem
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                Price = i.Price
            }).ToList(),
            Status = OrderStatus.Created,
            CreatedAt = DateTime.UtcNow,
            TotalAmount = request.Items.Sum(i => i.Price * i.Quantity)
        };

        _orders[orderId] = order;

        // 记录关键业务事件
        _logger.LogInformation(
            "订单已创建 | OrderId: {OrderId} | UserId: {UserId} | Total: {Total:C}",
            order.Id,
            order.UserId,
            order.TotalAmount);

        return await Task.FromResult(order);
    }

    public async Task<Order?> GetOrderAsync(string orderId)
    {
        using var activity = Activity.Current?.Source.StartActivity("OrderService.GetOrder");
        activity?.SetTag("order.id", orderId);

        _logger.LogDebug("从缓存中获取订单 | OrderId: {OrderId}", orderId);

        _orders.TryGetValue(orderId, out var order);

        if (order != null)
        {
            activity?.SetTag("order.status", order.Status.ToString());
            _logger.LogDebug("订单已找到 | OrderId: {OrderId} | Status: {Status}",
                orderId, order.Status);
        }
        else
        {
            _logger.LogDebug("订单未找到 | OrderId: {OrderId}", orderId);
        }

        return await Task.FromResult(order);
    }

    public async Task<object> ProcessOrderAsync(string orderId, string operation)
    {
        using var activity = Activity.Current?.Source.StartActivity($"OrderService.{operation}");

        var order = await GetOrderAsync(orderId);
        if (order == null)
        {
            throw new InvalidOperationException($"订单不存在: {orderId}");
        }

        activity?.SetTag("order.status", order.Status.ToString());
        activity?.SetTag("order.operation", operation);

        _logger.LogInformation(
            "处理订单 | OrderId: {OrderId} | Operation: {Operation} | CurrentStatus: {Status}",
            orderId,
            operation,
            order.Status);

        // 模拟业务处理
        await Task.Delay(50);

        switch (operation.ToLower())
        {
            case "pay":
                order.Status = OrderStatus.Paid;
                order.PaidAt = DateTime.UtcNow;
                _logger.LogInformation("订单已支付 | OrderId: {OrderId}", orderId);
                break;

            case "ship":
                if (order.Status != OrderStatus.Paid)
                    throw new InvalidOperationException("订单未支付，无法发货");
                order.Status = OrderStatus.Shipped;
                order.ShippedAt = DateTime.UtcNow;
                _logger.LogInformation("订单已发货 | OrderId: {OrderId}", orderId);
                break;

            case "complete":
                order.Status = OrderStatus.Completed;
                order.CompletedAt = DateTime.UtcNow;
                _logger.LogInformation("订单已完成 | OrderId: {OrderId}", orderId);
                break;

            case "cancel":
                order.Status = OrderStatus.Cancelled;
                order.CancelledAt = DateTime.UtcNow;
                _logger.LogInformation("订单已取消 | OrderId: {OrderId}", orderId);
                break;

            default:
                throw new InvalidOperationException($"不支持的操作: {operation}");
        }

        return new
        {
            OrderId = orderId,
            Operation = operation,
            NewStatus = order.Status.ToString(),
            ProcessedAt = DateTime.UtcNow
        };
    }

    public async Task<List<Order>> GetUserOrdersAsync(string userId)
    {
        using var activity = Activity.Current?.Source.StartActivity("OrderService.GetUserOrders");
        activity?.SetTag("user.id", userId);

        _logger.LogInformation("查询用户订单 | UserId: {UserId}", userId);

        var orders = _orders.Values
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToList();

        _logger.LogInformation(
            "查询到 {Count} 个订单 | UserId: {UserId}",
            orders.Count,
            userId);

        return await Task.FromResult(orders);
    }
}