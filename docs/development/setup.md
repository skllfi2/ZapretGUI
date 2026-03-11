# Настройка окружения разработки

## Требования

### Операционная система
- Windows 10 (version 2004) or later
- Visual Studio 2022 (version 17.0 or later)
- .NET 10.0 SDK
- Windows App SDK (WinUI 3)

### Разработные инструменты
- Visual Studio 2022 Community/Professional/Enterprise
- Git
- .NET 10.0 SDK
- Windows App SDK 1.8
- WinUI 3
- C# 12.0

## Установка Visual Studio 2022

1. Скачайте Visual Studio 2022 Community с официального сайта: https://visualstudio.microsoft.com/
2. Во время установки выберите следующие компоненты:
   - Среда разработки для .NET Desktop
   - .NET 10.0 SDK
   - Разработка для мультиплатформных приложений .NET
   - Разработка классических библиотек .NET
   - Пользовательский визуальный студии
   - Инструменты для Windows App SDK

## Установка Windows App SDK

1. Скачайте Windows App SDK с: https://aka.ms/windowsappsdk
2. Выберите версию 1.8.260209005 или позже
3. Запустите установку

## Получение исходного кода

1. Клонируйте репозиторий или клонируйте проект через Git:
   ```bash
   git clone https://github.com/your-repo/ZapretGUI.git
   cd ZapretGUI
   ```

2. Откройте решение `ZapretGUI.sln` в Visual Studio 2022

## Сборка и разработка

### Конфигурация сборки

- **Debug**: Для разработки и отладки
- **Release**: Для сборки встроенной версии
- **x64**: 64-разрядная архитектура
- **x86**: 32-разрядная архитектура

### Переменные ключи сборки

```xml
<!-- ZapretGUI.csproj для Release сборки -->
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <PublishReadyToRun>True</PublishReadyToRun>
    <PublishTrimmed>True</PublishTrimmed>
</PropertyGroup>
```

### Командная строка

- **Build**: `Ctrl+Shift+B` или Варианты → Сборка
- **Rebuild**: Варианты → Пересборка
- **Clean**: Варианты → Очистить

## Разработка WinUI 3

### Структура проекта

```
ZapretGUI/
├── ZapretGUI.csproj          # Заглавный проект WinUI
├── App.xaml                    # Конфигурация приложения
├── App.xaml.cs                 # Код конфигурации
├── Program.cs                  # Точка входа
├── MainWindow.xaml            # Основное окно WinUI
├── MainWindow.xaml.cs         # Код основного окна
├── Views/                     # Страницы WinUI
├── Services/                  # Бизнес-логика
├── Controls/                  # Пользовательские элементы
└── zapret/                    # Бинарники и списки
```

### Добавление новой страницы

1. Создайте файл `Views/NewPage.xaml`
2. Создайте код-загрузчик `Views/NewPage.xaml.cs`
3. Добавьте новую страницу в навигацию

```xml
<!-- NewPage.xaml -->
<Window
    x:Class="ZapretGUI.Views.NewPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    Title="New Page" Height="450" Width="800">

    <Grid>
        <TextBlock Text="New Page Content"
                   HorizontalAlignment="Center"
                   VerticalAlignment="Center"
                   FontSize="24"/>
    </Grid>
</Window>
```

```csharp
// NewPage.xaml.cs
using Microsoft.UI.Xaml.Controls;

namespace ZapretGUI.Views
{
    public sealed partial class NewPage : Window
    {
        public NewPage()
        {
            this.InitializeComponent();
        }
    }
}
```

## Процесс CI/CD

### Конфигурация задач

1. Проверить код в разработке
2. Запустить тесты
3. Создать пул запросов
4. Собрать версию
5. Создать запускать

### Полезные команды Git

```bash
# Создание новой ветки
git checkout -b feature/new-feature

# Запуск изменений
git add .
git commit -m "feat: добавлено новую функцию"

# Пуш в главную ветку
git push origin feature/new-feature
```

## Управление зависимостями

### Пакетазации NuGet

1. Обновить зависимости:
   ```bash
   dotnet add package PackageName --version 1.0.0
   ```

2. Удалить зависимость:
   ```bash
   dotnet remove package PackageName
   ```

### Обновление зависимостей

```bash
# Обновление всех пакетов
dotnet restore

# Обновление конкретного пакета
dotnet add package Microsoft.WindowsAppSDK --version 1.8.260209005
```

## Тестирование

### Создание тестов

```csharp
// Services/AppStateTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZapretGUI.Services;

namespace ZapretGUI.Tests
{
    [TestClass]
    public class AppStateTests
    {
        [TestMethod]
        public void TestAppStateInitialization()
        {
            var appState = new AppState();
            Assert.IsNotNull(appState);
            Assert.IsFalse(appState.IsRunning);
        }
    }
}
```

### Запуск тестов

```bash
# Запуск всех тестов
dotnet test

# Запуск конкретного проекта
dotnet test ZapretGUI.Tests.csproj
```

## Устранение ошибок

### Частые проблемы

1. **WinUI зависимости**: Обновление WinUI SDK
2. **.NET синтаксис**: Обновление .NET SDK
3. **Windows SDK**: Обновление Windows SDK

### Устранение

1. Проверьте версии Visual Studio 2022
2. Обновите версии WinUI SDK
3. Перезапустите версии .NET 10.0 SDK
4. Очистите кэш Visual Studio 2022

## Утилиты разработки

- Используйте консистентный интерфейс WinUI
- Следуйте принципуму MVVM
- Используйте асинхронную программирования для UI
- Тестируйте изменения в локах
- Документируйтесь перед отправкой изменений

## Ресурсы

- [Microsoft WinUI Documentation](https://docs.microsoft.com/en-us/windows/apps/winui/)
- [.NET 10.0 Documentation](https://docs.microsoft.com/en-us/dotnet/core/dotnet-five/)
- [Windows App SDK Documentation](https://docs.microsoft.com/en-us/windows/apps/windows-app-sdk/)
- [C# 12.0 Documentation](https://docs.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-12)
- [Visual Studio 2022 Documentation](https://docs.microsoft.com/en-us/visualstudio/)