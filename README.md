# NovaCalc

**NovaCalc — The Future of Calculation.**

Однофайловый калькулятор для Windows 10/11: скачали `NovaCalc.exe` → запустили → работаете. Без установки, без интернета, без браузерных технологий, без внешних DLL.

A single-file calculator for Windows 10/11: download `NovaCalc.exe`, run it, done. No installation, no internet required, no Electron/browser stack, no extra runtime.

- **EN:** NovaCalc is a fast, precise, offline calculator with three modes (Standard, Scientific, Laboratory), 12 UI languages, history, themes and full keyboard support. Built with C# / .NET Framework 4.8 / WinForms — already part of Windows, so nothing to install.
- **RU:** NovaCalc — быстрый, точный и полностью офлайн-калькулятор с тремя режимами (Обычный, Инженерный, Лабораторный), 12 языками интерфейса, историей, темами и полной поддержкой клавиатуры. C# / .NET Framework 4.8 / WinForms — всё встроено в Windows, устанавливать нечего.

## Скачать / собрать

**Готовый файл:** вкладка Actions → последний успешный запуск → artifact `NovaCalc.zip` (внутри один `NovaCalc.exe`, ~146 КБ). Либо релиз `v*`.

**Собрать самому (одна команда):**

```bat
build.bat        :: результат — dist\NovaCalc.exe
```

Подробности: [LOCAL_BUILD_README.md](LOCAL_BUILD_README.md) · [BUILD.md](BUILD.md) · [docs/BUILD.md](docs/BUILD.md)

Проверка сборки: `NovaCalc.exe --selftest` (код 0 = все тесты пройдены).

## Режимы

| Режим | Возможности |
|---|---|
| Обычный | + − × ÷, проценты как в Windows, ±, x², √x, 1/x, память MC/MR/M+/M−/MS, история, копирование/вставка |
| Инженерный | скобки и приоритеты, x^y, 10ˣ, 2ˣ, eˣ, ⁿ√x, log, ln, exp, n!, abs, sin/cos/tan (+ обратные и гиперболические), Deg/Rad/Grad, π, e, AND/OR/XOR/NOT/сдвиги, DEC/HEX/OCT/BIN, Ans |
| Лабораторный | конвертер единиц (11 категорий), валюты (офлайн, редактор курсов), физические константы, дата-калькулятор, статистика (сумма, среднее, min/max, медиана, σ) |

Точность: базовая арифметика на `decimal` — `0.1 + 0.2 = 0.3` ровно, до 28 значащих цифр.

## Языки интерфейса (12)

English · Русский · Українська · Қазақша · Deutsch · Français · Español · Italiano · Português · Türkçe · 中文 · العربية

Автоопределение по системе при первом запуске, выбор в настройках, мгновенное переключение без перезапуска, fallback на English.

## Горячие клавиши

`0–9` `. ,` ввод · `+ - * / ^ %` операции · `Enter` равно · `Backspace` стереть · `Esc`/`Delete` сброс · `F9` смена знака · `Ctrl+C` копировать результат · `Ctrl+Shift+C` копировать выражение · `Ctrl+V` вставить · `Ctrl+H` история · `Ctrl+,` настройки · `↑ ↓` история.

## Портативность

Работает с флешки: положите пустой файл `portable.flag` рядом с `NovaCalc.exe` — настройки, история и курсы валют будут храниться в той же папке. Иначе данные лежат в `%APPDATA%\NovaCalc` (JSON, реестр не используется). Реестр не трогается, телеметрия отсутствует, фоновый интернет не используется.

## Документация

- [docs/USER_GUIDE.md](docs/USER_GUIDE.md) — как пользоваться (режимы, проценты, валюты, настройки)
- [docs/FEATURES.md](docs/FEATURES.md) — полный список функций и языков
- [docs/PLAN.md](docs/PLAN.md) — архитектура и этапы
- [docs/DECISIONS.md](docs/DECISIONS.md) — ключевые технические решения
- [docs/BUILD.md](docs/BUILD.md) / [BUILD.md](BUILD.md) / [LOCAL_BUILD_README.md](LOCAL_BUILD_README.md) — сборка

## Технологии

C# · .NET Framework 4.8 · WinForms. Никаких NuGet-зависимостей рантайма, HTML/JS/Electron/WebView2 не используются.
