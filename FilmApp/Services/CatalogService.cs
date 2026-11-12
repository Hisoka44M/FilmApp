using FilmApp.Data;
using FilmApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FilmApp.Services;

/// <summary>Результат создания фильма: сам фильм либо сообщение об ошибке.</summary>
public record MovieResult(bool Success, Movie? Movie, string? Error)
{
    public static MovieResult Ok(Movie movie) => new(true, movie, null);
    public static MovieResult Fail(string error) => new(false, null, error);
}

/// <summary>Каталог фильмов: ручное добавление, поиск, чтение данных о фильме.</summary>
public class CatalogService
{
    private const int MaxTitleLength = 300;
    private const int MaxDescriptionLength = 4000;
    private const int MaxGenreLength = 100;
    private const int FirstFilmYear = 1888;
    private const int MaxDuration = 1000;

    private readonly AppDbContext _db;
    private readonly ILogger<CatalogService> _log;

    public CatalogService(AppDbContext db, ILogger<CatalogService> log)
    {
        _db = db;
        _log = log;
    }

    /// <summary>Добавляет фильм вручную (без TMDB), с проверкой всех полей и без дублей.</summary>
    public async Task<MovieResult> AddMovieManuallyAsync(
        string title, int? year, int? duration, string? description, IEnumerable<string>? genres)
    {
        title = (title ?? string.Empty).Trim();
        if (title.Length == 0)
            return MovieResult.Fail("Введите название фильма.");
        if (title.Length > MaxTitleLength)
            return MovieResult.Fail($"Название не должно быть длиннее {MaxTitleLength} символов.");

        if (year is not null && (year < FirstFilmYear || year > DateTime.Now.Year))
            return MovieResult.Fail($"Год выпуска должен быть от {FirstFilmYear} до {DateTime.Now.Year}.");

        if (duration is not null && (duration < 1 || duration > MaxDuration))
            return MovieResult.Fail($"Длительность должна быть от 1 до {MaxDuration} минут.");

        string? desc = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (desc is not null && desc.Length > MaxDescriptionLength)
            return MovieResult.Fail($"Описание не должно быть длиннее {MaxDescriptionLength} символов.");

        var genreNames = (genres ?? Enumerable.Empty<string>())
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Select(g => g.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (genreNames.Any(g => g.Length > MaxGenreLength))
            return MovieResult.Fail($"Название жанра не должно быть длиннее {MaxGenreLength} символов.");

        try
        {
            // Дубль: то же название (без учёта регистра) и тот же год
            var sameYearTitles = await _db.Movies
                .Where(m => m.ReleaseYear == year)
                .Select(m => m.Title)
                .ToListAsync();
            if (sameYearTitles.Any(t => string.Equals(t, title, StringComparison.OrdinalIgnoreCase)))
                return MovieResult.Fail("Такой фильм уже есть в каталоге.");

            var genreMap = new Dictionary<string, Genre>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in await _db.Genres.ToListAsync())
                genreMap.TryAdd(g.GenreName, g);

            var movie = new Movie
            {
                Title = title,
                ReleaseYear = year,
                Duration = duration,
                Description = desc
            };

            foreach (string name in genreNames)
            {
                if (!genreMap.TryGetValue(name, out var genre))
                {
                    genre = new Genre { GenreName = name };
                    genreMap[name] = genre;
                }
                movie.MovieGenres.Add(new MovieGenre { Genre = genre });
            }

            _db.Movies.Add(movie);
            await _db.SaveChangesAsync();

            _log.LogInformation("Фильм добавлен вручную: {Title}", title);
            return MovieResult.Ok(movie);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Ошибка при добавлении фильма {Title}", title);
            return MovieResult.Fail("Не удалось сохранить фильм. Подробности в журнале.");
        }
    }

    /// <summary>
    /// Ищет фильмы по части названия без учёта регистра. Пустой запрос возвращает первые фильмы по алфавиту.
    /// </summary>
    public async Task<List<Movie>> SearchAsync(string? query, int take = 50)
    {
        string q = (query ?? string.Empty).Trim();

        // Сравнение в памяти: SQLite не сравнивает кириллицу без учёта регистра
        var titles = await _db.Movies.AsNoTracking()
            .Select(m => new { m.MovieID, m.Title })
            .ToListAsync();

        var ids = titles
            .Where(t => q.Length == 0 || t.Title.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Title, StringComparer.CurrentCultureIgnoreCase)
            .Take(take)
            .Select(t => t.MovieID)
            .ToList();

        var movies = await _db.Movies.AsNoTracking()
            .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
            .Where(m => ids.Contains(m.MovieID))
            .ToListAsync();

        return ids.Select(id => movies.First(m => m.MovieID == id)).ToList();
    }

    /// <summary>Фильм со всеми жанрами и персонами или null.</summary>
    public Task<Movie?> GetMovieAsync(int movieId) =>
        _db.Movies.AsNoTracking()
            .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
            .Include(m => m.MoviePersons).ThenInclude(mp => mp.Person)
            .FirstOrDefaultAsync(m => m.MovieID == movieId);

    /// <summary>Все жанры по алфавиту.</summary>
    public async Task<List<Genre>> GetGenresAsync()
    {
        var genres = await _db.Genres.AsNoTracking().ToListAsync();
        return genres.OrderBy(g => g.GenreName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}