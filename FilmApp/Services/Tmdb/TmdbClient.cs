using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace FilmApp.Services.Tmdb;

/// <summary>Ошибка при обращении к TMDB. Текст сообщения можно показывать пользователю.</summary>
public class TmdbException : Exception
{
    public TmdbException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>Клиент TMDB API: поиск фильмов и получение подробных данных.</summary>
public class TmdbClient
{
    private readonly HttpClient _http;
    private readonly ILogger<TmdbClient> _log;
    private readonly string _baseUrl;
    private readonly string _language;
    private readonly string _apiKey;

    public TmdbClient(HttpClient http, IConfiguration config, ILogger<TmdbClient> log)
    {
        _http = http;
        _log = log;
        _baseUrl = (config["Tmdb:BaseUrl"] ?? "https://api.themoviedb.org/3/").TrimEnd('/') + "/";
        _language = config["Tmdb:Language"] ?? "ru-RU";
        _apiKey = config["Tmdb:ApiKey"] ?? string.Empty;
    }

    /// <summary>Ищет фильмы по части названия.</summary>
    public async Task<List<TmdbSearchItem>> SearchMoviesAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<TmdbSearchItem>();

        string url = $"{_baseUrl}search/movie?api_key={_apiKey}&language={_language}" +
                     $"&query={Uri.EscapeDataString(query.Trim())}";
        var response = await GetAsync<TmdbSearchResponse>(url, ct);
        return response?.Results ?? new List<TmdbSearchItem>();
    }

    /// <summary>Получает подробные данные о фильме (с актёрами и съёмочной группой). Null, если фильм не найден.</summary>
    public Task<TmdbMovieDetails?> GetMovieAsync(int tmdbId, CancellationToken ct = default)
    {
        string url = $"{_baseUrl}movie/{tmdbId}?api_key={_apiKey}&language={_language}" +
                     "&append_to_response=credits";
        return GetAsync<TmdbMovieDetails>(url, ct);
    }

    private async Task<T?> GetAsync<T>(string url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new TmdbException("Не задан ключ TMDB API. Укажите его в файле appsettings.local.json.");

        try
        {
            using var response = await _http.GetAsync(url, ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new TmdbException("Ключ TMDB API недействителен.");
            if (response.StatusCode == HttpStatusCode.NotFound)
                return default;

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(ct);
        }
        catch (TmdbException)
        {
            throw;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Сработал таймаут HttpClient, а не отмена пользователем
            _log.LogWarning("TMDB не ответил за отведённое время");
            throw new TmdbException("Сервер TMDB не ответил вовремя. Проверьте подключение к интернету.");
        }
        catch (HttpRequestException ex)
        {
            // Адрес не логируем: в нём находится ключ
            _log.LogWarning("Ошибка сети при обращении к TMDB: {Status}", ex.StatusCode);
            throw new TmdbException("Не удалось связаться с TMDB. Проверьте подключение к интернету.", ex);
        }
        catch (JsonException ex)
        {
            _log.LogError(ex, "Не удалось разобрать ответ TMDB");
            throw new TmdbException("TMDB вернул данные в неожиданном формате.", ex);
        }
    }
}