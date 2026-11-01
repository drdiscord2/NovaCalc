# BUILD — сборка NovaCalc

См. также: [LOCAL_BUILD_README.md](LOCAL_BUILD_README.md) (упрощённая версия) и [docs/BUILD.md](docs/BUILD.md).

## Кратко

```bat
build.bat
```

Результат: `dist\NovaCalc.exe` (один файл, без внешних DLL).

## Цепочка сборки build.bat

1. `msbuild` (если в PATH) → поиск MSBuild из VS 2022/2019 (Enterprise/Professional/Community/BuildTools);
2. иначе `dotnet build NovaCalc.csproj -c Release` (нужен пакет Microsoft.NETFramework.ReferenceAssemblies — только на этапе сборки, в рантайме его нет);
3. иначе встроенный `csc.exe` из `C:\Windows\Microsoft.NET\Framework64\v4.0.30319` (есть на любой Windows 10/11, доп. ПО не требуется).

После успешной сборки: запуск `NovaCalc.exe --selftest` и копирование exe в `dist\`.

## Требования

- Сборка: любой из вариантов выше.
- Запуск: чистая Windows 10/11 x64 (.NET Framework 4.8 встроен). NuGet-пакеты в рантайме отсутствуют.
