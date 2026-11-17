using FilmApp.Data;
using FilmApp.Models;
using FilmApp.Services.Composite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FilmApp.Services;

/// <summary>Результат создания подборки: сама подборка либо сообщение об ошибке.</summary>
public record CollectionResult(bool Success, Collection? Collection, string? Error)
{
    public static CollectionResult Ok(Collection collection) => new(true, collection, null);
    public static CollectionResult Fail(string error) => new(false, null, error);
}

/// <summary>Подборки пользователя: создание, состав, вложенность без циклов, дерево (Компоновщик).</summary>
public class CollectionService
{
    private const int MaxNameLength = 200;
    private const int MaxDescriptionLength = 1000;
    private const string NotFound = "Подборка не найдена.";

    private readonly AppDbContext _db;
    private readonly ILogger<CollectionService> _log;

    public CollectionService(AppDbContext db, ILogger<CollectionService> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<CollectionResult> CreateAsync(int userId, string name, string? description)
    {
        string? error = Validate(name, description, out string cleanName, out string? cleanDesc);
        if (error is not null)
            return CollectionResult.Fail(error);

        try
        {
            if (!await _db.Users.AnyAsync(u => u.UserID == userId))
                return CollectionResult.Fail("Пользователь не найден.");

            var collection = new Collection { UserID = userId, Name = cleanName, Description = cleanDesc };
            _db.Collections.Add(collection);
            await _db.SaveChangesAsync();
            return CollectionResult.Ok(collection);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Ошибка при создании подборки");
            return CollectionResult.Fail("Не удалось создать подборку. Подробности в журнале.");
        }
    }

    public async Task<OperationResult> UpdateAsync(int userId, int collectionId, string name, string? description)
    {
        string? error = Validate(name, description, out string cleanName, out string? cleanDesc);
        if (error is not null)
            return OperationResult.Fail(error);

        try
        {
            var collection = await FindOwnedAsync(userId, collectionId);
            if (collection is null)
                return OperationResult.Fail(NotFound);

            collection.Name = cleanName;
            collection.Description = cleanDesc;
            await _db.SaveChangesAsync();
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Ошибка при изменении подборки {Id}", collectionId);
            return OperationResult.Fail("Не удалось сохранить изменения. Подробности в журнале.");
        }
    }

    /// <summary>Удаляет подборку вместе с её элементами и ссылками на неё из других подборок. Фильмы остаются.</summary>
    public async Task<OperationResult> DeleteAsync(int userId, int collectionId)
    {
        try
        {
            var collection = await FindOwnedAsync(userId, collectionId);
            if (collection is null)
                return OperationResult.Fail(NotFound);

            _db.Collections.Remove(collection);
            await _db.SaveChangesAsync();
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Ошибка при удалении подборки {Id}", collectionId);
            return OperationResult.Fail("Не удалось удалить подборку. Подробности в журнале.");
        }
    }

    public async Task<OperationResult> AddMovieAsync(int userId, int collectionId, int movieId)
    {
        try
        {
            if (await FindOwnedAsync(userId, collectionId) is null)
                return OperationResult.Fail(NotFound);
            if (!await _db.Movies.AnyAsync(m => m.MovieID == movieId))
                return OperationResult.Fail("Фильм не найден.");
            if (await _db.CollectionItems.AnyAsync(i => i.CollectionID == collectionId && i.MovieID == movieId))
                return OperationResult.Fail("Фильм уже есть в этой подборке.");

            _db.CollectionItems.Add(new CollectionItem { CollectionID = collectionId, MovieID = movieId });
            await _db.SaveChangesAsync();
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Ошибка при добавлении фильма {MovieId} в подборку {Id}", movieId, collectionId);
            return OperationResult.Fail("Не удалось добавить фильм. Подробности в журнале.");
        }
    }

    /// <summary>Вкладывает одну подборку в другую. Запрещены вложение в себя и любые циклы.</summary>
    public async Task<OperationResult> AddChildCollectionAsync(int userId, int parentId, int childId)
    {
        try
        {
            // Обе подборки должны принадлежать одному пользователю
            if (await FindOwnedAsync(userId, parentId) is null || await FindOwnedAsync(userId, childId) is null)
                return OperationResult.Fail(NotFound);
            if (parentId == childId)
                return OperationResult.Fail("Подборка не может содержать саму себя.");
            if (await _db.CollectionItems.AnyAsync(i => i.CollectionID == parentId && i.ChildCollectionID == childId))
                return OperationResult.Fail("Эта подборка уже вложена.");
            if (await WouldCreateCycleAsync(userId, parentId, childId))
                return OperationResult.Fail("Нельзя вложить подборку: образуется цикл.");

            _db.CollectionItems.Add(new CollectionItem { CollectionID = parentId, ChildCollectionID = childId });
            await _db.SaveChangesAsync();
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Ошибка при вложении подборки {Child} в {Parent}", childId, parentId);
            return OperationResult.Fail("Не удалось вложить подборку. Подробности в журнале.");
        }
    }

    /// <summary>Убирает элемент (фильм или вложенную подборку) из подборки.</summary>
    public async Task<OperationResult> RemoveItemAsync(int userId, int itemId)
    {
        try
        {
            var item = await _db.CollectionItems
                .FirstOrDefaultAsync(i => i.ItemID == itemId && i.Collection.UserID == userId);
            if (item is null)
                return OperationResult.Fail("Элемент не найден.");

            _db.CollectionItems.Remove(item);
            await _db.SaveChangesAsync();
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Ошибка при удалении элемента {ItemId}", itemId);
            return OperationResult.Fail("Не удалось удалить элемент. Подробности в журнале.");
        }
    }

    public async Task<List<Collection>> GetUserCollectionsAsync(int userId)
    {
        var list = await _db.Collections.AsNoTracking().Where(c => c.UserID == userId).ToListAsync();
        return list.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>Строит дерево подборки (Компоновщик): листья-фильмы и вложенные подборки. Null, если подборки нет.</summary>
    public async Task<CollectionComposite?> BuildTreeAsync(int userId, int collectionId)
    {
        var root = await FindOwnedAsync(userId, collectionId);
        if (root is null)
            return null;

        var collections = await _db.Collections.AsNoTracking()
            .Where(c => c.UserID == userId)
            .ToDictionaryAsync(c => c.CollectionID);

        var items = await _db.CollectionItems.AsNoTracking()
            .Include(i => i.Movie)
            .Where(i => i.Collection.UserID == userId)
            .OrderBy(i => i.ItemID)
            .ToListAsync();
        var byParent = items.ToLookup(i => i.CollectionID);

        CollectionComposite Build(Collection c, HashSet<int> path)
        {
            var node = new CollectionComposite(c.CollectionID, c.Name, c.Description);
            path.Add(c.CollectionID);

            foreach (var item in byParent[c.CollectionID])
            {
                if (item.Movie is not null)
                    node.Add(new MovieLeaf(item.Movie));
                // path защищает от зависания, даже если в базе окажется цикл
                else if (item.ChildCollectionID is int childId && !path.Contains(childId)
                         && collections.TryGetValue(childId, out var child))
                    node.Add(Build(child, path));
            }

            path.Remove(c.CollectionID);
            return node;
        }

        return Build(collections[root.CollectionID], new HashSet<int>());
    }

    /// <summary>Образуется ли цикл, если вложить childId в parentId (обход вложенных подборок child в глубину).</summary>
    private async Task<bool> WouldCreateCycleAsync(int userId, int parentId, int childId)
    {
        var links = await _db.CollectionItems.AsNoTracking()
            .Where(i => i.ChildCollectionID != null && i.Collection.UserID == userId)
            .Select(i => new { i.CollectionID, i.ChildCollectionID })
            .ToListAsync();

        var children = links
            .GroupBy(l => l.CollectionID)
            .ToDictionary(g => g.Key, g => g.Select(l => l.ChildCollectionID!.Value).ToList());

        var visited = new HashSet<int>();
        var stack = new Stack<int>();
        stack.Push(childId);

        while (stack.Count > 0)
        {
            int current = stack.Pop();
            if (current == parentId)
                return true;
            if (!visited.Add(current))
                continue;
            if (children.TryGetValue(current, out var list))
                foreach (int next in list)
                    stack.Push(next);
        }
        return false;
    }

    private Task<Collection?> FindOwnedAsync(int userId, int collectionId) =>
        _db.Collections.FirstOrDefaultAsync(c => c.CollectionID == collectionId && c.UserID == userId);

    private static string? Validate(string name, string? description, out string cleanName, out string? cleanDesc)
    {
        cleanName = (name ?? string.Empty).Trim();
        cleanDesc = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        if (cleanName.Length == 0)
            return "Введите название подборки.";
        if (cleanName.Length > MaxNameLength)
            return $"Название не должно быть длиннее {MaxNameLength} символов.";
        if (cleanDesc is not null && cleanDesc.Length > MaxDescriptionLength)
            return $"Описание не должно быть длиннее {MaxDescriptionLength} символов.";
        return null;
    }
}