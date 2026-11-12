using FilmApp.Data;
using FilmApp.Models;
using FilmApp.Services.Tmdb;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FilmApp.Services;

/// <summary>Результат импорта фильма.</summary>
public record ImportResult(bool Success, bool AlreadyExisted, Movie? Movie, string? Error)
{
    public static ImportResult Added(Movie movie) => new(true, false, movie, null);
    public static ImportResult Existing(Movie movie) => new(true, true, movie, null);
    public static ImportResult Fail(string error) => new(false, false, null, error);
}

/// <summary>Импорт фильмов из TMDB в базу данных без создания дублей.</summary>
public class MovieImportService
{
    private readonly AppDbContext _db;
    private readonly TmdbClient _client;
    private readonly IMovieAdapter _adapter;
    private readonly ILogger<MovieImportService> _log;

    public MovieImportService(AppDbContext db, TmdbClient client, IMovieAdapter adapter,
        ILogger<MovieImportService> log)
    {
        _db = db;
        _client = client;
        _adapter = adapter;
        _log = log;
    }

    /// <summary>Добавляет фильм по идентификатору TMDB. Если он уже есть в базе, возвращает существующий.</summary>
    public async Task<ImportResult> ImportByTmdbIdAsync(int tmdbId, CancellationToken ct = default)
    {
        try
        {
            var existing = await _db.Movies.FirstOrDefaultAsync(m => m.TmdbID == tmdbId, ct);
            if (existing is not null)
                return ImportResult.Existing(existing);

            var details = await _client.GetMovieAsync(tmdbId, ct);
            if (details is null)
                return ImportResult.Fail("Фильм не найден в TMDB.");

            var imported = _adapter.Adapt(details);
            var movie = imported.Movie;

            await AttachGenresAsync(movie, imported.Genres, ct);
            await AttachPeopleAsync(movie, imported.Directors, imported.Actors, ct);

            _db.Movies.Add(movie);
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("Импортирован фильм {Title} (TMDB {TmdbId})", movie.Title, tmdbId);
            return ImportResult.Added(movie);
        }
        catch (TmdbException ex)
        {
            return ImportResult.Fail(ex.Message);
        }
        catch (OperationCanceledException)
        {
            return ImportResult.Fail("Импорт отменён.");
        }
        catch (DbUpdateException ex)
        {
            _log.LogError(ex, "Ошибка записи фильма TMDB {TmdbId} в базу", tmdbId);
            return ImportResult.Fail("Не удалось сохранить фильм в базу данных.");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Непредвиденная ошибка импорта фильма TMDB {TmdbId}", tmdbId);
            return ImportResult.Fail("Произошла непредвиденная ошибка. Подробности в журнале.");
        }
    }

    /// <summary>Привязывает жанры: существующие берутся из базы (без учёта регистра), новые создаются.</summary>
    private async Task AttachGenresAsync(Movie movie, List<string> names, CancellationToken ct)
    {
        // Жанров мало, поэтому загружаем все и сравниваем в памяти: SQLite не умеет сравнивать кириллицу без учёта регистра
        var map = new Dictionary<string, Genre>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in await _db.Genres.ToListAsync(ct))
            map.TryAdd(g.GenreName, g);

        foreach (string name in names)
        {
            if (!map.TryGetValue(name, out var genre))
            {
                genre = new Genre { GenreName = name };
                map[name] = genre;
            }
            movie.MovieGenres.Add(new MovieGenre { Genre = genre });
        }
    }

    /// <summary>Привязывает режиссёров и актёров. Один человек в разных ролях остаётся одной записью Person.</summary>
    private async Task AttachPeopleAsync(Movie movie, List<string> directors, List<string> actors,
        CancellationToken ct)
    {
        var allNames = directors.Concat(actors).Distinct().ToList();
        var found = await _db.Persons.Where(p => allNames.Contains(p.FullName)).ToListAsync(ct);

        var map = new Dictionary<string, Person>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in found)
            map.TryAdd(p.FullName, p);

        Person Resolve(string name)
        {
            if (!map.TryGetValue(name, out var person))
            {
                person = new Person { FullName = name };
                map[name] = person;
            }
            return person;
        }

        foreach (string name in directors)
            movie.MoviePersons.Add(new MoviePerson { Person = Resolve(name), Role = PersonRole.Director });
        foreach (string name in actors)
            movie.MoviePersons.Add(new MoviePerson { Person = Resolve(name), Role = PersonRole.Actor });
    }
}