using System.Text;
using System.Text.RegularExpressions;

namespace Demo
{
    public record HttpRule
    {
        public string Regex { get; set; }

        public bool RequestBody { get; set; }

        public bool ResponseBody { get; set; }

    }

    public class HttpLogHandler : DelegatingHandler
    {
        private readonly ILogger<HttpLogHandler> logger;
        private readonly IConfigurationSection configuration;

        public HttpLogHandler(IServiceProvider services) : this("Http", services) { }

        public HttpLogHandler(string name, IServiceProvider services)
        {
            logger = services.GetService<ILogger<HttpLogHandler>>();
            configuration = services.GetRequiredService<IConfiguration>().GetSection(name);
        }

        private static async Task<byte[]> GetBodyBytes(HttpContent content, CancellationToken token)
        {
            if (content is null) { return Array.Empty<byte>(); }
            try
            {
                return await content.ReadAsByteArrayAsync(token);
            }
            catch (Exception ex)
            {
                Utility.GlobalExceptionHandler(ex);
                return Array.Empty<byte>();
            }
        }


        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var rules = Utility.GetArrayFromSection<HttpRule>(configuration.GetSection("Rules"));
            var url = request.RequestUri.AbsoluteUri;
            var options = configuration.GetValue("RegexOptions", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

            var rule = rules.FirstOrDefault(x => Regex.IsMatch(url, x.Regex, options));

            var response = await base.SendAsync(request, token);

            if (rule is null)
            {
                return response;
            }

            var log = new StringBuilder();
            try
            {
                log.AppendLine($"{request.Method} {request.RequestUri.AbsoluteUri} HTTP/{request.Version}");

                foreach (var header in request.Headers)
                {
                    foreach (var value in header.Value)
                    {
                        log.AppendLine($"{header.Key}: {value}");
                    }
                }
                log.AppendLine();

                if (rule.RequestBody)
                {
                    var body = await GetBodyBytes(request.Content, token);
                    if (body.Length > 0)
                    {
                        log.AppendLine(Convert.ToHexString(body));
                    }
                }

                log.AppendLine();

                log.AppendLine($"HTTP/{response.Version} {(int)response.StatusCode} {response.StatusCode}");

                foreach (var header in response.Headers)
                {
                    foreach (var value in header.Value)
                    {
                        log.AppendLine($"{header.Key}: {value}");
                    }
                }

                log.AppendLine();

                if (rule.ResponseBody)
                {
                    var body = await GetBodyBytes(response.Content, token);
                    if (body.Length > 0)
                    {
                        log.AppendLine(Convert.ToHexString(body));
                    }
                }
            }
            catch (Exception ex)
            {
                Utility.GlobalExceptionHandler(ex);
            }
            finally
            {
                if (log.Length > 0)
                {
                    logger?.LogInformation("{http}", log.ToString());
                }
            }

            return response;
        }
    }
}
