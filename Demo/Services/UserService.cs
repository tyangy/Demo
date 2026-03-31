using Demo.Models;
using System.Diagnostics;

namespace Demo.Services;

public interface IUserService
{
    Task<User?> GetUserAsync(string userId);
}

public class UserService : IUserService
{
    private readonly ILogger<UserService> _logger;
    private static readonly Dictionary<string, User> _users = new()
    {
        ["user001"] = new User { Id = "user001", Name = "张三", Email = "zhangsan@example.com", CreatedAt = DateTime.UtcNow.AddDays(-30) },
        ["user002"] = new User { Id = "user002", Name = "李四", Email = "lisi@example.com", CreatedAt = DateTime.UtcNow.AddDays(-15) },
        ["user003"] = new User { Id = "user003", Name = "王五", Email = "wangwu@example.com", CreatedAt = DateTime.UtcNow.AddDays(-5) }
    };

    public UserService(ILogger<UserService> logger)
    {
        _logger = logger;
    }

    public async Task<User?> GetUserAsync(string userId)
    {
        using var activity = Activity.Current?.Source.StartActivity("UserService.GetUser");
        activity?.SetTag("user.id", userId);

        _logger.LogDebug("查询用户信息 | UserId: {UserId}", userId);

        // 模拟数据库查询
        await Task.Delay(10);

        _users.TryGetValue(userId, out var user);

        if (user != null)
        {
            activity?.SetTag("user.name", user.Name);
            _logger.LogDebug("用户信息已找到 | UserId: {UserId} | Name: {Name}", userId, user.Name);
        }
        else
        {
            _logger.LogDebug("用户不存在 | UserId: {UserId}", userId);
        }

        return user;
    }
}