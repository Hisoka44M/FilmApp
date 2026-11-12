using FilmApp.Services.Tmdb;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FilmApp.Tests;

public class TmdbMovieAdapterTests
{
    private static TmdbMovieAdapter CreateAdapter()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tmdb:ImageBaseUrl"] = "https://img.test/w342"
            })
            .Build();
        return new TmdbMovieAdapter(config);
    }

    [Fact]
    public void Adapt_FullData_MapsFields()
    {
        var dto = new TmdbMovieDetails
        {
            Id = 27205,
            Title = "  Начало ",
            ReleaseDate = "2010-07-16",
            Runtime = 148,
            Overview = "Описание",
            PosterPath = "/abc.jpg",
            Genres = { new TmdbGenre { Name = "Фантастика" }, new TmdbGenre { Name = "фантастика" } }
        };

        var result = CreateAdapter().Adapt(dto);

        Assert.Equal(27205, result.Movie.TmdbID);
        Assert.Equal("Начало", result.Movie.Title);
        Assert.Equal(2010, result.Movie.ReleaseYear);
        Assert.Equal(148, result.Movie.Duration);
        Assert.Equal("https://img.test/w342/abc.jpg", result.Movie.PosterUrl);
        Assert.Single(result.Genres);
    }

    [Fact]
    public void Adapt_NoPosterAndBadDate_GivesNulls()
    {
        var dto = new TmdbMovieDetails { Id = 1, Title = "Фильм", ReleaseDate = "", PosterPath = null, Runtime = 0 };

        var result = CreateAdapter().Adapt(dto);

        Assert.Null(result.Movie.PosterUrl);
        Assert.Null(result.Movie.ReleaseYear);
        Assert.Null(result.Movie.Duration);
    }

    [Fact]
    public void Adapt_TakesOnlyFirstFiveActorsByOrder()
    {
        var cast = new List<TmdbCastMember>();
        for (int i = 8; i >= 1; i--)
            cast.Add(new TmdbCastMember { Name = "Актёр " + i, Order = i });
        var dto = new TmdbMovieDetails { Id = 1, Title = "Фильм", Credits = new TmdbCredits { Cast = cast } };

        var result = CreateAdapter().Adapt(dto);

        Assert.Equal(5, result.Actors.Count);
        Assert.Equal("Актёр 1", result.Actors[0]);
        Assert.Equal("Актёр 5", result.Actors[4]);
    }

    [Fact]
    public void Adapt_DirectorsOnlyFromCrewWithDirectorJob()
    {
        var dto = new TmdbMovieDetails
        {
            Id = 1,
            Title = "Фильм",
            Credits = new TmdbCredits
            {
                Crew =
                {
                    new TmdbCrewMember { Name = "Режиссёр", Job = "Director" },
                    new TmdbCrewMember { Name = "Композитор", Job = "Composer" }
                }
            }
        };

        var result = CreateAdapter().Adapt(dto);

        Assert.Equal(new[] { "Режиссёр" }, result.Directors);
    }

    [Fact]
    public void Adapt_EmptyTitle_ThrowsTmdbException()
    {
        var dto = new TmdbMovieDetails { Id = 1, Title = "   " };
        Assert.Throws<TmdbException>(() => CreateAdapter().Adapt(dto));
    }
}