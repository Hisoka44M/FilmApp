using FilmApp.Data;
using FilmApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FilmApp.Services;

/// <summary>Результат операции: успех либо понятное пользователю сообщение об ошибке.</summary>
public record OperationResult(bool Success, string? Error = null)
{
    public static OperationResult Ok() => new(true);
    public static OperationResult Fail(string error) => new(false, error);
}

/// <summary>Регистрация, вход и создание первого администратора.</summary>
public class AuthService
{
    private const int MinUsernameLength = 3;
    private const int MaxUsernameLength = 50;
    private const int MinPasswordLength = 6;

    private readonly AppDbContext _db;
    private readonly ILogger<AuthService> _log;

    public AuthService(AppDbContext db, ILogger<AuthService> log)
    {
        _db = db;
        _log = log;
    }

    /// <summary>Есть ли в системе хотя бы один пользователь (для первого запуска).</summary>
    public Task<bool> HasAnyUserAsync() => _db.Users.AnyAsync();

    /// <summary>Создаёт обычного пользователя.</summary>
    public Task<OperationResult> RegisterAsync(string username, string password)
        => CreateUserAsync(username, password, UserRole.User);

    /// <summary>Создаёт администратора (первый запуск или из панели администратора).</summary>
    public Task<OperationResult> CreateAdminAsync(string username, string password)
        => CreateUserAsync(username, password, UserRole.Admin);

    /// <summary>Проверяет логин и пароль. Возвращает пользователя или null.</summary>
    public async Task<User?> LoginAsync(string username, string password)
    {
        try
        {
            username = (username ?? string.Empty).Trim();
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);

            if (user is null || !PasswordHasher.Verify(password, user.PasswordHash))
            {
                _log.LogWarning("Неудачная попытка входа: {Username}", username);
                return null;
            }

            _log.LogInformation("Вход выполнен: {Username}", username);
            return user;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Ошибка при входе пользователя");
            return null;
        }
    }

    private async Task<OperationResult> CreateUserAsync(string username, string password, UserRole role)
    {
        username = (username ?? string.Empty).Trim();

        if (username.Length < MinUsernameLength || username.Length > MaxUsernameLength)
            return OperationResult.Fail(
                $"Логин должен содержать от {MinUsernameLength} до {MaxUsernameLength} символов.");

        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            return OperationResult.Fail($"Пароль должен содержать не менее {MinPasswordLength} символов.");

        try
        {
            if (await _db.Users.AnyAsync(u => u.Username == username))
                return OperationResult.Fail("Пользователь с таким логином уже существует.");

            _db.Users.Add(new User
            {
                Username = username,
                PasswordHash = PasswordHasher.Hash(password),
                Role = role,
                RegisteredAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            _log.LogInformation("Создан пользователь {Username} с ролью {Role}", username, role);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Ошибка при создании пользователя {Username}", username);
            return OperationResult.Fail("Не удалось сохранить пользователя. Подробности в журнале.");
        }
    }
}