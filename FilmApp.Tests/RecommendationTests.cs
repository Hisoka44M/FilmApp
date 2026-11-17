using FilmApp.Data;
using FilmApp.Models;
using FilmApp.Services.Recommendations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FilmApp.Tests;

public class RecommendationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly RecommendationService _service;
    private readonly int _userId;

    public RecommendationTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        var user = new User { Username = "user1", PasswordHash = "x" };
        _db.Users.Add(user);
        _db.SaveChanges();
        _userId = user.UserID;

        _service = new RecommendationService(_db, NullLogger<RecommendationService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private Movie AddMovie(string title, Genre[] genres, Person? director = null)
    {
        var movie = new Movie { Title = title };
        foreach (var genre in genres)
            movie.MovieGenres.Add(new MovieGenre { Genre = genre });
        if (director is not null)
            movie.MoviePersons.Add(new MoviePerson { Person = director, Role = PersonRole.Director });
        _db.Movies.Add(movie);
        _db.SaveChanges();
        return movie;
    }

    private void Watch(Movie movie, int rating)
    {
        _db.Viewings.Add(new Viewing
        {
            UserID = _userId,
            MovieID = movie.MovieID,
            Status = ViewingStatus.Watched,
            ViewDate = DateTime.Today,
            Rating = rating
        });
        _db.SaveChanges();
    }

    [Fact]
    public void ProfileBuilder_WeightIsRatingMinusFive()
    {
        var loved = new Viewing
        {
            Rating = 9,
            Movie = new Movie
            {
                MovieGenres = { new MovieGenre { GenreID = 1 } },
                MoviePersons = { new MoviePerson { PersonID = 10, Role = PersonRole.Director } }
            }
        };
        var disliked = new Viewing
        {
            Rating = 2,
            Movie = new Movie { MovieGenres = { new MovieGenre { GenreID = 2 } } }
        };

        var profile = new TasteProfileBuilder().Build(new[] { loved, disliked });

        Assert.Equal(4, profile.GenreWeights[1]);
        Assert.Equal(-3, profile.GenreWeights[2]);
        Assert.Equal(4, profile.DirectorWeights[10]);
    }

    [Fact]
    public void Scorer_SumsGenreAndDirectorWeights()
    {
        var profile = new TasteProfile();
        profile.GenreWeights[1] = 4;
        profile.GenreWeights[2] = -3;
        profile.DirectorWeights[10] = 2;
        var movie = new Movie
        {
            MovieGenres = { new MovieGenre { GenreID = 1 }, new MovieGenre { GenreID = 2 } },
            MoviePersons = { new MoviePerson { PersonID = 10, Role = PersonRole.Director } }
        };

        double score = new MovieScorer().Score(movie, profile);

        Assert.Equal(3, score); // 4 − 3 + 2
    }

    [Fact]
    public async Task Recommend_NoRatings_ReturnsEmpty()
    {
        var g = new Genre { GenreName = "Драма" };
        AddMovie("Фильм", new[] { g });

        var result = await _service.GetRecommendationsAsync(_userId);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Recommend_RanksByTasteAndSkipsWatchedAndDisliked()
    {
        var loved = new Genre { GenreName = "Фантастика" };
        var disliked = new Genre { GenreName = "Ужасы" };

        var w1 = AddMovie("Просмотренный любимый", new[] { loved });
        var w2 = AddMovie("Просмотренный нелюбимый", new[] { disliked });
        Watch(w1, 10);   // фантастика +5
        Watch(w2, 1);    // ужасы −4

        AddMovie("Кандидат А", new[] { loved });             // +5
        AddMovie("Кандидат Б", new[] { disliked });          // −4, не рекомендуется
        AddMovie("Кандидат В", new[] { loved, disliked });   // +1

        var result = await _service.GetRecommendationsAsync(_userId);

        Assert.Equal(new[] { "Кандидат А", "Кандидат В" }, result.Select(r => r.Movie.Title).ToArray());
    }

    [Fact]
    public async Task Recommend_FavoriteDirectorCountsWithoutSharedGenre()
    {
        var director = new Person { FullName = "Режиссёр" };
        var genreA = new Genre { GenreName = "Жанр А" };
        var genreB = new Genre { GenreName = "Жанр Б" };

        var seen = AddMovie("Просмотренный", new[] { genreA }, director);
        Watch(seen, 5);  // нейтральная оценка, вес 0

        // Оценка 5 даёт вес 0, поэтому добавим фильм с высокой оценкой того же режиссёра
        var seen2 = AddMovie("Просмотренный второй", new[] { genreA }, director);
        Watch(seen2, 9); // режиссёр +4, жанр А +4

        AddMovie("Тот же режиссёр, другой жанр", new[] { genreB }, director);

        var result = await _service.GetRecommendationsAsync(_userId);

        Assert.Single(result);
        Assert.Equal("Тот же режиссёр, другой жанр", result[0].Movie.Title);
        Assert.Equal(4, result[0].Score);
    }
}