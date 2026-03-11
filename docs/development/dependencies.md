# Управление зависимостями

## Пакеты NuGet

### Текущая конфигурация

```xml
<!-- ZapretGUI.csproj -->
<ItemGroup>
    <PackageReference Include="Microsoft.Graphics.Win2D" Version="1.3.2" />
    <PackageReference Include="Microsoft.Windows.SDK.BuildTools" Version="10.0.26100.7705" />
    <PackageReference Include="Microsoft.WindowsAppSDK" Version="1.8.260209005" />
    <PackageReference Include="System.ServiceProcess.ServiceController" Version="8.0.0" />
</ItemGroup>
```

### Управление пакетами

```bash
# Добавление нового пакета
dotnet add package PackageName --version 1.0.0

# Удаление пакета
dotnet remove package PackageName

# Обновление всех пакетов
dotnet restore

# Обновление конкретного пакета
dotnet add package Microsoft.WindowsAppSDK --version 1.9.0

# Просмотр установленных пакетов
dotnet list package

# Восстановление зависимостей
dotnet restore
```

### Версионирование

```xml
<!-- Рекомендуемый подход к версионированию -->
<PropertyGroup>
    <PackageVersion>1.2.3</PackageVersion>
    <Version>1.2.3</Version>
</PropertyGroup>

<!-- Зависимости с версионным диапазоном -->
<ItemGroup>
    <PackageReference Include="Microsoft.WindowsAppSDK" Version="[1.8.0, 2.0.0)" />
</ItemGroup>
```

## Внешние бинарники

### Структура бинарников

```
zapret/
├── utils/
│   └── test zapret.ps1
├── version.txt
├── winws/
│   ├── winws.exe
│   ├── cygwin1.dll
│   ├── WinDivert.dll
│   ├── WinDivert64.sys
│   ├── quic_initial_www_google_com.bin
│   ├── stun.bin
│   ├── tls_clienthello_4pda_to.bin
│   ├── tls_clienthello_max_ru.bin
│   ├── tls_clienthello_www_google_com.bin
├── lists/
│   ├── ipset-all.txt
│   ├── ipset-exclude-user.txt
│   ├── ipset-exclude.txt
│   ├── list-exclude-user.txt
│   ├── list-exclude.txt
│   ├── list-general-user.txt
│   ├── list-general.txt
│   └── list-google.txt
└── strategies/
    ├── general.bat
    ├── general (ALT).bat
    ├── general (ALT2).bat
    ├── general (ALT3).bat
    ├── general (ALT4).bat
    ├── general (ALT5).bat
    ├── general (ALT6).bat
    ├── general (ALT7).bat
    ├── general (ALT8).bat
    ├── general (ALT9).bat
    ├── general (ALT10).bat
    ├── general (ALT11).bat
    ├── general (FAKE TLS AUTO).bat
    ├── general (FAKE TLS AUTO ALT).bat
    ├── general (FAKE TLS AUTO ALT2).bat
    ├── general (FAKE TLS AUTO ALT3).bat
    ├── general (SIMPLE FAKE).bat
    ├── general (SIMPLE FAKE ALT).bat
    └── general (SIMPLE FAKE ALT2).bat
```

### Копирование бинарников

```xml
<!-- ZapretGUI.csproj - Копирование файлов -->
<ItemGroup>
    <!-- Версия zapret -->
    <None Update="zapret\utils\test zapret.ps1">
        <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
    <None Update="zapret\version.txt">
        <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>

    <!-- Бинарники winws -->
    <None Update="zapret\winws\winws.exe">
        <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
    <None Update="zapret\winws\cygwin1.dll">
        <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
    <None Update="zapret\winws\WinDivert.dll">
        <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
    <None Update="zapret\winws\WinDivert64.sys">
        <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
    <None Update="zapret\winws\quic_initial_www_google_com.bin">
        <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
    <None Update="zapret\winws\stun.bin">
        <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
    <None Update="zapret\winws\tls_clienthello_4pda_to.bin">
        <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
    <None Update="zapret\winws\tls_clienthello_max_ru.bin">
        <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
    <None Update="zapret\winws\tls_clienthello_www_google_com.bin">
        <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>

    <!-- Списки -->
    <None Update="zapret\lists\ipset-all.txt">
        <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
    <None Update="zapret\lists\ipset-exclude-user.txt">
        <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
    <None Update="zapret\lists\ipset-exclude.txt">
        <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
    <None Update="zapret\lists\list-exclude-user.txt">
        <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
    <None Update="zapret\lists\list-exclude.txt">
        <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
    <None Update="zapret\lists\list-general-user.txt">
        <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
    <None Update="zapret\lists\list-general.txt">
        <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
    <None Update="zapret\lists\list-google.txt">
        <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>

    <!-- Стратегии -->
    <None Update="zapret\strategies\general.bat">
        <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
    <!-- ... все стратегии -->
</ItemGroup>
```

