using FilmApp.Data;
using FilmApp.Models;
using FilmApp.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FilmApp.Tests;

public class ViewingServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly ViewingService _service;
    private readonly int _user1, _user2, _user3, _movie;

    public ViewingServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        var u1 = new User { Username = "user1", PasswordHash = "x" };
        var u2 = new User { Username = "user2", PasswordHash = "x" };
        var u3 = new User { Username = "user3", PasswordHash = "x" };
        var movie = new Movie { Title = "Тестовый фильм" };
        _db.Users.AddRange(u1, u2, u3);
        _db.Movies.Add(movie);
        _db.SaveChanges();
        (_user1, _user2, _user3, _movie) = (u1.UserID, u2.UserID, u3.UserID, movie.MovieID);

        _service = new ViewingService(_db, NullLogger<ViewingService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task MarkWatched_ValidData_SavesRatingAndReview()
    {
        var result = await _service.MarkWatchedAsync(_user1, _movie, 9, "  Отличный фильм ", null);

        Assert.True(result.Success);
        var viewing = await _service.GetViewingAsync(_user1, _movie);
        Assert.NotNull(viewing);
        Assert.Equal(ViewingStatus.Watched, viewing!.Status);
        Assert.Equal(9, viewing.Rating);
        Assert.Equal("Отличный фильм", viewing.ReviewText);
        Assert.NotNull(viewing.ReviewDate);
        Assert.Equal(DateTime.Today, viewing.ViewDate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(-5)]
    public async Task MarkWatched_RatingOutOfRange_Fails(int rating)
    {
        var result = await _service.MarkWatchedAsync(_user1, _movie, rating, null, null);

        Assert.False(result.Success);
        Assert.Equal(0, _db.Viewings.Count());
    }

    [Fact]
    public async Task MarkWatched_FutureDate_Fails()
    {
        var result = await _service.MarkWatchedAsync(_user1, _movie, 5, null, DateTime.Today.AddDays(1));

        Assert.False(result.Success);
    }

    [Fact]
    public async Task MarkWatched_UnknownMovie_Fails()
    {
        var result = await _service.MarkWatchedAsync(_user1, 9999, 5, null, null);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task MarkWatched_Twice_UpdatesSameRecord()
    {
        await _service.MarkWatchedAsync(_user1, _movie, 5, null, null);
        await _service.MarkWatchedAsync(_user1, _movie, 8, "Пересмотрел", null);

        Assert.Equal(1, _db.Viewings.Count());
        var viewing = await _service.GetViewingAsync(_user1, _movie);
        Assert.Equal(8, viewing!.Rating);
    }

    [Fact]
    public async Task MarkPlanned_AfterWatched_ClearsRatingAndReview()
    {
        await _service.MarkWatchedAsync(_user1, _movie, 7, "Текст", null);

        var result = await _service.MarkPlannedAsync(_user1, _movie);

        Assert.True(result.Success);
        var viewing = await _service.GetViewingAsync(_user1, _movie);
        Assert.Equal(ViewingStatus.Planned, viewing!.Status);
        Assert.Null(viewing.Rating);
        Assert.Null(viewing.ReviewText);
        Assert.Null(viewing.ViewDate);
    }

    [Fact]
    public async Task AverageRating_CountsOnlyWatchedWithRating()
    {
        await _service.MarkWatchedAsync(_user1, _movie, 8, null, null);
        await _service.MarkWatchedAsync(_user2, _movie, 6, null, null);
        await _service.MarkPlannedAsync(_user3, _movie);

        double? average = await _service.GetAverageRatingAsync(_movie);

        Assert.Equal(7.0, average!.Value);
    }

    [Fact]
    public async Task AverageRating_NoRatings_ReturnsNull()
    {
        await _service.MarkPlannedAsync(_user1, _movie);

        double? average = await _service.GetAverageRatingAsync(_movie);

        Assert.Null(average);
    }
}