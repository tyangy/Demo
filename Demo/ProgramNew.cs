//using Demo;
//using Demo.Services;
//using OpenTelemetry;
//using OpenTelemetry.Exporter;
//using OpenTelemetry.Resources;
//using OpenTelemetry.Trace;
//using Serilog;

//var builder = WebApplication.CreateBuilder(args);

//var serviceName = "AliyunTracingDemo";

//// 读取配置
//var endpoint = builder.Configuration["Otlp:Endpoint"];
//var enableTracing = builder.Configuration.GetValue<bool>("Tracing:Enabled", false);

//// 清理端点：如果是 HTTP 格式，转换为 gRPC 格式
//if (!string.IsNullOrEmpty(endpoint) && endpoint.Contains("/api/otlp/traces"))
//{
//    endpoint = endpoint.Split("/api/otlp/traces")[0];
//    Console.WriteLine($"已转换为 gRPC 端点: {endpoint}");
//}

//Console.WriteLine($"========== 配置信息 ==========");
//Console.WriteLine($"环境: {builder.Environment.EnvironmentName}");
//Console.WriteLine($"端点: {endpoint ?? "未配置"}");
//Console.WriteLine($"链路追踪启用: {enableTracing && !string.IsNullOrEmpty(endpoint)}");
//Console.WriteLine("================================");

//// 配置日志
//Log.Logger = new LoggerConfiguration()
//    .MinimumLevel.Debug()
//    .WriteTo.Console(outputTemplate:
//        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
//    .CreateLogger();

//builder.Host.UseSerilog();
//#region 链路追踪
//var tracing = builder.Configuration.GetSection("Tracing");
//if (tracing.Exists())
//{
//    if (!string.IsNullOrEmpty(endpoint))
//    {
//        Utility.Logger?.LogInformation("加载链路追踪");
//        var application = builder.Configuration.GetValue("Application", Utility.ServiceName);
//        var machine = builder.Configuration.GetValue("Machine", Environment.MachineName);
//        var version = builder.Configuration.GetValue("Version", Utility.Version);
//        var sources = new string[] { Utility.ServiceName, $"{Utility.ServiceName}.*" };

//        builder.Services.AddOpenTelemetry().WithTracing((ot) =>
//        {
//            ot.ConfigureResource((resource) =>
//            {
//                resource.AddService(
//                    serviceName: application,
//                    serviceNamespace: Utility.ServiceName,
//                    serviceVersion: version,
//                    serviceInstanceId: machine,
//                    autoGenerateServiceInstanceId: false
//                );
//            }).AddSource(sources);

//            // 配置自动埋点
//            ot.AddAspNetCoreInstrumentation((options) => { });
//            ot.AddHttpClientInstrumentation((options) => { });

//            if (Environment.UserInteractive)
//            {
//                ot.AddConsoleExporter();
//            }

//            if (!builder.Configuration.GetValue("disable-tracing", false))
//            {
//                ot.AddOtlpExporter((opt) =>
//                {
//                    opt.Endpoint = new Uri(endpoint);  // 使用完整端点
//                    opt.Protocol = OtlpExportProtocol.HttpProtobuf;  // 使用 HTTP 协议
//                    // 不设置其他选项，使用默认值
//                });

//                Utility.Logger?.LogInformation($"OTLP 导出器配置完成: {endpoint}");
//            }
//        });
//    }
//}
//#endregion

//// 注册服务
//builder.Services.AddScoped<IOrderService, OrderService>();
//builder.Services.AddScoped<IUserService, UserService>();
//builder.Services.AddHttpClient();
//builder.Services.AddControllers();
//builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();

//var app = builder.Build();

//// 测试端点
//app.MapGet("/test", async (ILogger<Program> logger) =>
//{
//    var traceId = System.Diagnostics.Activity.Current?.TraceId.ToString();
//    Console.WriteLine($"🎯 测试端点被调用 - TraceId: {traceId ?? "未生成"}");
//    logger.LogInformation("测试端点被调用，TraceId: {TraceId}", traceId);
    
//    await Task.Delay(100);
    
//    return Results.Ok(new
//    {
//        message = "链路追踪测试成功",
//        timestamp = DateTime.UtcNow,
//        traceId = traceId
//    });
//});

//app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI();
//}

//app.UseHttpsRedirection();
//app.UseAuthorization();
//app.MapControllers();

//Console.WriteLine("========================================");
//Console.WriteLine($"🚀 应用启动完成");
//Console.WriteLine($"   环境: {builder.Environment.EnvironmentName}");
//Console.WriteLine($"   端点: {endpoint ?? "未配置"}");
//Console.WriteLine($"   协议: gRPC");
//Console.WriteLine($"   测试地址: https://localhost:5001/test");
//Console.WriteLine("========================================");

//app.Run();