### Версионирование бинарников

```csharp
// Services/WinwsService.cs - Проверка версии
public class WinwsService
{
    public string GetWinwsVersion()
    {
        var versionPath = Path.Combine(
            AppContext.BaseDirectory,
            "zapret",
            "version.txt");

        if (File.Exists(versionPath))
        {
            return File.ReadAllText(versionPath).Trim();
        }

        return "Unknown";
    }

    public bool IsWinwsUpToDate()
    {
        var currentVersion = GetWinwsVersion();
        var latestVersion = CheckLatestVersion();

        return currentVersion == latestVersion;
    }
}
```

## Сборка и публикация

### Конфигурация публикации

```xml
<!-- ZapretGUI.csproj - Publish configuration -->
<PropertyGroup>
    <PublishProfile>win-$(Platform).pubxml</PublishProfile>
</PropertyGroup>

<PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <PublishReadyToRun>True</PublishReadyToRun>
    <PublishTrimmed>True</PublishTrimmed>
</PropertyGroup>

<PropertyGroup Condition="'$(Configuration)' == 'Debug'">
    <PublishReadyToRun>False</PublishReadyToRun>
    <PublishTrimmed>False</PublishTrimmed>
</PropertyGroup>
```

### Публикация приложения

```bash
# Публикация для конкретной платформы
dotnet publish -c Release -r win-x64 --self-contained true

# Публикация для всех поддерживаемых платформ
dotnet publish -c Release -r win-x86 --self-contained true
dotnet publish -c Release -r win-x64 --self-contained true
dotnet publish -c Release -r win-arm64 --self-contained true

# Публикация с включенным ReadyToRun
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true

# Публикация с включенным Trimming
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishTrimmed=true
```

### Управление ресурсами

```csharp
// Services/ResourceManager.cs - Управление ресурсами
public class ResourceManager
{
    public string GetAssetPath(string assetName)
    {
        var baseDirectory = AppContext.BaseDirectory;
        var assetDirectory = Path.Combine(baseDirectory, "Assets");
        var assetPath = Path.Combine(assetDirectory, assetName);

        if (File.Exists(assetPath))
        {
            return assetPath;
        }

        throw new FileNotFoundException($"Asset not found: {assetName}");
    }

    public string GetStrategyPath(string strategyName)
    {
        var baseDirectory = AppContext.BaseDirectory;
        var strategyDirectory = Path.Combine(baseDirectory, "zapret", "strategies");
        var strategyPath = Path.Combine(strategyDirectory, strategyName + ".bat");

        if (File.Exists(strategyPath))
        {
            return strategyPath;
        }

        throw new FileNotFoundException($"Strategy not found: {strategyName}");
    }
}
```

## Версионирование проекта

### Семантическое версионирование

```xml
<!-- ZapretGUI.csproj - Version management -->
<PropertyGroup>
    <Version>1.2.3</Version>
    <FileVersion>1.2.3</FileVersion>
    <AssemblyVersion>1.2.3.0</AssemblyVersion>
    <PackageVersion>1.2.3</PackageVersion>
</PropertyGroup>

<!-- Автоматическое версионирование -->
<PropertyGroup>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
</PropertyGroup>

<ItemGroup>
    <None Include="Version.props" />
</ItemGroup>
```

