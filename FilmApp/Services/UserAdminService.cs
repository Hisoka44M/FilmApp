using FilmApp.Data;
using FilmApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FilmApp.Services;

/// <summary>Управление пользователями (только для администраторов): список, удаление, смена роли.</summary>
public class UserAdminService
{
    private readonly AppDbContext _db;
    private readonly ILogger<UserAdminService> _log;

    public UserAdminService(AppDbContext db, ILogger<UserAdminService> log)
    {
        _db = db;
        _log = log;
    }

    /// <summary>Все пользователи по алфавиту.</summary>
    public async Task<List<User>> GetUsersAsync()
    {
        var users = await _db.Users.AsNoTracking().ToListAsync();
        return users.OrderBy(u => u.Username, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>
    /// Удаляет пользователя вместе с его просмотрами и подборками.
    /// Последнего администратора удалить нельзя.
    /// </summary>
    public async Task<OperationResult> DeleteUserAsync(int actingUserId, int targetUserId)
    {
        try
        {
            var denied = await CheckAdminAsync(actingUserId);
            if (denied is not null)
                return denied;

            var target = await _db.Users.FirstOrDefaultAsync(u => u.UserID == targetUserId);
            if (target is null)
                return OperationResult.Fail("Пользователь не найден.");

            if (target.Role == UserRole.Admin && await IsLastAdminAsync())
                return OperationResult.Fail("Нельзя удалить последнего администратора.");

            _db.Users.Remove(target);
            await _db.SaveChangesAsync();

            _log.LogInformation("Пользователь {Username} удалён администратором {AdminId}",
                target.Username, actingUserId);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Ошибка при удалении пользователя {UserId}", targetUserId);
            return OperationResult.Fail("Не удалось удалить пользователя. Подробности в журнале.");
        }
    }

    /// <summary>Меняет роль пользователя. Последнего администратора понизить нельзя.</summary>
    public async Task<OperationResult> ChangeRoleAsync(int actingUserId, int targetUserId, UserRole newRole)
    {
        try
        {
            var denied = await CheckAdminAsync(actingUserId);
            if (denied is not null)
                return denied;

            var target = await _db.Users.FirstOrDefaultAsync(u => u.UserID == targetUserId);
            if (target is null)
                return OperationResult.Fail("Пользователь не найден.");

            if (target.Role == UserRole.Admin && newRole == UserRole.User && await IsLastAdminAsync())
                return OperationResult.Fail("Нельзя лишить прав последнего администратора.");

            target.Role = newRole;
            await _db.SaveChangesAsync();

            _log.LogInformation("Роль пользователя {Username} изменена на {Role}", target.Username, newRole);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Ошибка при смене роли пользователя {UserId}", targetUserId);
            return OperationResult.Fail("Не удалось изменить роль. Подробности в журнале.");
        }
    }

    /// <summary>Null, если действие выполняет администратор, иначе отказ.</summary>
    private async Task<OperationResult?> CheckAdminAsync(int userId)
    {
        bool isAdmin = await _db.Users.AnyAsync(u => u.UserID == userId && u.Role == UserRole.Admin);
        return isAdmin ? null : OperationResult.Fail("Недостаточно прав для этого действия.");
    }

    private async Task<bool> IsLastAdminAsync() =>
        await _db.Users.CountAsync(u => u.Role == UserRole.Admin) <= 1;
}