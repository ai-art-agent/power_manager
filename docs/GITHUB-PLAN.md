# План публикации Power Manager на GitHub

Пошаговый план, чтобы выложить проект на GitHub и сделать его удобным для других пользователей.

---

## 1. Подготовка репозитория (уже сделано)

- [x] **.gitignore** — исключены `bin/`, `obj/`, `*.user`, результаты тестов (`PowerSchemeTest-Results.csv`), служебные папки.
- [x] **Удалены лишние файлы** — `PowerSchemeTest-Results.csv`, `docs/dual-boot-ubuntu.md` (не по теме электропитания/виджета).
- [x] **README.md** — описание проекта, быстрый старт, требования, структура, сборка.
- [x] **docs/GETTING-STARTED.md** — пошаговая инструкция от клонирования до запуска виджета.
- [x] **docs/power-schemes.md** — описание схем и параметров powercfg; добавлена связь с виджетом.
- [x] **scheme-guids.json** — оставлен в репозитории как пример; скрипт `Install-PowerManagerSchemes.ps1` перезапишет его при установке схем на машине пользователя.

---

## 2. Перед первым push

1. **Заменить placeholder в README и GETTING-STARTED**  
   Найти `YOUR_USERNAME` и подставить свой логин GitHub (или имя организации), чтобы ссылки «Клонировать» вели на ваш репозиторий.

2. **Лицензия**  
   Создать файл `LICENSE` в корне (например, MIT или Apache-2.0). На GitHub: Repository → Add file → Create new file → имя файла `LICENSE` → выбрать шаблон лицензии.

3. **Проверка сборки**  
   Убедиться, что на чистой копии собирается решение и запускается виджет:
   ```powershell
   dotnet build PowerManager.sln -c Release
   dotnet run --project src\PowerManagerWidget\PowerManagerWidget.csproj
   ```

4. **Опционально: пример scheme-guids.json**  
   Текущий `scheme-guids.json` содержит GUID с вашей машины. После `Install-PowerManagerSchemes.ps1` у пользователя будут свои GUID. Можно оставить файл как есть (скрипт его перезапишет) или добавить в README примечание: «После установки схем файл будет перезаписан».

---

## 3. Создание репозитория на GitHub

1. Войти на [github.com](https://github.com).
2. **New repository** (или «Create a new repository»).
3. Указать имя, например: `power_manager` или `PowerManager-Widget`.
4. Описание (short): например, «Windows power schemes widget: custom plans, CPU monitoring, brightness, battery».
5. Выбрать **Public**.
6. **Не** отмечать «Add a README» (README уже есть локально).
7. Создать репозиторий.

---

## 4. Первый push (из папки проекта)

Если Git ещё не инициализирован:

```powershell
cd C:\Users\AI_Art\power_manager
git init
git add .
git commit -m "Initial commit: Power Manager widget and power scheme scripts"
git branch -M main
git remote add origin https://github.com/YOUR_USERNAME/power_manager.git
git push -u origin main
```

Если репозиторий уже инициализирован:

```powershell
git add .
git status   # проверить, что нет лишнего (bin, obj не должны попасть)
git commit -m "Prepare for GitHub: README, GETTING-STARTED, .gitignore, cleanup"
git remote add origin https://github.com/YOUR_USERNAME/power_manager.git
git push -u origin main
```

Заменить `YOUR_USERNAME` и `power_manager` на свои.

---

## 5. После публикации

1. **Описание и темы репозитория**  
   В настройках репозитория (About → Edit) добавить ссылку на сайт (если есть), описание и теги, например: `windows`, `power-management`, `wpf`, `dotnet`, `widget`, `librehardwaremonitor`.

2. **Releases (опционально)**  
   Для пользователей без .NET SDK можно собрать exe и выложить в **Releases**:
   - Собрать: `dotnet publish src\PowerManagerWidget\PowerManagerWidget.csproj -c Release -r win-x64 --self-contained false`
   - В папке `publish` будут exe и зависимости; упаковать в ZIP вместе с `scheme-guids.json`.
   - Создать новый Release (например, v1.0.0), прикрепить ZIP.

3. **Документация в вики или в README**  
   Текущих документов (README, GETTING-STARTED, power-schemes, power-widget-test) достаточно для старта. При желании можно вынести «Устранение неполадок» в отдельную страницу или в вики GitHub.

---

## 6. Краткий чек-лист перед публикацией

- [ ] .gitignore не допускает попадания bin/obj в репозиторий
- [ ] README содержит актуальные команды и ссылки на ваш репозиторий
- [ ] GETTING-STARTED описывает шаги от клонирования до запуска виджета
- [ ] scheme-guids.json присутствует (как пример/шаблон)
- [ ] Лицензия (LICENSE) добавлена
- [ ] В README указана выбранная лицензия
- [ ] Локально выполняется `dotnet build` и виджет запускается после установки схем

После этого репозиторий готов к публикации на GitHub.