### Версионирование в CI/CD

```yaml
# .github/workflows/version.yml
name: Version Management

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:
  version:
    runs-on: windows-latest

    steps:
    - uses: actions/checkout@v3

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 10.0.x

    - name: Get current version
      run: |
        dotnet tool install GitVersion.Tool --global
        gitversion /output json /showvariable SemVer

    - name: Update version
      run: |
        dotnet tool install GitVersion.Tool --global
        dotnet gitversion update-project
```

## Лицензирование

### Лицензионные файлы

```xml
<!-- ZapretGUI.csproj - License management -->
<ItemGroup>
    <None Include="LICENSE.txt">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Include="THIRD-PARTY-NOTICES.txt">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
</ItemGroup>
```

### Лицензирование зависимостей

```csharp
// Services/LicenseChecker.cs - License validation
public class LicenseChecker
{
    public void ValidateDependencies()
    {
        // Check package licenses
        var packageLicenses = GetPackageLicenses();

        foreach (var license in packageLicenses)
        {
            if (!IsCompatibleLicense(license))
            {
                throw new InvalidOperationException(
                    $"Incompatible license detected: {license}");
            }
        }

        // Check binary licenses
        var binaryLicenses = GetBinaryLicenses();

        foreach (var license in binaryLicenses)
        {
            if (!IsCompatibleLicense(license))
            {
                throw new InvalidOperationException(
                    $"Incompatible binary license detected: {license}");
            }
        }
    }

    private IEnumerable<string> GetPackageLicenses()
    {
        // Implementation to get package licenses
        return new List<string>();
    }

    private IEnumerable<string> GetBinaryLicenses()
    {
        // Implementation to get binary licenses
        return new List<string>();
    }

    private bool IsCompatibleLicense(string license)
    {
        // Implementation to check license compatibility
        return true;
    }
}
```

## Best Practices

### Рекомендации по управлению зависимостями

1. **Версионный контроль**: Используйте семантическое версионирование
2. **Зависимости**: Держите зависимости в актуальном состоянии
3. **Лицензирование**: Проверяйте лицензии всех зависимостей
4. **Безопасность**: Регулярно проверяйте уязвимости в зависимостях
5. **Размер**: Минимизируйте размер приложения за счет trimming
6. **Совместимость**: Тестируйте совместимость с разными версиями Windows
7. **Документация**: Документируйте все внешние зависимости
8. **CI/CD**: Автоматизируйте управление зависимостями в CI/CD pipeline

### Мониторинг зависимостей

```csharp
// Services/DependencyMonitor.cs - Dependency monitoring
public class DependencyMonitor
{
    public void CheckForUpdates()
    {
        // Check NuGet package updates
        CheckNuGetUpdates();

        // Check binary updates
        CheckBinaryUpdates();

        // Check Windows App SDK updates
        CheckWindowsAppSDKUpdates();
    }

    private void CheckNuGetUpdates()
    {
        // Implementation to check NuGet package updates
    }

    private void CheckBinaryUpdates()
    {
        // Implementation to check binary updates
    }

    private void CheckWindowsAppSDKUpdates()
    {
        // Implementation to check Windows App SDK updates
    }
}
```

### Резервное копирование

```csharp
// Services/BackupManager.cs - Backup management
public class BackupManager
{
    public void CreateBackup()
    {
        var backupPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ZapretGUI",
            "Backups");

        if (!Directory.Exists(backupPath))
        {
            Directory.CreateDirectory(backupPath);
        }

        // Backup configuration
        BackupFile("zapret\version.txt", backupPath);
        BackupFile("zapret\winws\winws.exe", backupPath);
        BackupFile("zapret\lists\ipset-all.txt", backupPath);
    }

    private void BackupFile(string relativePath, string backupPath)
    {
        var sourcePath = Path.Combine(AppContext.BaseDirectory, relativePath);
        var backupFilePath = Path.Combine(backupPath, Path.GetFileName(relativePath));

        if (File.Exists(sourcePath))
        {
            File.Copy(sourcePath, backupFilePath, true);
        }
    }
}
```