using FilmApp.Data;
using FilmApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FilmApp.Services.Recommendations;

/// <summary>
/// Фасад рекомендаций: единая точка входа, которая скрывает загрузку данных,
/// построение профиля вкусов, оценку фильмов и сортировку.
/// </summary>
public class RecommendationService
{
    private readonly AppDbContext _db;
    private readonly ILogger<RecommendationService> _log;
    private readonly TasteProfileBuilder _profileBuilder = new();
    private readonly MovieScorer _scorer = new();

    public RecommendationService(AppDbContext db, ILogger<RecommendationService> log)
    {
        _db = db;
        _log = log;
    }

    /// <summary>
    /// Фильмы, которые пользователь ещё не смотрел, по убыванию оценки. Фильмы с неположительной оценкой
    /// не рекомендуются. Если у пользователя нет оценок, список пуст.
    /// </summary>
    public async Task<List<RecommendedMovie>> GetRecommendationsAsync(int userId, int take = 20)
    {
        try
        {
            var rated = await _db.Viewings.AsNoTracking()
                .Where(v => v.UserID == userId && v.Status == ViewingStatus.Watched && v.Rating != null)
                .Include(v => v.Movie).ThenInclude(m => m.MovieGenres)
                .Include(v => v.Movie).ThenInclude(m => m.MoviePersons)
                .ToListAsync();

            if (rated.Count == 0)
                return new List<RecommendedMovie>();

            var profile = _profileBuilder.Build(rated);

            var watchedIds = await _db.Viewings
                .Where(v => v.UserID == userId && v.Status == ViewingStatus.Watched)
                .Select(v => v.MovieID)
                .ToListAsync();

            var candidates = await _db.Movies.AsNoTracking()
                .Include(m => m.MovieGenres)
                .Include(m => m.MoviePersons)
                .Where(m => !watchedIds.Contains(m.MovieID))
                .ToListAsync();

            return candidates
                .Select(m => new RecommendedMovie(m, _scorer.Score(m, profile)))
                .Where(r => r.Score > 0)
                .OrderByDescending(r => r.Score)
                .ThenBy(r => r.Movie.Title, StringComparer.CurrentCultureIgnoreCase)
                .Take(take)
                .ToList();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Ошибка при расчёте рекомендаций для пользователя {UserId}", userId);
            return new List<RecommendedMovie>();
        }
    }
}