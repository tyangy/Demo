using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text.Json;
using Demo.Services;
using Demo.Models;

namespace Demo.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class OrderController : ControllerBase
{
    private readonly ILogger<OrderController> _logger;
    private readonly IOrderService _orderService;
    private readonly IUserService _userService;

    public OrderController(
        ILogger<OrderController> logger,
        IOrderService orderService,
        IUserService userService)
    {
        _logger = logger;
        _orderService = orderService;
        _userService = userService;
    }

    /// <summary>
    /// 创建订单
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var activity = Activity.Current;
        activity?.SetTag("business.user_id", request.UserId);
        activity?.SetTag("business.order_items_count", request.Items?.Count ?? 0);

        _logger.LogInformation(
            "开始创建订单 | UserId: {UserId} | Items: {ItemCount} | TotalAmount: {TotalAmount}",
            request.UserId,
            request.Items?.Count ?? 0,
            request.Items?.Sum(i => i.Price * i.Quantity) ?? 0);

        try
        {
            // 验证用户
            var user = await _userService.GetUserAsync(request.UserId);
            if (user == null)
            {
                _logger.LogWarning("用户不存在 | UserId: {UserId}", request.UserId);
                return BadRequest(new { error = "用户不存在" });
            }

            // 创建订单
            var order = await _orderService.CreateOrderAsync(request);

            _logger.LogInformation(
                "订单创建成功 | OrderId: {OrderId} | UserId: {UserId} | Status: {Status}",
                order.Id,
                order.UserId,
                order.Status);

            return Ok(order);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "业务逻辑错误 | UserId: {UserId}", request.UserId);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建订单失败 | UserId: {UserId} | Request: {@Request}",
                request.UserId, request);
            return StatusCode(500, new { error = "创建订单失败" });
        }
    }

    /// <summary>
    /// 获取订单详情（演示链路追踪）
    /// </summary>
    [HttpGet("{orderId}")]
    public async Task<IActionResult> GetOrder(string orderId)
    {
        using var activity = Activity.Current?.Source.StartActivity("GetOrderDetail");
        activity?.SetTag("order.id", orderId);

        _logger.LogInformation("查询订单 | OrderId: {OrderId}", orderId);

        try
        {
            var order = await _orderService.GetOrderAsync(orderId);

            if (order == null)
            {
                _logger.LogWarning("订单不存在 | OrderId: {OrderId}", orderId);
                return NotFound();
            }

            // 同时获取用户信息（演示跨服务调用）
            var user = await _userService.GetUserAsync(order.UserId);

            var result = new
            {
                Order = order,
                User = user
            };

            _logger.LogInformation("订单查询成功 | OrderId: {OrderId} | Status: {Status}",
                orderId, order.Status);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询订单失败 | OrderId: {OrderId}", orderId);
            return StatusCode(500, new { error = "查询订单失败" });
        }
    }

    /// <summary>
    /// 批量处理订单（演示复杂调用链）
    /// </summary>
    [HttpPost("batch")]
    public async Task<IActionResult> BatchProcess([FromBody] BatchOrderRequest request)
    {
        using var activity = Activity.Current?.Source.StartActivity("BatchProcessOrders");
        activity?.SetTag("batch.count", request.OrderIds.Count);

        _logger.LogInformation("开始批量处理订单 | Count: {Count} | Operation: {Operation}",
            request.OrderIds.Count, request.Operation);

        var sw = Stopwatch.StartNew();
        var results = new List<BatchResult>();

        foreach (var orderId in request.OrderIds)
        {
            try
            {
                using var childActivity = Activity.Current?.Source.StartActivity($"ProcessOrder_{orderId}");
                childActivity?.SetTag("order.id", orderId);

                var result = await _orderService.ProcessOrderAsync(orderId, request.Operation);
                results.Add(new BatchResult { OrderId = orderId, Success = true, Data = result });

                _logger.LogDebug("订单处理成功 | OrderId: {OrderId}", orderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "订单处理失败 | OrderId: {OrderId}", orderId);
                results.Add(new BatchResult { OrderId = orderId, Success = false, Error = ex.Message });
            }
        }

        sw.Stop();

        _logger.LogInformation(
            "批量处理完成 | Total: {Total} | Success: {Success} | Failed: {Failed} | Duration: {Duration}ms",
            results.Count,
            results.Count(r => r.Success),
            results.Count(r => !r.Success),
            sw.ElapsedMilliseconds);

        return Ok(new
        {
            Total = results.Count,
            SuccessCount = results.Count(r => r.Success),
            FailedCount = results.Count(r => !r.Success),
            Duration = sw.ElapsedMilliseconds,
            Results = results
        });
    }
}

