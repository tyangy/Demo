using Hestia.Logging.AliCloudLogService;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Demo
{
    public class Utility
    {
        public static readonly StringComparison DefaultStringComparison = StringComparison.OrdinalIgnoreCase;
        public static readonly Encoding DefaultEncoding = Encoding.UTF8;
        public static readonly StringComparer DefaultStringComparer = StringComparer.OrdinalIgnoreCase;

        public static bool IsInRole(params WindowsBuiltInRole[] roles)
        {
#pragma warning disable CA1416 // 验证平台兼容性      
            var id = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(id);
            return roles.Any(role => principal.IsInRole(role));
#pragma warning restore CA1416 // 验证平台兼容性
        }

        public static void DefaultLogging(ILoggingBuilder logging)
        {
            logging.ClearProviders();
            if (Environment.UserInteractive)
            {
                logging.AddConsole();
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
#pragma warning disable CA1416 // 验证平台兼容性   
                if (IsInRole(WindowsBuiltInRole.Administrator))
                {
                    if (!EventLog.SourceExists(CurrentAssembly.Name))
                    {
                        // https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.eventlog.createeventsource
                        // 需要管理员权限，运行一次创建即可
                        EventLog.CreateEventSource(CurrentAssembly.Name, "Application");
                    }
                    if (EventLog.SourceExists(CurrentAssembly.Name))
                    {
                        logging.AddEventLog((options) =>
                        {
                            options.SourceName = CurrentAssembly.Name;
                            options.LogName = "Application";
                        });
                    }
                }
#pragma warning restore CA1416 // 验证平台兼容性
            }
        }

        public static void ConfigureLogging(ILoggingBuilder logging)
        {
            // 基础日志
            logging.AddConsole();
            logging.AddDebug();

            // 临时禁用阿里云日志服务
            // logging.AddAliCloudLogService();

            // 或者添加条件编译
            #if !DISABLE_ALICLOUD_LOG
                        // logging.AddAliCloudLogService();
            #endif
        }

        public static void GlobalExceptionHandler(Exception ex)
        {
            Logger.LogError(ex, "{message}({base})", ex.Message, ex.GetBaseException().Message);
        }

        #region 日志
        private static readonly Lazy<ILogger<Program>> logger = new(() =>
        {
            return LoggerFactory.Create(DefaultLogging).CreateLogger<Program>();
        });
        /// <summary>
        /// 全局日志
        /// </summary>
        public static ILogger<Program> Logger { get { return logger.Value; } }
        #endregion

        #region 程序集
        private static readonly Lazy<AssemblyName> assembly = new(() =>
        {
            return typeof(Program).Assembly.GetName();
        });
        private static readonly string name = AppDomain.CurrentDomain.FriendlyName;
        /// <summary>
        /// 当前程序集
        /// </summary>
        public static AssemblyName CurrentAssembly { get { return assembly.Value; } }
        /// <summary>
        /// 程序集名称
        /// </summary>
        public static string ServiceName { get { return name; } }
        /// <summary>
        /// 程序集版本
        /// </summary>
        public static string Version { get { return CurrentAssembly.Version?.ToString() ?? string.Empty; } }
        #endregion        

        #region 配置
        public static T[] GetArrayFromSection<T>(IConfigurationSection configuration)
        {
            if (configuration.Exists()) { return configuration.Get<T[]>(); }
            return Array.Empty<T>();
        }
        #endregion

        #region 错误
        /// <summary>
        /// 参数非法
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="fmt"></param>
        /// <returns></returns>
        public static string ArgumentIllegalError<T>(string name, T value, Func<T, string> fmt = null)
        {
            return Error(nameof(ArgumentIllegalError), name, value, fmt);
        }
        /// <summary>
        /// 参数无效
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="fmt"></param>
        /// <returns></returns>
        public static string ArgumentInvalidError<T>(string name, T value, Func<T, string> fmt = null)
        {
            return Error(nameof(ArgumentInvalidError), name, value, fmt);
        }
        /// <summary>
        /// 配置非法
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="fmt"></param>
        /// <returns></returns>
        public static string ConfigurationIllegalError<T>(string name, T value, Func<T, string> fmt = null)
        {
            return Error(nameof(ConfigurationIllegalError), name, value, fmt);
        }
        /// <summary>
        /// 配置无效
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="fmt"></param>
        /// <returns></returns>
        public static string ConfigurationInvalidError<T>(string name, T value, Func<T, string> fmt = null)
        {
            return Error(nameof(ConfigurationInvalidError), name, value, fmt);
        }
        /// <summary>
        /// 通用错误
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="message"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="fmt"></param>
        /// <returns></returns>
        private static string Error<T>(string message, string name, T value, Func<T, string> fmt = null)
        {
            var error = fmt?.Invoke(value) ?? value?.ToString() ?? string.Empty;
            return $"{message}: {(string.IsNullOrEmpty(error) ? name : $"{name}({error})")}";
        }
        #endregion

        #region 异常
        public static void GlobalExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                GlobalExceptionHandler(ex);
            }
        }

        public static async Task GlobalExceptionMiddleware(HttpContext context, RequestDelegate next)
        {
            try
            {
                await next.Invoke(context);
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler(ex);
            }
        }
        #endregion

        #region 追踪
        public static ActivitySource CreateActivitySource<T>(string version = null)
        {
            var source = new ActivitySource($"{ServiceName}.{typeof(T).Name}", version ?? Version);
            return source;
        }
        #endregion
    }
}
