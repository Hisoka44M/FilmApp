using FilmApp.Models;
using Microsoft.Extensions.Configuration;

namespace FilmApp.Services.Tmdb;

/// <summary>Фильм, преобразованный из формата TMDB в модель приложения.</summary>
public record ImportedMovie(
    Movie Movie,
    List<string> Genres,
    List<string> Directors,
    List<string> Actors);

/// <summary>Целевой интерфейс паттерна «Адаптер»: что ожидает от источника данных само приложение.</summary>
public interface IMovieAdapter
{
    ImportedMovie Adapt(TmdbMovieDetails source);
}

/// <summary>Адаптер данных TMDB (DTO) к сущностям приложения.</summary>
public class TmdbMovieAdapter : IMovieAdapter
{
    private const int MaxActors = 5;
    private readonly string _imageBaseUrl;

    public TmdbMovieAdapter(IConfiguration config)
    {
        _imageBaseUrl = (config["Tmdb:ImageBaseUrl"] ?? "https://image.tmdb.org/t/p/w342").TrimEnd('/');
    }

    public ImportedMovie Adapt(TmdbMovieDetails source)
    {
        ArgumentNullException.ThrowIfNull(source);

        string title = source.Title?.Trim() ?? string.Empty;
        if (title.Length == 0)
            throw new TmdbException("У фильма из TMDB нет названия, импорт невозможен.");

        var movie = new Movie
        {
            TmdbID = source.Id,
            Title = title,
            ReleaseYear = ParseYear(source.ReleaseDate),
            Duration = source.Runtime is > 0 ? source.Runtime : null,
            Description = string.IsNullOrWhiteSpace(source.Overview) ? null : source.Overview.Trim(),
            PosterUrl = string.IsNullOrWhiteSpace(source.PosterPath)
                ? null
                : _imageBaseUrl + source.PosterPath
        };

        var genres = Names(source.Genres.Select(g => g.Name));

        var directors = Names(source.Credits?.Crew
            .Where(c => c.Job == "Director")
            .Select(c => c.Name) ?? Enumerable.Empty<string?>());

        var actors = Names(source.Credits?.Cast
            .OrderBy(c => c.Order)
            .Select(c => c.Name) ?? Enumerable.Empty<string?>())
            .Take(MaxActors)
            .ToList();

        return new ImportedMovie(movie, genres, directors, actors);
    }

    /// <summary>Год из даты формата «2010-07-16». Пустая или неверная дата даёт null.</summary>
    private static int? ParseYear(string? date)
    {
        if (string.IsNullOrWhiteSpace(date) || date.Length < 4)
            return null;
        return int.TryParse(date.AsSpan(0, 4), out int year) && year > 1800 ? year : null;
    }

    /// <summary>Непустые имена без повторов (без учёта регистра).</summary>
    private static List<string> Names(IEnumerable<string?> names) =>
        names.Where(n => !string.IsNullOrWhiteSpace(n))
             .Select(n => n!.Trim())
             .Distinct(StringComparer.OrdinalIgnoreCase)
             .ToList();
}