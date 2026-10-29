# Журнал выполнения ревизии PythonClient

**Ветка:** `dev`
**Основание:** `plan.md` (ревизия ADO.NET x64 provider поверх pythonnet 3.0.5)
**Целевая платформа:** .NET Framework 4.8, x64, pythonnet 3.0.5

Легенда статусов: ✅ выполнено · ⏳ в работе · ⛔ заблокировано · ⬜ не начато

## Окружение (проверено)

| Компонент | Значение |
|---|---|
| MSBuild | `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe` (18.10.1.42706) |
| VSTest | `C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\Extensions\TestPlatform\vstest.console.exe` (18.10.0) |
| dotnet SDK | 10.0.401 |
| nuget.exe | `%TEMP%\nugetcli\nuget.exe` (скачан с dist.nuget.org, в репозиторий не добавляется) |
| Python / pandas | **не установлены** (в PATH только Store-заглушка) |

## Ограничения и особенности

1. **Интеграционные тесты (`TestCategory=Python`) в этом окружении не выполняются** — нет Python.
   Для их прогона нужен `PYTHONNET_TEST_DLL=<путь к python3xx.dll>` и установленные `numpy`/`pandas`.
   Такие тесты пишутся, но помечаются как «не прогонялись».
2. **`msbuild /t:restore` не восстанавливает `packages.config`** (проекты legacy-формата, в MSBuild отсутствует
   поддержка packages.config-restore; при явном `/p:RestorePackagesConfig=true` — ошибка «Решение не найдено»).
   Используется `nuget.exe restore <packages.config> -PackagesDirectory packages`.
3. Папка `packages/` покрыта `.gitignore`, в репозиторий не попадает.

## Команды проверки (используются на каждом шаге)

```powershell
$msbuild = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
$vstest  = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\Extensions\TestPlatform\vstest.console.exe'

# восстановление пакетов (после изменения packages.config)
& "$env:TEMP\nugetcli\nuget.exe" restore 'Pike.PythonClient64\packages.config' -PackagesDirectory 'packages' -NonInteractive
& "$env:TEMP\nugetcli\nuget.exe" restore 'Pike.PythonClient.Test\packages.config' -PackagesDirectory 'packages' -NonInteractive

# сборка
& $msbuild 'PythonClient.sln' /p:Configuration=Debug '/p:Platform=Any CPU' /m /v:m /nologo

# быстрый набор тестов (не требует Python)
& $vstest 'Pike.PythonClient.Test\bin\Debug\Pike.PythonClient.Test.dll' '/TestCaseFilter:TestCategory=Unit'

# интеграционные тесты (требуют установленный Python)
# $env:PYTHONNET_TEST_DLL = 'C:\Python311\python311.dll'
# & $vstest 'Pike.PythonClient.Test\bin\Debug\Pike.PythonClient.Test.dll' '/TestCaseFilter:TestCategory=Python'
```

## План этапов

| Этап | Содержание | Статус |
|---|---|---|
| 0 | Подготовка: restore, baseline-сборка, журнал | ✅ |
| 1 | P0-1…P0-4: connection string, коллекции параметров, `PythonParameter` + Unit-тесты | ⬜ |
| 2 | P0-8, P0-9: конвертеры, реестр dtype, конвертация DataFrame | ⬜ |
| 3 | P0-6, P0-7: GIL на операцию, refcount движка, жизненный цикл соединения | ⬜ |
| 4 | P0-5: безопасная регистрация в `machine.config` | ⬜ |
| 5 | P1-1…P1-5: контракты `DbCommand`/`DbParameter`/`DbConnection`, контекст выполнения | ⬜ |
| 6 | P2-1…P2-6: SDK-style, вынос install, тестовая инфраструктура, документация, CI | ⬜ |

---

## Этап 0. Подготовка

### 0.1 Восстановление пакетов и baseline-сборка — ✅ (2026-10-29)

**Сделано**

- Скачан `nuget.exe` в `%TEMP%\nugetcli` (в репозиторий не добавляется).
- Восстановлены пакеты в корневую папку `packages/` (пути `HintPath` в csproj указывают на `..\packages\...`):
  - `Pike.PythonClient64`: `pythonnet 3.0.5`, `Microsoft.CSharp 4.7.0`, `System.Reflection.Emit 4.7.0`;
  - `Pike.PythonClient.Test`: `MSTest.TestAdapter 2.2.10`, `MSTest.TestFramework 2.2.10`.
- Собрано решение в конфигурации `Debug|Any CPU`: **успешно**.
  - `Pike.PythonClient64 -> Pike.PythonClient64\bin\Debug\Pike.PythonClient64.dll`
  - `Pike.PythonClient.Test -> Pike.PythonClient.Test\bin\Debug\Pike.PythonClient.Test.dll`
  - Baseline-предупреждение (1 шт.): `MSB3270` — тестовый проект `AnyCPU` ссылается на x64-сборку провайдера.
    Планируется устранить при переводе тестового проекта на `PlatformTarget=x64` (этап 6).

**Baseline-прогон тестов**

- `vstest.console.exe ... '/TestCaseFilter:TestCategory=Unit'` → «Нет тестов, соответствующих указанному
  фильтру»: категорий в существующих тестах нет. Это ожидаемо; Unit-набор появится на этапе 1.
- Существующие тесты (`Basic`, `Connection`, `DataProvider`, `DataQuery`, `DbProviderFactory`) требуют
  установленного Python (`SettingsMain.Default.PythonDllPath` = `C:\Users\Pike\anaconda3\python311.dll`)
  и в текущем окружении не запускаются. Исправление путей/категорий — этап 6 (P1-6).

### 0.2 Журнал `steps.md` — ✅ (2026-10-29)

Создан этот файл; ведётся после каждого шага (дата, файлы, команды, результат).

---

<!-- Новые записи добавляются ниже в формате:

## Этап N. <название>

### N.M <шаг> — ✅ (YYYY-MM-DD)

**Изменённые файлы:** ...
**Сделано:** ...
**Проверка:** ...
-->
