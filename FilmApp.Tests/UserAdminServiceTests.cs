using FilmApp.Data;
using FilmApp.Models;
using FilmApp.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FilmApp.Tests;

public class UserAdminServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly UserAdminService _service;

    public UserAdminServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        _service = new UserAdminService(_db, NullLogger<UserAdminService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private int AddUser(string name, UserRole role)
    {
        var user = new User { Username = name, PasswordHash = "x", Role = role };
        _db.Users.Add(user);
        _db.SaveChanges();
        return user.UserID;
    }

    [Fact]
    public async Task Delete_LastAdmin_Fails()
    {
        int admin = AddUser("admin", UserRole.Admin);
        AddUser("user", UserRole.User);

        var result = await _service.DeleteUserAsync(admin, admin);

        Assert.False(result.Success);
        Assert.Equal(2, _db.Users.Count());
    }

    [Fact]
    public async Task Delete_AdminWhenTwoAdmins_Succeeds()
    {
        int admin1 = AddUser("admin1", UserRole.Admin);
        int admin2 = AddUser("admin2", UserRole.Admin);

        var result = await _service.DeleteUserAsync(admin1, admin2);

        Assert.True(result.Success);
        Assert.Equal(1, _db.Users.Count());
    }

    [Fact]
    public async Task Delete_ByRegularUser_Fails()
    {
        AddUser("admin", UserRole.Admin);
        int user = AddUser("user", UserRole.User);
        int victim = AddUser("victim", UserRole.User);

        var result = await _service.DeleteUserAsync(user, victim);

        Assert.False(result.Success);
        Assert.Equal(3, _db.Users.Count());
    }

    [Fact]
    public async Task Delete_RegularUser_RemovesTheirDataButKeepsMovies()
    {
        int admin = AddUser("admin", UserRole.Admin);
        int user = AddUser("user", UserRole.User);
        var movie = new Movie { Title = "Фильм" };
        _db.Movies.Add(movie);
        _db.SaveChanges();
        _db.Viewings.Add(new Viewing { UserID = user, MovieID = movie.MovieID, Status = ViewingStatus.Planned });
        _db.Collections.Add(new Collection { UserID = user, Name = "Подборка" });
        _db.SaveChanges();

        var result = await _service.DeleteUserAsync(admin, user);

        Assert.True(result.Success);
        Assert.Equal(0, _db.Viewings.Count());
        Assert.Equal(0, _db.Collections.Count());
        Assert.Equal(1, _db.Movies.Count());
    }

    [Fact]
    public async Task ChangeRole_DemoteLastAdmin_Fails()
    {
        int admin = AddUser("admin", UserRole.Admin);

        var result = await _service.ChangeRoleAsync(admin, admin, UserRole.User);

        Assert.False(result.Success);
        Assert.Equal(1, _db.Users.Count(u => u.Role == UserRole.Admin));
    }

    [Fact]
    public async Task ChangeRole_PromoteAndDemoteWithTwoAdmins_Succeeds()
    {
        int admin = AddUser("admin", UserRole.Admin);
        int user = AddUser("user", UserRole.User);

        Assert.True((await _service.ChangeRoleAsync(admin, user, UserRole.Admin)).Success);
        Assert.True((await _service.ChangeRoleAsync(admin, user, UserRole.User)).Success);
        Assert.Equal(1, _db.Users.Count(u => u.Role == UserRole.Admin));
    }

    [Fact]
    public async Task GetUsers_ReturnsAllSortedByName()
    {
        AddUser("борис", UserRole.User);
        AddUser("Анна", UserRole.Admin);

        var users = await _service.GetUsersAsync();

        Assert.Equal(new[] { "Анна", "борис" }, users.Select(u => u.Username).ToArray());
    }
}