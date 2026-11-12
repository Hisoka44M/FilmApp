using System.Net;
using System.Text;
using FilmApp.Data;
using FilmApp.Services;
using FilmApp.Services.Tmdb;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FilmApp.Tests;

public class MovieImportServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly MovieImportService _service;

    public MovieImportServiceTests()
    {
        // База в памяти живёт, пока открыто соединение
        _connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tmdb:ApiKey"] = "test",
                ["Tmdb:BaseUrl"] = "https://api.test/3/",
                ["Tmdb:ImageBaseUrl"] = "https://img.test/w342"
            })
            .Build();

        var client = new TmdbClient(new HttpClient(new FakeTmdbHandler()), config,
            NullLogger<TmdbClient>.Instance);
        _service = new MovieImportService(_db, client, new TmdbMovieAdapter(config),
            NullLogger<MovieImportService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Import_NewMovie_SavesMovieGenresAndPeople()
    {
        var result = await _service.ImportByTmdbIdAsync(1);

        Assert.True(result.Success);
        Assert.False(result.AlreadyExisted);
        Assert.Equal(1, _db.Movies.Count());
        Assert.Equal(2, _db.Genres.Count());
        Assert.Equal(2, _db.Persons.Count());
        Assert.Equal(2, _db.MoviePersons.Count());
    }

    [Fact]
    public async Task Import_SameTmdbIdTwice_DoesNotDuplicate()
    {
        await _service.ImportByTmdbIdAsync(1);
        var second = await _service.ImportByTmdbIdAsync(1);

        Assert.True(second.Success);
        Assert.True(second.AlreadyExisted);
        Assert.Equal(1, _db.Movies.Count());
    }

    [Fact]
    public async Task Import_TwoMoviesShareGenreAndDirector_ReusesRecords()
    {
        await _service.ImportByTmdbIdAsync(1);
        await _service.ImportByTmdbIdAsync(2);

        Assert.Equal(2, _db.Movies.Count());
        Assert.Equal(2, _db.Genres.Count());   // «Фантастика» и «Боевик», без повторов
        Assert.Equal(2, _db.Persons.Count());  // режиссёр общий, актёр один
    }

    [Fact]
    public async Task Import_UnknownMovie_ReturnsFail()
    {
        var result = await _service.ImportByTmdbIdAsync(999);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Equal(0, _db.Movies.Count());
    }

    /// <summary>Поддельный TMDB: два известных фильма, остальные не найдены.</summary>
    private class FakeTmdbHandler : HttpMessageHandler
    {
        private static readonly Dictionary<string, string> Movies = new()
        {
            ["1"] = """
            {"id":1,"title":"Фильм один","release_date":"2010-07-16","runtime":148,"overview":"О",
             "poster_path":"/a.jpg",
             "genres":[{"id":1,"name":"Фантастика"},{"id":2,"name":"Боевик"}],
             "credits":{"cast":[{"name":"Актёр Первый","order":0}],
                        "crew":[{"name":"Режиссёр Общий","job":"Director"}]}}
            """,
            ["2"] = """
            {"id":2,"title":"Фильм два","release_date":"2014-11-05","runtime":169,"overview":"О",
             "poster_path":null,
             "genres":[{"id":1,"name":"фантастика"}],
             "credits":{"cast":[],"crew":[{"name":"Режиссёр Общий","job":"Director"}]}}
            """
        };

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string id = request.RequestUri!.AbsolutePath.Split('/').Last();
            if (!Movies.TryGetValue(id, out string? json))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}