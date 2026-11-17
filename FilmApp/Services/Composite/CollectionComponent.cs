using FilmApp.Models;

namespace FilmApp.Services.Composite;

/// <summary>Общий интерфейс паттерна «Компоновщик»: и фильм, и подборка умеют отдавать свои фильмы.</summary>
public abstract class CollectionComponent
{
    public abstract string Name { get; }

    /// <summary>Все фильмы элемента (для подборки: включая вложенные, возможно с повторами).</summary>
    public abstract IEnumerable<Movie> GetMovies();

    /// <summary>Количество разных фильмов: один фильм в разных вложенных подборках считается один раз.</summary>
    public int CountMovies() => GetMovies().DistinctBy(m => m.MovieID).Count();
}

/// <summary>Лист дерева: один фильм.</summary>
public class MovieLeaf : CollectionComponent
{
    public Movie Movie { get; }

    public MovieLeaf(Movie movie) => Movie = movie;

    public override string Name => Movie.Title;

    public override IEnumerable<Movie> GetMovies()
    {
        yield return Movie;
    }
}

/// <summary>Составной узел дерева: подборка, которая содержит фильмы и другие подборки.</summary>
public class CollectionComposite : CollectionComponent
{
    private readonly List<CollectionComponent> _children = new();

    public int CollectionID { get; }
    public string? Description { get; }

    public CollectionComposite(int collectionId, string name, string? description)
    {
        CollectionID = collectionId;
        Name = name;
        Description = description;
    }

    public override string Name { get; }

    public IReadOnlyList<CollectionComponent> Children => _children;

    public void Add(CollectionComponent child) => _children.Add(child);

    public override IEnumerable<Movie> GetMovies() => _children.SelectMany(c => c.GetMovies());
}