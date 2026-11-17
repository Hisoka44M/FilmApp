using FilmApp.Models;

namespace FilmApp.Services;

/// <summary>Текущий вошедший пользователь (один на всё приложение).</summary>
public class UserSession
{
    public User? CurrentUser { get; private set; }

    public bool IsLoggedIn => CurrentUser is not null;

    public bool IsAdmin => CurrentUser?.Role == UserRole.Admin;

    /// <summary>Срабатывает при входе и выходе.</summary>
    public event Action? Changed;

    public void SignIn(User user)
    {
        CurrentUser = user;
        Changed?.Invoke();
    }

    public void SignOut()
    {
        CurrentUser = null;
        Changed?.Invoke();
    }
}