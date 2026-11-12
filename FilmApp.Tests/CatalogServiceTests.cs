using FilmApp.Data;
using FilmApp.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FilmApp.Tests;

public class CatalogServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly CatalogService _service;

    public CatalogServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        _service = new CatalogService(_db, NullLogger<CatalogService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task AddManually_ValidData_SavesMovieWithGenres()
    {
        var result = await _service.AddMovieManuallyAsync(
            "  Начало ", 2010, 148, "Описание", new[] { "Фантастика", "Боевик" });

        Assert.True(result.Success);
        Assert.Equal("Начало", result.Movie!.Title);
        Assert.Null(result.Movie.TmdbID);
        Assert.Equal(1, _db.Movies.Count());
        Assert.Equal(2, _db.Genres.Count());
    }

    [Fact]
    public async Task AddManually_EmptyTitle_Fails()
    {
        var result = await _service.AddMovieManuallyAsync("   ", 2010, null, null, null);

        Assert.False(result.Success);
        Assert.Equal(0, _db.Movies.Count());
    }

    [Fact]
    public async Task AddManually_FutureYear_Fails()
    {
        var result = await _service.AddMovieManuallyAsync("Фильм", DateTime.Now.Year + 1, null, null, null);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task AddManually_Duplicate_Fails()
    {
        await _service.AddMovieManuallyAsync("Начало", 2010, null, null, null);

        var result = await _service.AddMovieManuallyAsync("НАЧАЛО", 2010, null, null, null);

        Assert.False(result.Success);
        Assert.Equal(1, _db.Movies.Count());
    }

    [Fact]
    public async Task AddManually_GenreInOtherCase_ReusesExistingGenre()
    {
        await _service.AddMovieManuallyAsync("Фильм один", 2000, null, null, new[] { "Драма" });
        await _service.AddMovieManuallyAsync("Фильм два", 2001, null, null, new[] { "драма" });

        Assert.Equal(2, _db.Movies.Count());
        Assert.Equal(1, _db.Genres.Count());
    }

    [Fact]
    public async Task Search_CyrillicIgnoreCase_FindsMovie()
    {
        await _service.AddMovieManuallyAsync("Начало", 2010, null, null, null);
        await _service.AddMovieManuallyAsync("Интерстеллар", 2014, null, null, null);

        var found = await _service.SearchAsync("НАЧ");

        Assert.Single(found);
        Assert.Equal("Начало", found[0].Title);
    }

    [Fact]
    public async Task Search_EmptyQuery_ReturnsAllSortedByTitle()
    {
        await _service.AddMovieManuallyAsync("Бета", 2001, null, null, null);
        await _service.AddMovieManuallyAsync("Альфа", 2000, null, null, null);

        var found = await _service.SearchAsync("");

        Assert.Equal(new[] { "Альфа", "Бета" }, found.Select(m => m.Title).ToArray());
    }
}