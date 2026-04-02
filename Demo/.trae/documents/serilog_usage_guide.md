# Serilog 使用指南

## 1. 安装 Serilog 包

首先，需要安装必要的 Serilog 包：

```bash
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Sinks.File
dotnet add package Serilog.Enrichers.Environment
dotnet add package Serilog.Enrichers.Thread
```

## 2. 基本配置

### 2.1 代码配置

在 `Program.cs` 文件中配置 Serilog：

```csharp
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 配置 Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.WithProperty("Application", "YourApplicationName")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {TraceId} {SpanId} {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {TraceId} {SpanId} {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// 其他配置...

var app = builder.Build();

// 应用启动
app.Run();
```

### 2.2 配置文件配置

在 `appsettings.json` 中添加 Serilog 配置：

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.AspNetCore": "Warning",
        "System": "Warning"
      }
    },
    "Enrich": [ "FromLogContext", "WithMachineName", "WithThreadId" ],
    "Properties": {
      "Application": "YourApplicationName"
    }
  }
}
```

## 3. 基本使用

### 3.1 在控制器中使用

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private readonly ILogger<WeatherForecastController> _logger;

    public WeatherForecastController(ILogger<WeatherForecastController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public IEnumerable<WeatherForecast> Get()
    {
        _logger.LogInformation("Getting weather forecast");
        
        try
        {
            // 业务逻辑
            var forecasts = Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            }).ToArray();
            
            _logger.LogInformation("Weather forecast retrieved successfully, count: {Count}", forecasts.Length);
            return forecasts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting weather forecast");
            throw;
        }
    }
}
```

### 3.2 在服务中使用

```csharp
using Microsoft.Extensions.Logging;

public class OrderService : IOrderService
{
    private readonly ILogger<OrderService> _logger;

    public OrderService(ILogger<OrderService> logger)
    {
        _logger = logger;
    }

    public async Task<Order> CreateOrder(OrderRequest request)
    {
        using (_logger.BeginScope(new Dictionary<string, object> { { "OrderId", request.OrderId } }))
        {
            _logger.LogInformation("Creating order for customer {CustomerId}", request.CustomerId);
            
            try
            {
                // 业务逻辑
                var order = await _orderRepository.CreateAsync(request);
                _logger.LogInformation("Order created successfully with id {OrderId}", order.Id);
                return order;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order");
                throw;
            }
        }
    }
}
```

## 4. 高级功能

### 4.1 结构化日志

```csharp
// 结构化日志，自动序列化对象
var user = new { Id = 123, Name = "John Doe", Email = "john@example.com" };
_logger.LogInformation("User {User} logged in", user);

// 或者使用命名参数
_logger.LogInformation("User {UserId} ({UserName}) logged in with email {UserEmail}", 
    user.Id, user.Name, user.Email);
```

### 4.2 日志上下文

```csharp
// 使用 BeginScope 添加上下文信息
using (_logger.BeginScope("Processing order {OrderId}", orderId))
{
    _logger.LogInformation("Starting order processing");
    // 处理订单...
    _logger.LogInformation("Order processing completed");
}

// 嵌套上下文
using (_logger.BeginScope("Request {RequestId}", requestId))
{
    _logger.LogInformation("Processing request");
    
    using (_logger.BeginScope("User {UserId}", userId))
    {
        _logger.LogInformation("Processing user data");
        // 处理用户数据...
    }
    
    _logger.LogInformation("Request processing completed");
}
```

### 4.3 与 OpenTelemetry 集成

```csharp
// 在 Serilog 配置中添加 OpenTelemetry 集成
Log.Logger = new LoggerConfiguration()
    // 其他配置...
    .Enrich.WithProperty("TraceId", Activity.Current?.TraceId.ToString())
    .Enrich.WithProperty("SpanId", Activity.Current?.SpanId.ToString())
    // 其他配置...
    .CreateLogger();

// 或者使用 Serilog.Enrichers.OpenTelemetry 包
// dotnet add package Serilog.Enrichers.OpenTelemetry

Log.Logger = new LoggerConfiguration()
    // 其他配置...
    .Enrich.WithOpenTelemetry()
    // 其他配置...
    .CreateLogger();
```

### 4.4 自定义日志级别

```csharp
// 定义自定义日志级别
public static class LogLevelExtensions
{
    public static void LogCustom(this ILogger logger, string message, params object[] args)
    {
        logger.Log(new LogLevel(10000, "Custom"), message, args);
    }
}

// 使用自定义日志级别
_logger.LogCustom("This is a custom log message");
```

## 5. 最佳实践

### 5.1 日志级别使用

- **Trace**：最详细的日志，通常只在开发环境使用
- **Debug**：调试信息，开发和测试环境使用
- **Information**：一般信息，生产环境也可以使用
- **Warning**：警告信息，需要关注但不影响系统运行
- **Error**：错误信息，影响系统功能但不导致崩溃
- **Critical**：严重错误，导致系统崩溃或核心功能不可用

### 5.2 日志内容规范

1. **清晰明确**：日志信息应该清晰明了，能够快速理解发生了什么
2. **包含上下文**：添加足够的上下文信息，如用户ID、订单ID等
3. **避免敏感信息**：不要记录密码、令牌等敏感信息
4. **结构化**：使用结构化日志，便于后期分析
5. **一致格式**：保持团队内日志格式一致

### 5.3 性能考虑

1. **异步日志**：在高流量场景下使用异步日志
2. **批量写入**：配置批量写入，减少I/O操作
3. **适当采样**：在高流量场景下使用采样策略
4. **避免过度日志**：不要在循环中记录过多日志

## 6. 常见问题和解决方案

| 问题 | 原因 | 解决方案 |
|------|------|----------|
| 日志文件占用 | 日志文件过大 | 使用滚动文件和保留策略 |
| 性能下降 | 日志写入频繁 | 使用异步日志和批量写入 |
| 敏感信息泄露 | 记录了敏感信息 | 实现日志过滤器，过滤敏感信息 |
| 日志丢失 | 应用崩溃 | 使用 Serilog.Sinks.Async 确保日志异步写入 |
| 配置复杂 | 配置项过多 | 使用配置文件管理配置，避免硬编码 |

## 7. 示例配置

### 7.1 开发环境配置

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.AspNetCore": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {TraceId} {SpanId} {Message:lj}{NewLine}{Exception}"
        }
      }
    ],
    "Enrich": [ "FromLogContext", "WithMachineName", "WithThreadId" ]
  }
}
```

### 7.2 生产环境配置

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.AspNetCore": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "logs/log-.txt",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7,
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {TraceId} {SpanId} {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "Seq",
        "Args": {
          "serverUrl": "http://seq-server:5341"
        }
      }
    ],
    "Enrich": [ "FromLogContext", "WithMachineName", "WithThreadId" ]
  }
}
```

## 8. 总结

Serilog 是一个功能强大的结构化日志框架，通过本指南的配置和使用方法，您可以：

1. 获得结构化的、易于分析的日志数据
2. 与 OpenTelemetry 等系统无缝集成
3. 提高系统的可观测性和可维护性
4. 快速定位和解决系统问题

通过遵循最佳实践，您可以充分发挥 Serilog 的优势，为您的应用程序提供可靠的日志记录能力。