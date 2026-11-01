# NovaCalc — быстрая сборка на своём компьютере

**NovaCalc — The Future of Calculation.**

Нужен только один готовый файл? Скачайте `NovaCalc.exe` из релиза (GitHub Actions artifact) — установка не требуется: запустили и работаете. Интернет для основных функций не нужен.

## Если хотите собрать сами (3 шага)

1. **Скачайте исходники** (кнопка Code → Download ZIP) и распакуйте в любую папку, например `C:\NovaCalc`.
2. **Откройте командную строку** (`Win+R` → `cmd`) в этой папке:
   ```bat
   cd C:\NovaCalc
   build.bat
   ```
3. **Готово!** Итоговый файл появится здесь:
   ```
   C:\NovaCalc\dist\NovaCalc.exe
   ```

После сборки скрипт автоматически запускает проверку: `NovaCalc.exe --selftest` (код 0 = всё в порядке).

## Что нужно для сборки (минимальные требования)

| Вариант | Инструмент | Комментарий |
|---|---|---|
| 1 (лучше) | Visual Studio 2022 Community или **Build Tools** (галочка «.NET desktop build tools») | MSBuild собирается «из коробки» |
| 2 | .NET SDK 6+ (`dotnet --version`) | `build.bat` сам использует `dotnet build` с ReferenceAssemblies |
| 3 | Ничего дополнительно | На Windows 10/11 уже есть .NET Framework 4.8, а в нём встроенный компилятор `csc.exe` — `build.bat` найдёт его сам как запасной вариант |

Запускать готовый `NovaCalc.exe` может **любая чистая Windows 10/11 x64** — ничего устанавливать не нужно (.NET Framework 4.8 встроен в систему).

## Ручные команды (если без build.bat)

```bat
:: Через MSBuild
msbuild NovaCalc.csproj /t:Rebuild /p:Configuration=Release
bin\Release\NovaCalc.exe --selftest

:: Через dotnet SDK
dotnet build NovaCalc.csproj -c Release
```

Подробности — в [docs/BUILD.md](docs/BUILD.md).
