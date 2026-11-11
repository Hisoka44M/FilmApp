using FilmApp.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Windows.Input;

namespace FilmApp.Data;

/// <summary>Контекст базы данных приложения (EF Core, SQLite).</summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<MovieGenre> MovieGenres => Set<MovieGenre>();
    public DbSet<MoviePerson> MoviePersons => Set<MoviePerson>();
    public DbSet<Viewing> Viewings => Set<Viewing>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<CollectionItem> CollectionItems => Set<CollectionItem>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // ---------- User ----------
        b.Entity<User>(e =>
        {
            e.ToTable("User", t =>
                t.HasCheckConstraint("CK_User_Role", "\"Role\" IN ('User', 'Admin')"));
            e.HasKey(x => x.UserID);
            e.Property(x => x.Username).IsRequired().HasMaxLength(50);
            e.Property(x => x.PasswordHash).IsRequired();
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(10);
            e.HasIndex(x => x.Username).IsUnique();
        });

        // ---------- Movie ----------
        b.Entity<Movie>(e =>
        {
            e.ToTable("Movie");
            e.HasKey(x => x.MovieID);
            e.Property(x => x.Title).IsRequired().HasMaxLength(300);
            e.HasIndex(x => x.TmdbID).IsUnique(); // несколько NULL в SQLite допустимы
        });

        // ---------- Genre / Person ----------
        b.Entity<Genre>(e =>
        {
            e.ToTable("Genre");
            e.HasKey(x => x.GenreID);
            e.Property(x => x.GenreName).IsRequired().HasMaxLength(100);
        });

        b.Entity<Person>(e =>
        {
            e.ToTable("Person");
            e.HasKey(x => x.PersonID);
            e.Property(x => x.FullName).IsRequired().HasMaxLength(200);
        });

        // ---------- MovieGenre ----------
        b.Entity<MovieGenre>(e =>
        {
            e.ToTable("MovieGenre");
            e.HasKey(x => new { x.MovieID, x.GenreID });
            e.HasOne(x => x.Movie).WithMany(m => m.MovieGenres)
                .HasForeignKey(x => x.MovieID).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Genre).WithMany(g => g.MovieGenres)
                .HasForeignKey(x => x.GenreID).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- MoviePerson ----------
        b.Entity<MoviePerson>(e =>
        {
            e.ToTable("MoviePerson", t =>
                t.HasCheckConstraint("CK_MoviePerson_Role", "\"Role\" IN ('Director', 'Actor')"));
            e.HasKey(x => new { x.MovieID, x.PersonID, x.Role });
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(10);
            e.HasOne(x => x.Movie).WithMany(m => m.MoviePersons)
                .HasForeignKey(x => x.MovieID).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Person).WithMany(p => p.MoviePersons)
                .HasForeignKey(x => x.PersonID).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Viewing ----------
        b.Entity<Viewing>(e =>
        {
            e.ToTable("Viewing", t =>
            {
                t.HasCheckConstraint("CK_Viewing_Status", "\"Status\" IN ('Watched', 'Planned')");
                t.HasCheckConstraint("CK_Viewing_Rating", "\"Rating\" IS NULL OR \"Rating\" BETWEEN 1 AND 10");
                // Дата просмотра, оценка и отзыв допустимы только у просмотренных фильмов
                t.HasCheckConstraint("CK_Viewing_WatchedOnly",
                    "\"Status\" = 'Watched' OR (\"ViewDate\" IS NULL AND \"Rating\" IS NULL " +
                    "AND \"ReviewText\" IS NULL AND \"ReviewDate\" IS NULL)");
            });
            e.HasKey(x => x.ViewingID);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(10);
            e.HasIndex(x => new { x.UserID, x.MovieID }).IsUnique();
            e.HasOne(x => x.User).WithMany(u => u.Viewings)
                .HasForeignKey(x => x.UserID).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Movie).WithMany(m => m.Viewings)
                .HasForeignKey(x => x.MovieID).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- Collection ----------
        b.Entity<Collection>(e =>
        {
            e.ToTable("Collection");
            e.HasKey(x => x.CollectionID);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.HasOne(x => x.User).WithMany(u => u.Collections)
                .HasForeignKey(x => x.UserID).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- CollectionItem ----------
        b.Entity<CollectionItem>(e =>
        {
            e.ToTable("CollectionItem", t =>
                // Ровно одно из полей MovieID и ChildCollectionID должно быть заполнено
                t.HasCheckConstraint("CK_CollectionItem_OneOf",
                    "(\"MovieID\" IS NULL AND \"ChildCollectionID\" IS NOT NULL) OR " +
                    "(\"MovieID\" IS NOT NULL AND \"ChildCollectionID\" IS NULL)"));
            e.HasKey(x => x.ItemID);
            e.HasOne(x => x.Collection).WithMany(c => c.Items)
                .HasForeignKey(x => x.CollectionID).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Movie).WithMany()
                .HasForeignKey(x => x.MovieID).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ChildCollection).WithMany(c => c.ReferencedBy)
                .HasForeignKey(x => x.ChildCollectionID).OnDelete(DeleteBehavior.Cascade);
        });
    }
}