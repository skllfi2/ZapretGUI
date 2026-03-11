# Архитектурные паттерны

## MVVM Pattern

ZapretGUI использует Model-View-ViewModel (MVVM) pattern для разделения пользовательского интерфейса от бизнес-логики.

### Структура MVVM

```
Model:
  - Business logic
  - Data access
  - External integrations

ViewModel:
  - State management
  - Business logic presentation
  - Command handling
  - Data transformation

View:
  - UI rendering
  - User interactions
  - Data binding
  - Visual presentation
```

### Реализация MVVM

```csharp
// Services/AppState.cs - ViewModel
public class AppState : INotifyPropertyChanged
{
    private bool _isRunning;

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (_isRunning != value)
            {
                _isRunning = value;
                OnPropertyChanged(nameof(IsRunning));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
```

```xml
<!-- Views/DashboardPage.xaml - View -->
<Page
    x:Class="ZapretGUI.Views.DashboardPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="using:ZapretGUI">

    <Grid>
        <TextBlock Text="{\}Binding IsRunning, Mode=OneWay}"
                   Visibility="{\}Binding IsRunning, Converter={StaticResource BooleanToVisibilityConverter}}"/>
    </Grid>
</Page>
```

## Сервисный слой

### Сервисы в ZapretGUI

```csharp
// Services/AppSettings.cs - Service
public class AppSettings
{
    public bool AutoUpdateCheck { get; set; } = true;
    public string Strategy { get; set; } = "general";

    public static AppSettings Load()
    {
        // Load from file or registry
        return new AppSettings();
    }

    public void Save()
    {
        // Save to file or registry
    }
}
```

```csharp
// Services/WinwsService.cs - Service
public class WinwsService : INotifyPropertyChanged
{
    private bool _isRunning;

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (_isRunning != value)
            {
                _isRunning = value;
                OnPropertyChanged(nameof(IsRunning));
                StatusChanged?.Invoke(value);
            }
        }
    }

    public event Action<bool>? StatusChanged;

    public async Task StartAsync()
    {
        // Start winws.exe process
        IsRunning = true;
    }

    public async Task StopAsync()
    {
        // Stop winws.exe process
        IsRunning = false;
    }
}
```

## Команды и взаимодействие

### Команды WinUI

```csharp
// Services/AppState.cs - Commands
public class AppState : INotifyPropertyChanged
{
    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand RestartCommand { get; }

    public AppState()
    {
        StartCommand = new RelayCommand(Start, CanStart);
        StopCommand = new RelayCommand(Stop, CanStop);
        RestartCommand = new RelayCommand(Restart);
    }

    private void Start()
    {
        // Implementation
    }

    private bool CanStart()
    {
        return !IsRunning;
    }
}
```

## Свойства зависимости

### Реализация свойств зависимости

```csharp
// Controls/StatusIndicator.cs - Dependency Property
public class StatusIndicator : Control
{
    public static readonly DependencyProperty IsRunningProperty =
        DependencyProperty.Register(
            nameof(IsRunning),
            typeof(bool),
            typeof(StatusIndicator),
            new PropertyMetadata(false, OnIsRunningChanged));

    public bool IsRunning
    {
        get => (bool)GetValue(IsRunningProperty);
        set => SetValue(IsRunningProperty, value);
    }

    private static void OnIsRunningChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var indicator = (StatusIndicator)d;
        indicator.UpdateVisualState();
    }
}
```

## Асинхронное программирование

### Async/Await паттерны

```csharp
// Services/UpdateChecker.cs - Async operations
public class UpdateChecker
{
    public async Task CheckForUpdatesAsync()
    {
        try
        {
            // Network request
            var response = await httpClient.GetAsync("https://api.example.com/updates");

            if (response.IsSuccessStatusCode)
            {
                var updateInfo = await response.Content.ReadFromJsonAsync<UpdateInfo>();
                // Process update
            }
        }
        catch (Exception ex)
        {
            // Handle error
        }
    }
}
```

## Управление состоянием

### Singleton pattern для состояния

```csharp
// Services/AppState.cs - Singleton
public sealed class AppState
{
    private static readonly Lazy<AppState> _instance =
        new(() => new AppState());

    public static AppState Instance => _instance.Value;

    private AppState()
    {
        // Private constructor
    }

    // State properties
    public bool IsRunning { get; private set; }
    public string CurrentStrategy { get; set; } = "general";
}
```

## Внедрение зависимостей

### DI паттерны

