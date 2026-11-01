# BUILD — подробная инструкция сборки NovaCalc

Краткая версия для пользователя: [../LOCAL_BUILD_README.md](../LOCAL_BUILD_README.md), корневая: [../BUILD.md](../BUILD.md).

## Что нужно

| Действие | Требования |
|---|---|
| **Запустить** NovaCalc.exe | Чистая Windows 10/11 x64 — ничего устанавливать не нужно (.NET Framework 4.8 встроен) |
| **Собрать** из исходников | Любой один инструмент: Visual Studio 2022 (Community/BuildTools, «.NET desktop development»), или .NET SDK (`dotnet`), или вообще ничего — сойдёт встроенный `csc.exe` .NET Framework |

NuGet-пакеты используются только на этапе сборки (`Microsoft.NETFramework.ReferenceAssemblies` для варианта с `dotnet`). В рантайме зависимостей нет.

## Способ 1 — автоматический скрипт (рекомендуется)

```bat
cd <папка репозитория>
build.bat
```

или PowerShell:

```powershell
.\build.ps1
```

Скрипт сам определит доступный компилятор по цепочке **MSBuild → dotnet → csc.exe**, соберёт Release, выполнит `NovaCalc.exe --selftest` и положит итог в:

```
dist\NovaCalc.exe
```

## Способ 2 — MSBuild вручную

```bat
msbuild NovaCalc.csproj /t:Rebuild /p:Configuration=Release /v:minimal
bin\Release\NovaCalc.exe --selftest
```

Если `msbuild` не в PATH, используйте полный путь, например:
`"C:\Program Files\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"`

## Способ 3 — dotnet SDK

```bat
dotnet build NovaCalc.csproj -c Release
bin\Release\NovaCalc.exe --selftest
```

## Способ 4 — только встроенный компилятор Windows (без установки чего-либо)

```bat
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
%CSC% /nologo /optimize+ /target:winexe /out:dist\NovaCalc.exe ^
  /win32manifest:app.manifest ^
  /reference:System.dll /reference:System.Core.dll ^
  /reference:System.Windows.Forms.dll /reference:System.Drawing.dll ^
  src\Program.cs src\Engine\*.cs src\Converters\*.cs src\Services\*.cs ^
  src\Localization\*.cs src\Themes\*.cs src\UI\*.cs
dist\NovaCalc.exe --selftest
```

(Именно эту цепочку `build.bat` использует как последний запасной вариант.)

## Проверка результата

```bat
dist\NovaCalc.exe --selftest
echo %errorlevel%   :: 0 = все ~77 тестов пройдены
```

## CI (GitHub Actions)

`.github/workflows/build.yml`: windows-latest → сборка Release → `--selftest` → artifact **NovaCalc.zip** с единственным файлом `NovaCalc.exe` внутри. Архив публикуется как артефакт запуска и (для тегов `v*`) как вложение Draft Release.

## Где итоговый файл

- после любого способа: `dist\NovaCalc.exe` (≈146 КБ);
- промежуточный: `bin\Release\NovaCalc.exe`.

## Типичные проблемы

- **«csc не найден»** — проверьте, что Windows 10/11 имеет включённый компонент .NET Framework 4.8 (Панель управления → Программы → Включение компонентов Windows).
- **Антивирус ругается на exe** — ложное срабатывание на unsigned-бинарь; собирайте сами или подписывайте. Проект полностью открытый, телеметрии нет.
- **Сборка на русской Windows ломает batch** — кодировка скриптов ASCII, проблем быть не должно; при запуске из проводника используйте cmd, а не PowerShell (для `build.bat`).
