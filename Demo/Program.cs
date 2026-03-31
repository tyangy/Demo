using Demo.Models;
using Demo.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System;
using System.Threading.Tasks;

namespace Demo
{
    public class Program
    {
        private static void ConfigureTracing(TracerProviderBuilder tracing)
        {
            tracing.AddAspNetCoreInstrumentation((options) =>
            {
                options.RecordException = true;
            });
            tracing.AddHttpClientInstrumentation((options) =>
            {
                options.RecordException = true;
            });
        }

        private static void ConfigureEndpoints(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/Health", Endpoints.Health);
            endpoints.MapGet("/", () => "Hello World!");
        }

        public static async Task Main(string[] args)
        {
            #region 初始化
            Hestia.Core.Utility.RegisterProvider();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions()
            {
                Args = args,
                ContentRootPath = AppContext.BaseDirectory
            });
            #endregion

            #region 日志
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.AddDebug();
            #endregion

            #region 配置
            var nacos = builder.Configuration.GetSection("Nacos");
            if (nacos.Exists())
            {
                Console.WriteLine("加载 Nacos 配置");
                builder.Configuration.AddNacosV2Configuration(nacos);
            }
            #endregion

            #region 链路追踪
            var tracing = builder.Configuration.GetSection("Otlp");
            if (tracing.Exists())
            {
                var endpoint = tracing["Endpoint"];
                Console.WriteLine($"读取到 Endpoint: '{endpoint}'");

                if (!string.IsNullOrEmpty(endpoint))
                {
                    Console.WriteLine("加载链路追踪");
                    var application = builder.Configuration.GetValue("Application", "DemoService");
                    var machine = builder.Configuration.GetValue("Machine", Environment.MachineName);
                    var version = builder.Configuration.GetValue("Version", "1.0.0");
                    var serviceName = "DemoService";
                    var sources = new string[] { serviceName, $"{serviceName}.*" };

                    builder.Services.AddOpenTelemetry().WithTracing((ot) =>
                    {
                        ot.ConfigureResource((resource) =>
                        {
                            resource.AddService(
                                serviceName: application,
                                serviceNamespace: serviceName,
                                serviceVersion: version,
                                serviceInstanceId: machine,
                                autoGenerateServiceInstanceId: false
                            );
                        }).AddSource(sources);

                        ConfigureTracing(ot);

                        if (Environment.UserInteractive)
                        {
                            ot.AddConsoleExporter();
                        }

                        if (!builder.Configuration.GetValue("disable-tracing", false))
                        {
                            Console.WriteLine($"添加 ARMS OTLP 导出器");
                            ot.AddOtlpExporter((opt) =>
                            {
                                opt.Endpoint = new Uri(endpoint);
                                opt.Protocol = OtlpExportProtocol.HttpProtobuf;
                                opt.TimeoutMilliseconds = 30000;
                            });
                        }
                    });
                }
            }
            #endregion

            #region 基础服务
            // 注册 HttpClientHandler
            builder.Services.AddTransient<HttpGatewayHandler>();
            builder.Services.AddTransient<HttpLogHandler>();

            // HttpClientFactory
            builder.Services.AddHttpClient(string.Empty, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddHttpMessageHandler<HttpGatewayHandler>()
            .AddHttpMessageHandler<HttpLogHandler>();

            // ========== 注册所有业务服务 ==========
            builder.Services.AddScoped<IOrderService, OrderService>();
            builder.Services.AddScoped<IUserService, UserService>();  // 注册 UserService

            // 注册其他服务
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddHealthChecks();
            #endregion

            #region 构建应用
            Console.WriteLine("开始构建应用...");
            var app = builder.Build();
            Console.WriteLine("应用构建成功");
            #endregion

            #region 中间件和接口
            app.UseRouting();
            app.UseEndpoints(ConfigureEndpoints);
            app.MapControllers();
            app.MapHealthChecks("/health");
            #endregion

            #region 启动
            Console.WriteLine("应用启动中...");
            await app.RunAsync();
            #endregion
        }
    }
}