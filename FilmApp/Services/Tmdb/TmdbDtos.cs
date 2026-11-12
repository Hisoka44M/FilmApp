using System.Text.Json.Serialization;

namespace FilmApp.Services.Tmdb;

/// <summary>Ответ TMDB на поиск фильмов.</summary>
public class TmdbSearchResponse
{
    [JsonPropertyName("results")]
    public List<TmdbSearchItem> Results { get; set; } = new();
}

/// <summary>Краткие данные о фильме из результатов поиска.</summary>
public class TmdbSearchItem
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("release_date")] public string? ReleaseDate { get; set; }
    [JsonPropertyName("poster_path")] public string? PosterPath { get; set; }
}

/// <summary>Подробные данные о фильме вместе с актёрами и съёмочной группой.</summary>
public class TmdbMovieDetails
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("release_date")] public string? ReleaseDate { get; set; }
    [JsonPropertyName("runtime")] public int? Runtime { get; set; }
    [JsonPropertyName("overview")] public string? Overview { get; set; }
    [JsonPropertyName("poster_path")] public string? PosterPath { get; set; }
    [JsonPropertyName("genres")] public List<TmdbGenre> Genres { get; set; } = new();
    [JsonPropertyName("credits")] public TmdbCredits? Credits { get; set; }
}

public class TmdbGenre
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
}

public class TmdbCredits
{
    [JsonPropertyName("cast")] public List<TmdbCastMember> Cast { get; set; } = new();
    [JsonPropertyName("crew")] public List<TmdbCrewMember> Crew { get; set; } = new();
}

public class TmdbCastMember
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("order")] public int Order { get; set; }
}

public class TmdbCrewMember
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("job")] public string? Job { get; set; }
}