using System.IO;
using System.Windows;
using FilmApp.Data;
using FilmApp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace FilmApp;

/// <summary>Точка входа: настройка журнала, конфигурации, DI и базы данных.</summary>
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // Каталог данных: %LocalAppData%\FilmApp
            string dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FilmApp");
            Directory.CreateDirectory(dataDir);

            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(Path.Combine(dataDir, "logs", "log-.txt"),
                    rollingInterval: RollingInterval.Day)
                .CreateLogger();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.AddLogging(b => b.AddSerilog(dispose: true));

            string dbPath = Path.Combine(dataDir, "films.db");
            services.AddDbContext<AppDbContext>(
                o => o.UseSqlite($"Data Source={dbPath};Foreign Keys=True"),
                ServiceLifetime.Transient);

            services.AddTransient<AuthService>();
            // Дальше сюда добавляются остальные сервисы, ViewModel и окна

            Services = services.BuildServiceProvider();

            // Применяем миграции при запуске: при первом запуске база создаётся сама
            using (var db = Services.GetRequiredService<AppDbContext>())
            {
                db.Database.Migrate();
            }

            Log.Information("Приложение запущено, база: {Db}", dbPath);

            // Пока без окон: проверка, что всё поднимается
            // new MainWindow().Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Ошибка при запуске приложения");
            MessageBox.Show("Не удалось запустить приложение. Подробности в журнале.",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}