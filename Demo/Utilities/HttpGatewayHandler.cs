using System.Text.RegularExpressions;

namespace Demo
{
    public record GatewayRule
    {
        public string Regex { get; set; }
        public string AppCode { get; set; }

        public string Stage { get; set; }

        public bool Nonce { get; set; }
    }

    public class HttpGatewayHandler : DelegatingHandler
    {
        private readonly ILogger<HttpGatewayHandler> logger;
        private readonly IConfigurationSection configuration;

        public HttpGatewayHandler(IServiceProvider services) : this("Gateway", services) { }

        public HttpGatewayHandler(string name, IServiceProvider services)
        {
            logger = services.GetService<ILogger<HttpGatewayHandler>>();
            configuration = services.GetRequiredService<IConfiguration>().GetSection(name);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var rules = Utility.GetArrayFromSection<GatewayRule>(configuration.GetSection("Rules"));
            var url = request.RequestUri.AbsoluteUri;
            var options = configuration.GetValue("RegexOptions", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

            var rule = rules.FirstOrDefault(x => Regex.IsMatch(url, x.Regex, options));

            if (rule is not null)
            {
                if (!string.IsNullOrEmpty(rule.AppCode))
                {
                    request.Headers.Add("Authorization", string.Join(" ", "APPCODE", rule.AppCode));
                }
                if (!string.IsNullOrEmpty(rule.Stage))
                {
                    request.Headers.Add("X-Ca-Stage", rule.Stage.ToUpper());
                }
                if (rule.Nonce)
                {
                    request.Headers.Add("X-Ca-Nonce", Guid.NewGuid().ToString("N"));
                }
            }

            var response = await base.SendAsync(request, token);
            return response;
        }
    }
}
