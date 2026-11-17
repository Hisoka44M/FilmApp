using FilmApp.Models;

namespace FilmApp.Services.Recommendations;

/// <summary>Вкусы пользователя: веса жанров и режиссёров (по идентификаторам).</summary>
public class TasteProfile
{
    public Dictionary<int, double> GenreWeights { get; } = new();
    public Dictionary<int, double> DirectorWeights { get; } = new();
}

/// <summary>Строит профиль вкусов по оценкам: вес = оценка − 5, оценка 9 даёт +4, оценка 2 даёт −3.</summary>
public class TasteProfileBuilder
{
    /// <summary>Оценка, которая считается нейтральной.</summary>
    public const int NeutralRating = 5;

    /// <summary>Фильмы в просмотрах должны быть загружены вместе с жанрами и персонами.</summary>
    public TasteProfile Build(IEnumerable<Viewing> ratedViewings)
    {
        var profile = new TasteProfile();

        foreach (var viewing in ratedViewings)
        {
            if (viewing.Rating is not int rating)
                continue;

            double weight = rating - NeutralRating;

            foreach (int genreId in viewing.Movie.MovieGenres.Select(g => g.GenreID).Distinct())
                Add(profile.GenreWeights, genreId, weight);

            foreach (int personId in viewing.Movie.MoviePersons
                         .Where(p => p.Role == PersonRole.Director)
                         .Select(p => p.PersonID).Distinct())
                Add(profile.DirectorWeights, personId, weight);
        }

        return profile;
    }

    private static void Add(Dictionary<int, double> weights, int id, double value)
    {
        weights.TryGetValue(id, out double current);
        weights[id] = current + value;
    }
}

/// <summary>Оценивает фильм по профилю вкусов: сумма весов его жанров и режиссёров.</summary>
public class MovieScorer
{
    /// <summary>Фильм должен быть загружен вместе с жанрами и персонами.</summary>
    public double Score(Movie movie, TasteProfile profile)
    {
        double score = 0;

        foreach (int genreId in movie.MovieGenres.Select(g => g.GenreID).Distinct())
            if (profile.GenreWeights.TryGetValue(genreId, out double w))
                score += w;

        foreach (int personId in movie.MoviePersons
                     .Where(p => p.Role == PersonRole.Director)
                     .Select(p => p.PersonID).Distinct())
            if (profile.DirectorWeights.TryGetValue(personId, out double w))
                score += w;

        return score;
    }
}

/// <summary>Рекомендованный фильм и его оценка по профилю вкусов.</summary>
public record RecommendedMovie(Movie Movie, double Score);