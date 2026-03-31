using System.Diagnostics;

namespace Demo
{
    public sealed partial class Endpoints
    {
        /// <summary>
        /// 健康检查
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static async Task Health(HttpContext context)
        {
            using var tracer = Utility.CreateActivitySource<Endpoint>();
            using var activity = tracer.StartActivity($"{tracer.Name}.{nameof(Health)}", ActivityKind.Server);
            var logger = context.RequestServices.GetService<ILogger<Endpoints>>();
            var now = DateTimeOffset.UtcNow.LocalDateTime;
            logger?.LogInformation("{name}: {version}:{now:yyyy-MM-dd HH:mm:ss}", Utility.ServiceName, Utility.Version, now);
            await context.Response.WriteAsync($"{Utility.ServiceName}: {Utility.Version}:{now:yyyy-MM-dd HH:mm:ss}");
        }
    }
}
