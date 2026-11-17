using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FilmApp.ViewModels;

/// <summary>Базовый класс ViewModel: уведомляет интерфейс об изменении свойств.</summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>Меняет значение поля и сообщает об этом интерфейсу, если значение действительно изменилось.</summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(name);
        return true;
    }
}