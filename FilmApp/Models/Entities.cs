namespace FilmApp.Models;

/// <summary>Пользователь приложения.</summary>
public class User
{
    public int UserID { get; set; }
    public string Username { get; set; } = string.Empty;

    /// <summary>Строка вида «итерации.соль.хэш» (PBKDF2), пароль в открытом виде не хранится.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.User;
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public List<Viewing> Viewings { get; set; } = new();
    public List<Collection> Collections { get; set; } = new();
}

/// <summary>Фильм каталога.</summary>
public class Movie
{
    public int MovieID { get; set; }

    /// <summary>Идентификатор в TMDB; пусто, если фильм добавлен вручную.</summary>
    public int? TmdbID { get; set; }

    public string Title { get; set; } = string.Empty;
    public int? ReleaseYear { get; set; }

    /// <summary>Длительность в минутах.</summary>
    public int? Duration { get; set; }

    public string? Description { get; set; }
    public string? PosterUrl { get; set; }

    public List<MovieGenre> MovieGenres { get; set; } = new();
    public List<MoviePerson> MoviePersons { get; set; } = new();
    public List<Viewing> Viewings { get; set; } = new();
}

/// <summary>Жанр.</summary>
public class Genre
{
    public int GenreID { get; set; }
    public string GenreName { get; set; } = string.Empty;

    public List<MovieGenre> MovieGenres { get; set; } = new();
}

/// <summary>Персона (режиссёр или актёр).</summary>
public class Person
{
    public int PersonID { get; set; }
    public string FullName { get; set; } = string.Empty;

    public List<MoviePerson> MoviePersons { get; set; } = new();
}

/// <summary>Связь «фильм – жанр».</summary>
public class MovieGenre
{
    public int MovieID { get; set; }
    public int GenreID { get; set; }

    public Movie Movie { get; set; } = null!;
    public Genre Genre { get; set; } = null!;
}

/// <summary>Участие персоны в фильме с указанием роли.</summary>
public class MoviePerson
{
    public int MovieID { get; set; }
    public int PersonID { get; set; }
    public PersonRole Role { get; set; }

    public Movie Movie { get; set; } = null!;
    public Person Person { get; set; } = null!;
}

/// <summary>Просмотр: статус, оценка и отзыв пользователя к фильму.</summary>
public class Viewing
{
    public int ViewingID { get; set; }
    public int UserID { get; set; }
    public int MovieID { get; set; }
    public ViewingStatus Status { get; set; }
    public DateTime? ViewDate { get; set; }

    /// <summary>Оценка от 1 до 10.</summary>
    public int? Rating { get; set; }

    public string? ReviewText { get; set; }
    public DateTime? ReviewDate { get; set; }

    public User User { get; set; } = null!;
    public Movie Movie { get; set; } = null!;
}

/// <summary>Подборка фильмов (узел паттерна «Компоновщик»).</summary>
public class Collection
{
    public int CollectionID { get; set; }
    public int UserID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public User User { get; set; } = null!;

    /// <summary>Элементы этой подборки.</summary>
    public List<CollectionItem> Items { get; set; } = new();

    /// <summary>Элементы других подборок, ссылающиеся на эту подборку как на вложенную.</summary>
    public List<CollectionItem> ReferencedBy { get; set; } = new();
}

/// <summary>Элемент подборки: либо фильм, либо вложенная подборка.</summary>
public class CollectionItem
{
    public int ItemID { get; set; }
    public int CollectionID { get; set; }
    public int? MovieID { get; set; }
    public int? ChildCollectionID { get; set; }

    public Collection Collection { get; set; } = null!;
    public Movie? Movie { get; set; }
    public Collection? ChildCollection { get; set; }
}