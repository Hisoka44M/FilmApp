using FilmApp.Data;
using FilmApp.Models;
using FilmApp.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FilmApp.Tests;

public class CollectionServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly CollectionService _service;
    private readonly int _user1, _user2, _movie1, _movie2;

    public CollectionServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        var u1 = new User { Username = "user1", PasswordHash = "x" };
        var u2 = new User { Username = "user2", PasswordHash = "x" };
        var m1 = new Movie { Title = "Фильм 1" };
        var m2 = new Movie { Title = "Фильм 2" };
        _db.Users.AddRange(u1, u2);
        _db.Movies.AddRange(m1, m2);
        _db.SaveChanges();
        (_user1, _user2, _movie1, _movie2) = (u1.UserID, u2.UserID, m1.MovieID, m2.MovieID);

        _service = new CollectionService(_db, NullLogger<CollectionService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<int> NewCollection(int userId, string name)
    {
        var result = await _service.CreateAsync(userId, name, null);
        Assert.True(result.Success);
        return result.Collection!.CollectionID;
    }

    [Fact]
    public async Task Create_ValidName_Saves()
    {
        var result = await _service.CreateAsync(_user1, "  Любимое ", "Описание");

        Assert.True(result.Success);
        Assert.Equal("Любимое", result.Collection!.Name);
        Assert.Equal(1, _db.Collections.Count());
    }

    [Fact]
    public async Task Create_EmptyName_Fails()
    {
        var result = await _service.CreateAsync(_user1, "  ", null);

        Assert.False(result.Success);
        Assert.Equal(0, _db.Collections.Count());
    }

    [Fact]
    public async Task AddMovie_Twice_Fails()
    {
        int a = await NewCollection(_user1, "A");

        Assert.True((await _service.AddMovieAsync(_user1, a, _movie1)).Success);
        Assert.False((await _service.AddMovieAsync(_user1, a, _movie1)).Success);
        Assert.Equal(1, _db.CollectionItems.Count());
    }

    [Fact]
    public async Task AddChild_ToItself_Fails()
    {
        int a = await NewCollection(_user1, "A");

        Assert.False((await _service.AddChildCollectionAsync(_user1, a, a)).Success);
    }

    [Fact]
    public async Task AddChild_DirectCycle_Fails()
    {
        int a = await NewCollection(_user1, "A");
        int b = await NewCollection(_user1, "B");

        Assert.True((await _service.AddChildCollectionAsync(_user1, a, b)).Success);
        Assert.False((await _service.AddChildCollectionAsync(_user1, b, a)).Success);
    }

    [Fact]
    public async Task AddChild_IndirectCycle_Fails()
    {
        int a = await NewCollection(_user1, "A");
        int b = await NewCollection(_user1, "B");
        int c = await NewCollection(_user1, "C");

        Assert.True((await _service.AddChildCollectionAsync(_user1, a, b)).Success);
        Assert.True((await _service.AddChildCollectionAsync(_user1, b, c)).Success);
        Assert.False((await _service.AddChildCollectionAsync(_user1, c, a)).Success);
    }

    [Fact]
    public async Task AddChild_OtherUsersCollection_Fails()
    {
        int mine = await NewCollection(_user1, "Моя");
        int foreign = await NewCollection(_user2, "Чужая");

        Assert.False((await _service.AddChildCollectionAsync(_user1, mine, foreign)).Success);
    }

    [Fact]
    public async Task Tree_CountsDistinctMoviesAcrossNesting()
    {
        int a = await NewCollection(_user1, "A");
        int b = await NewCollection(_user1, "B");
        await _service.AddMovieAsync(_user1, a, _movie1);
        await _service.AddChildCollectionAsync(_user1, a, b);
        await _service.AddMovieAsync(_user1, b, _movie1);
        await _service.AddMovieAsync(_user1, b, _movie2);

        var tree = await _service.BuildTreeAsync(_user1, a);

        Assert.NotNull(tree);
        Assert.Equal(2, tree!.Children.Count);   // фильм и вложенная подборка
        Assert.Equal(2, tree.CountMovies());     // Фильм 1 в двух местах, но считается один раз
    }

    [Fact]
    public async Task Delete_Collection_RemovesLinksButKeepsMovies()
    {
        int a = await NewCollection(_user1, "A");
        int b = await NewCollection(_user1, "B");
        await _service.AddChildCollectionAsync(_user1, a, b);
        await _service.AddMovieAsync(_user1, b, _movie1);

        var result = await _service.DeleteAsync(_user1, b);

        Assert.True(result.Success);
        Assert.Equal(1, _db.Collections.Count());     // осталась A
        Assert.Equal(0, _db.CollectionItems.Count()); // исчезли элементы B и ссылка на B из A
        Assert.Equal(2, _db.Movies.Count());          // фильмы не удаляются
    }

    [Fact]
    public async Task Delete_OtherUsersCollection_Fails()
    {
        int foreign = await NewCollection(_user2, "Чужая");

        var result = await _service.DeleteAsync(_user1, foreign);

        Assert.False(result.Success);
        Assert.Equal(1, _db.Collections.Count());
    }
}