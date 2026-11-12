using FilmApp.Data;
using FilmApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FilmApp.Services;

/// <summary>Просмотры: статус, оценка, отзыв и средний рейтинг фильма.</summary>
public class ViewingService
{
    private const int MinRating = 1;
    private const int MaxRating = 10;
    private const int MaxReviewLength = 2000;

    private readonly AppDbContext _db;
    private readonly ILogger<ViewingService> _log;

    public ViewingService(AppDbContext db, ILogger<ViewingService> log)
    {
        _db = db;
        _log = log;
    }

    /// <summary>
    /// Помечает фильм как запланированный. Если он был просмотрен, дата, оценка и отзыв удаляются.
    /// </summary>
    public async Task<OperationResult> MarkPlannedAsync(int userId, int movieId)
    {
        try
        {
            if (!await _db.Movies.AnyAsync(m => m.MovieID == movieId))
                return OperationResult.Fail("Фильм не найден.");

            var viewing = await FindAsync(userId, movieId);
            if (viewing is null)
            {
                _db.Viewings.Add(new Viewing
                {
                    UserID = userId,
                    MovieID = movieId,
                    Status = ViewingStatus.Planned
                });
            }
            else
            {
                viewing.Status = ViewingStatus.Planned;
                viewing.ViewDate = null;
                viewing.Rating = null;
                viewing.ReviewText = null;
                viewing.ReviewDate = null;
            }

            await _db.SaveChangesAsync();
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Ошибка при добавлении фильма {MovieId} в планы", movieId);
            return OperationResult.Fail("Не удалось сохранить изменения. Подробности в журнале.");
        }
    }

    /// <summary>
    /// Отмечает фильм просмотренным. Оценка (1–10) и отзыв необязательны, дата по умолчанию сегодняшняя.
    /// </summary>
    public async Task<OperationResult> MarkWatchedAsync(
        int userId, int movieId, int? rating, string? reviewText, DateTime? viewDate)
    {
        if (rating is < MinRating or > MaxRating)
            return OperationResult.Fail($"Оценка должна быть от {MinRating} до {MaxRating}.");

        string? review = string.IsNullOrWhiteSpace(reviewText) ? null : reviewText.Trim();
        if (review is not null && review.Length > MaxReviewLength)
            return OperationResult.Fail($"Отзыв не должен быть длиннее {MaxReviewLength} символов.");

        DateTime date = (viewDate ?? DateTime.Today).Date;
        if (date > DateTime.Today)
            return OperationResult.Fail("Дата просмотра не может быть в будущем.");

        try
        {
            if (!await _db.Movies.AnyAsync(m => m.MovieID == movieId))
                return OperationResult.Fail("Фильм не найден.");

            var viewing = await FindAsync(userId, movieId);
            if (viewing is null)
            {
                viewing = new Viewing { UserID = userId, MovieID = movieId };
                _db.Viewings.Add(viewing);
            }

            // Дата отзыва обновляется только если текст отзыва изменился
            DateTime? reviewDate = review is null
                ? null
                : (review == viewing.ReviewText ? viewing.ReviewDate : DateTime.Now);

            viewing.Status = ViewingStatus.Watched;
            viewing.ViewDate = date;
            viewing.Rating = rating;
            viewing.ReviewText = review;
            viewing.ReviewDate = reviewDate;

            await _db.SaveChangesAsync();
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Ошибка при сохранении просмотра фильма {MovieId}", movieId);
            return OperationResult.Fail("Не удалось сохранить изменения. Подробности в журнале.");
        }
    }

    /// <summary>Удаляет запись о просмотре (вместе с оценкой и отзывом).</summary>
    public async Task<OperationResult> RemoveAsync(int userId, int movieId)
    {
        try
        {
            var viewing = await FindAsync(userId, movieId);
            if (viewing is null)
                return OperationResult.Fail("Запись не найдена.");

            _db.Viewings.Remove(viewing);
            await _db.SaveChangesAsync();
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Ошибка при удалении просмотра фильма {MovieId}", movieId);
            return OperationResult.Fail("Не удалось удалить запись. Подробности в журнале.");
        }
    }

    /// <summary>Запись пользователя о фильме или null.</summary>
    public Task<Viewing?> GetViewingAsync(int userId, int movieId) => FindAsync(userId, movieId);

    /// <summary>Просмотры пользователя вместе с фильмами; можно отфильтровать по статусу.</summary>
    public async Task<List<Viewing>> GetUserViewingsAsync(int userId, ViewingStatus? status = null)
    {
        var query = _db.Viewings.Include(v => v.Movie).Where(v => v.UserID == userId);
        if (status is not null)
            query = query.Where(v => v.Status == status);

        return await query
            .OrderByDescending(v => v.ViewDate)
            .ThenBy(v => v.Movie.Title)
            .ToListAsync();
    }

    /// <summary>
    /// Средний рейтинг фильма: среднее по оценкам записей со статусом Watched. Null, если оценок нет.
    /// </summary>
    public Task<double?> GetAverageRatingAsync(int movieId) =>
        _db.Viewings
            .Where(v => v.MovieID == movieId && v.Status == ViewingStatus.Watched && v.Rating != null)
            .AverageAsync(v => (double?)v.Rating);

    private Task<Viewing?> FindAsync(int userId, int movieId) =>
        _db.Viewings.FirstOrDefaultAsync(v => v.UserID == userId && v.MovieID == movieId);
} 