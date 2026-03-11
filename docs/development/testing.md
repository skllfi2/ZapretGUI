# Тестирование

## Unit Testing

### Тестирование сервисов

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
        public void AppState_ShouldInitializeWithDefaults()
        {
            var appState = new AppState();

            Assert.IsNotNull(appState);
            Assert.IsFalse(appState.IsRunning);
            Assert.AreEqual("general", appState.CurrentStrategy);
        }

        [TestMethod]
        public void AppState_ShouldToggleRunningState()
        {
            var appState = new AppState();

            appState.StartService();
            Assert.IsTrue(appState.IsRunning);

            appState.StopService();
            Assert.IsFalse(appState.IsRunning);
        }

        [TestMethod]
        public void AppState_ShouldRaisePropertyChanged()
        {
            var appState = new AppState();
            bool propertyChanged = false;

            appState.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(AppState.IsRunning))
                {
                    propertyChanged = true;
                }
            };

            appState.StartService();
            Assert.IsTrue(propertyChanged);
        }
    }
}
```

### Тестирование сервисов Winws

```csharp
// Services/WinwsServiceTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using ZapretGUI.Services;

namespace ZapretGUI.Tests
{
    [TestClass]
    public class WinwsServiceTests
    {
        [TestMethod]
        public async Task WinwsService_ShouldStartSuccessfully()
        {
            var mockProcess = new Mock<IProcessService>();
            mockProcess.Setup(p => p.StartAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            var winwsService = new WinwsService(mockProcess.Object);

            var result = await winwsService.StartAsync();

            Assert.IsTrue(result);
            Assert.IsTrue(winwsService.IsRunning);
            mockProcess.Verify(p => p.StartAsync(It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        public async Task WinwsService_ShouldHandleStartFailure()
        {
            var mockProcess = new Mock<IProcessService>();
            mockProcess.Setup(p => p.StartAsync(It.IsAny<string>()))
                .ReturnsAsync(false);

            var winwsService = new WinwsService(mockProcess.Object);

            var result = await winwsService.StartAsync();

            Assert.IsFalse(result);
            Assert.IsFalse(winwsService.IsRunning);
        }

        [TestMethod]
        public async Task WinwsService_ShouldStopSuccessfully()
        {
            var mockProcess = new Mock<IProcessService>();
            mockProcess.Setup(p => p.KillAsync(It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            var winwsService = new WinwsService(mockProcess.Object);
            await winwsService.StartAsync();

            await winwsService.StopAsync();

            Assert.IsFalse(winwsService.IsRunning);
            mockProcess.Verify(p => p.KillAsync(It.IsAny<int>()), Times.Once);
        }
    }
}
```

## Интеграционное тестирование

### Тестирование интеграции с внешними бинарниками

```csharp
// Services/IntegrationTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Threading.Tasks;

namespace ZapretGUI.Tests
{
    [TestClass]
    public class IntegrationTests
    {
        [TestMethod]
        public void Binaries_ShouldBeCopiedToOutput()
        {
            var outputDirectory = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "zapret");

            Assert.IsTrue(Directory.Exists(outputDirectory));

            var files = Directory.GetFiles(outputDirectory, "*");
            Assert.IsTrue(files.Length > 0);

            // Check for specific binaries
            var winwsExe = Path.Combine(outputDirectory, "winws.exe");
            Assert.IsTrue(File.Exists(winwsExe));

            var cygwinDll = Path.Combine(outputDirectory, "cygwin1.dll");
            Assert.IsTrue(File.Exists(cygwinDll));
        }

        [TestMethod]
        public async Task StrategyParser_ShouldParseValidBatFile()
        {
            var parser = new BatStrategyParser();
            var content = "@echo off\necho Starting strategy\n" +
                        "winws.exe -c config.txt";

            var strategies = await parser.ParseAsync(content);

            Assert.IsNotNull(strategies);
            Assert.IsTrue(strategies.Count > 0);
            Assert.AreEqual("Starting strategy", strategies[0].Description);
        }

        [TestMethod]
        public async Task StrategyParser_ShouldHandleInvalidBatFile()
        {
            var parser = new BatStrategyParser();
            var invalidContent = "invalid content";

            var strategies = await parser.ParseAsync(invalidContent);

            Assert.IsNotNull(strategies);
            Assert.AreEqual(0, strategies.Count);
        }
    }
}
```

## UI Testing

### Тестирование WinUI элементов

```csharp
// Views/UIElementTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.UI.Xaml.Controls;
using ZapretGUI.Views;

namespace ZapretGUI.Tests
{
    [TestClass]
    public class UIElementTests
    {
        [TestMethod]
        public void DashboardPage_ShouldInitializeCorrectly()
        {
            var dashboardPage = new DashboardPage();

            Assert.IsNotNull(dashboardPage);
            Assert.IsNotNull(dashboardPage.Content);

            // Check for expected elements
            var statusIndicator = dashboardPage.FindName("StatusIndicator") as TextBlock;
            Assert.IsNotNull(statusIndicator);

            var startButton = dashboardPage.FindName("StartButton") as Button;
            Assert.IsNotNull(startButton);
        }

        [TestMethod]
        public void SettingsPage_ShouldBindToViewModel()
        {
            var settingsViewModel = new SettingsViewModel();
            var settingsPage = new SettingsPage();

            // Set DataContext
            settingsPage.DataContext = settingsViewModel;

            // Check bindings
            var autoStartCheckbox = settingsPage.FindName("AutoStartCheckbox") as CheckBox;
            Assert.IsNotNull(autoStartCheckbox);

            // Verify binding
            autoStartCheckbox.IsChecked = true;
            Assert.IsTrue(settingsViewModel.AutoStart);
        }
    }
}
```

## Моки и заглушки

### Создание моков для тестирования

```csharp
// Services/IProcessService.cs
public interface IProcessService
{
    Task<bool> StartAsync(string fileName);
    Task KillAsync(int processId);
    Task<bool> IsRunningAsync(int processId);
}

// Services/ProcessService.cs
public class ProcessService : IProcessService
{
    public async Task<bool> StartAsync(string fileName)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            return process.Start();
        }
        catch
        {
            return false;
        }
    }

    public async Task KillAsync(int processId)
    {
        var process = Process.GetProcessById(processId);
        process.Kill();
    }

    public async Task<bool> IsRunningAsync(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }
}

// Usage in tests
var mockProcessService = new Mock<IProcessService>();
mockProcessService.Setup(p => p.StartAsync(It.IsAny<string>()))
    .ReturnsAsync(true);

var winwsService = new WinwsService(mockProcessService.Object);
```

## Тестовые данные

### Создание тестовых файлов

```csharp
// Tests/TestHelper.cs
public static class TestHelper
{
    public static string CreateTestBatFile(string content)
    {
        var tempPath = Path.GetTempFileName();
        File.Delete(tempPath);
        tempPath = Path.ChangeExtension(tempPath, ".bat");

        File.WriteAllText(tempPath, content);
        return tempPath;
    }

    public static void CleanupTestFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    public static AppSettings CreateTestSettings()
    {
        return new AppSettings
        {
            AutoUpdateCheck = true,
            Strategy = "general",
            Port = 8080
        };
    }
}

// Usage in tests
var testContent = "@echo off\necho Test strategy\n" +
                "winws.exe -c test.cfg";
var testFilePath = TestHelper.CreateTestBatFile(testContent);

// Run tests
var parser = new BatStrategyParser();
var strategies = await parser.ParseAsync(testFilePath);

// Cleanup
TestHelper.CleanupTestFile(testFilePath);
```

## Тестирование производительности

### Производительность операций

```csharp
// Services/PerformanceTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;

namespace ZapretGUI.Tests
{
    [TestClass]
    public class PerformanceTests
    {
        [TestMethod]
        [Timeout(1000)] // 1 second timeout
        public void WinwsService_StartShouldCompleteQuickly()
        {
            var winwsService = new WinwsService();
            var stopwatch = Stopwatch.StartNew();

            var task = winwsService.StartAsync();
            task.Wait();

            stopwatch.Stop();
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 1000);
        }

        [TestMethod]
        public void StrategyParser_ShouldParseLargeFile()
        {
            var largeContent = GenerateLargeBatContent(1000);
            var parser = new BatStrategyParser();
            var stopwatch = Stopwatch.StartNew();

            var strategies = parser.Parse(largeContent);

            stopwatch.Stop();
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 5000);
            Assert.IsNotNull(strategies);
        }

        private string GenerateLargeBatContent(int lineCount)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < lineCount; i++)
            {
                sb.AppendLine($"echo Strategy {i}");
                sb.AppendLine("winws.exe -c config{i}.txt");
            }
            return sb.ToString();
        }
    }
}
```

## Тестирование безопасности

### Безопасность операций

```csharp
// Services/SecurityTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ZapretGUI.Tests
{
    [TestClass]
    public class SecurityTests
    {
        [TestMethod]
        public void ServiceManager_ShouldValidateAdminRights()
        {
            var serviceManager = new ServiceManager();

            var isAdmin = serviceManager.IsRunningAsAdministrator();

            if (!isAdmin)
            {
                // Test should be run as administrator
                Assert.Inconclusive("Test requires administrator privileges");
            }
        }

        [TestMethod]
        public void Settings_ShouldValidateInput()
        {
            var settings = new AppSettings();

            // Test invalid port
            settings.Port = -1;
            Assert.IsFalse(settings.IsValid());

            // Test valid port
            settings.Port = 8080;
            Assert.IsTrue(settings.IsValid());
        }

        [TestMethod]
        public void StrategyParser_ShouldSanitizeInput()
        {
            var parser = new BatStrategyParser();
            var maliciousContent = "@echo off\nrundll32.exe ...";

            var strategies = parser.Parse(maliciousContent);

            // Should not execute dangerous commands
            Assert.IsFalse(strategies.Any(s => s.IsDangerous));
        }
    }
}
```

## Continuous Integration

### Конфигурация CI

```yaml
# .github/workflows/ci.yml
name: CI

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:
  build:
    runs-on: windows-latest

    steps:
    - uses: actions/checkout@v3

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 10.0.x

    - name: Restore dependencies
      run: dotnet restore

    - name: Build
      run: dotnet build --configuration Release --no-restore

    - name: Run tests
      run: dotnet test --no-build --verbosity normal

    - name: Publish
      if: github.ref == 'refs/heads/main'
      run: dotnet publish -c Release -r win-x64 --self-contained true

    - name: Upload artifacts
      uses: actions/upload-artifact@v3
      with:
        name: ZapretGUI
        path: bin/Release/net10.0-windows10.0.19041.0/win-x64/publish/
```

## Best Practices

### Рекомендации по тестированию

1. **Arrange-Act-Assert**: Используйте AAA паттерн для структурирования тестов
2. **Изолированные тесты**: Каждый тест должен быть независимым
3. **Читаемые имена**: Используйте описательные имена для тестов
4. **Тестовые данные**: Используйте тестовые данные для предсказуемых результатов
5. **Обработка ошибок**: Тестируйте как успешные, так и неуспешные сценарии
6. **Производительность**: Тестируйте производительность критических операций
7. **Безопасность**: Тестируйте безопасность и валидацию входных данных
8. **Integration tests**: Тестируйте интеграцию с внешними системами
9. **UI tests**: Тестируйте пользовательский интерфейс
10. **CI/CD**: Автоматизируйте тестирование в CI/CD pipeline

### Типы тестов

```
Unit Tests:      100% coverage
Integration Tests: 80% coverage
UI Tests:        50% coverage
Performance Tests: Critical paths
Security Tests:   Input validation
```

### Тестовые фреймворки

- **MSTest**: Microsoft's testing framework
- **xUnit**: Alternative testing framework
- **NUnit**: Another popular testing framework
- **Moq**: Mocking framework
- **FluentAssertions**: Better assertion library
- **AutoFixture**: Test data generation