```csharp
// Services/ServiceCollection.cs - Service registration
public static class ServiceCollection
{
    private static readonly Dictionary<Type, object> _services =
        new Dictionary<Type, object>();

    public static void Register<TService, TImplementation>()
        where TImplementation : TService, new()
    {
        _services[typeof(TService)] = new TImplementation();
    }

    public static TService Get<TService>()
    {
        return (TService)_services[typeof(TService)];
    }
}

// Usage
ServiceCollection.Register<ISettingsService, SettingsService>();
var settingsService = ServiceCollection.Get<ISettingsService>();
```

## Обработка ошибок

### Error handling паттерны

```csharp
// Services/ServiceManager.cs - Error handling
public class ServiceManager
{
    public async Task<bool> StartServiceAsync()
    {
        try
        {
            await StartWinwsServiceAsync();
            await StartDivertServiceAsync();
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 5)
        {
            // Access denied
            ShowError("Access denied. Run as administrator.");
            return false;
        }
        catch (Exception ex)
        {
            // General error
            LogError(ex);
            ShowError("Failed to start service: " + ex.Message);
            return false;
        }
    }
}
```

## Наблюдаемые паттерны

### INotifyPropertyChanged

```csharp
// Services/BaseViewModel.cs - Base implementation
public abstract class BaseViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void SetProperty<T>(ref T backingField, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!EqualityComparer<T>.Default.Equals(backingField, value))
        {
            backingField = value;
            OnPropertyChanged(propertyName);
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

// Usage
public class SettingsViewModel : BaseViewModel
{
    private bool _autoStart;

    public bool AutoStart
    {
        get => _autoStart;
        set => SetProperty(ref _autoStart, value);
    }
}
```

## Обработка событий

### Event aggregator pattern

```csharp
// Services/EventAggregator.cs - Event system
public class EventAggregator
{
    private readonly Dictionary<Type, List<Delegate>> _events =
        new Dictionary<Type, List<Delegate>>();

    public void Subscribe<TEvent>(Action<TEvent> handler)
    {
        var eventType = typeof(TEvent);
        if (!_events.TryGetValue(eventType, out var handlers))
        {
            handlers = new List<Delegate>();
            _events[eventType] = handlers;
        }
        handlers.Add(handler);
    }

    public void Publish<TEvent>(TEvent @event)
    {
        if (_events.TryGetValue(typeof(TEvent), out var handlers))
        {
            foreach (var handler in handlers.OfType<Action<TEvent>>())
            {
                handler(@event);
            }
        }
    }
}

// Usage
var eventAggregator = new EventAggregator();
 eventAggregator.Subscribe<ServiceStatusChanged>(OnServiceStatusChanged);
eventAggregator.Publish(new ServiceStatusChanged { IsRunning = true });
```

## Паттерны для WinUI

### WinUI специфичные паттерны

```csharp
// Controls/BindableIcon.cs - WinUI control
public sealed class BindableIcon : IconElement
{
    public static readonly DependencyProperty IconSourceProperty =
        DependencyProperty.Register(
            nameof(IconSource),
            typeof(IconSource),
            typeof(BindableIcon),
            new PropertyMetadata(null, OnIconSourceChanged));

    public IconSource? IconSource
    {
        get => (IconSource?)GetValue(IconSourceProperty);
        set => SetValue(IconSourceProperty, value);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        UpdateIcon();
    }

    private static void OnIconSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (BindableIcon)d;
        control.UpdateIcon();
    }

    private void UpdateIcon()
    {
        if (IconSource != null)
        {
            // Update icon rendering
        }
    }
}
```

## Best practices

### Рекомендации по архитектуре

1. **Разделение обязанностей**: Каждый класс должен иметь одну ответственность
2. **Тестируемость**: Пишите код, который легко тестировать
3. **Асинхронность**: Используйте async/await для IO-операций
4. **Обработка ошибок**: Реализуйте комплексную обработку ошибок
5. **Состояние UI**: Управляйте состоянием UI через ViewModel
6. **Сервисы**: Используйте сервисы для бизнес-логики
7. **Команды**: Используйте команды для пользовательских действий
8. **Свойства**: Используйте свойства зависимости для UI-элементов

### Code organization

```
Services/
├── AppSettings.cs    # Конфигурация
├── AppState.cs       # Состояние приложения
├── BatStrategyParser.cs # Парсер стратегий
├── ServiceManager.cs  # Управление сервисами
├── UpdateChecker.cs   # Проверка обновлений
└── WinwsService.cs    # Сервис Winws

Views/
├── DashboardPage.xaml
├── LogsPage.xaml
├── ServicePage.xaml
├── SettingsPage.xaml
├── StrategiesPage.xaml
└── UpdatesPage.xaml

Controls/
├── CoreControl.xaml
└── CustomControls/
```