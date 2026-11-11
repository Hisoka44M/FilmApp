namespace FilmApp.Models;

/// <summary>Роль пользователя в системе.</summary>
public enum UserRole
{
    User,
    Admin
}

/// <summary>Статус просмотра фильма: просмотрен или запланирован.</summary>
public enum ViewingStatus
{
    Watched,
    Planned
}

/// <summary>Роль персоны в фильме.</summary>
public enum PersonRole
{
    Director,
    Actor
}