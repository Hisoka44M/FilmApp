using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FilmApp.Data;

/// <summary>
/// Нужна только инструментам EF (Add-Migration, Update-Database),
/// чтобы создать контекст без запуска всего приложения.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=design.db;Foreign Keys=True")
            .Options;
        return new AppDbContext(options);
    }
}