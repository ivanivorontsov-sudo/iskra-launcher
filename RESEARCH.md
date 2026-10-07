# Исследование перед разработкой

## Аналоги
- **Flow Launcher / PowerToys Run / Command Palette** — окно-поиск по Alt+Space, нечёткий поиск, плагины.
  Взято: модель «горячая клавиша → поиск → ранжирование», нечёткий матчер в духе fzf, веб-поиск и калькулятор.
- **Raycast (Windows, 2025–2026)** — аккуратная карточка, действия над результатом. Взято: единая строка, действия
  (открыть / от администратора / расположение / закрепить), «обучение» на выборе.
- **Seelen UI / Rainmeter / TranslucentTB** — оформление рабочего стола и панели задач. Взято: идея собственной
  панели-дока и прозрачность через `SetWindowCompositionAttribute`/DWM.
- **Open-Shell / StartAllBack / Start11 / ExplorerPatcher** — замена «Пуска» и панели задач.
  Open-Shell перехватывает клавишу Win; StartAllBack/ExplorerPatcher внедряются в Проводник (мы это НЕ делаем).
- **Mica For Everyone** — применяет Mica/Acrylic к чужим окнам через `DwmSetWindowAttribute`. Взято напрямую:
  наблюдатель окон (`DwmWatcher`) оформляет Проводник и диалоги без внедрения кода.
- **Windhawk / SecureUxTheme / msstyles** — глубокая тема оболочки патчем `uxtheme`/сторонними стилями.
  Сознательно НЕ используем: это патчинг подписанных системных компонентов.

## Техники WinAPI
- **Mica/Acrylic**: `DwmSetWindowAttribute(DWMWA_SYSTEMBACKDROP_TYPE=38, DWMSBT_MAINWINDOW/TRANSIENT/TABBED)` +
  `DwmExtendFrameIntoClientArea(-1)`; тёмная рамка — `DWMWA_USE_IMMERSIVE_DARK_MODE=20`; углы — `DWMWA_WINDOW_CORNER_PREFERENCE=33`.
- **Перехват Win**: низкоуровневый хук `WH_KEYBOARD_LL`, на отпускании одиночного Win впрыскиваем «пустую» клавишу
  `0xFF` (приём PowerToys), затем открываем своё меню; Win+<клавиша> не трогаем.
- **Глобальные горячие клавиши**: `RegisterHotKey` + `HwndSource`; конфликт — неуспешная регистрация + список
  заведомо системных сочетаний.
- **Панель задач**: прозрачность/акрил — `SetWindowCompositionAttribute(WCA_ACCENT_POLICY)`; автоскрытие —
  `SHAppBarMessage(ABM_SETSTATE)`. Перекраска XAML-панели Windows 11 возможна только внедрением в Проводник (как TranslucentTB
  ExplorerTAP) — мы этого не делаем; предлагаем свой док.
- **Обои**: `IDesktopWallpaper` (COM) + запасной `SystemParametersInfo(SPI_SETDESKWALLPAPER)`.
- **Экран блокировки**: `Windows.System.UserProfile.LockScreen.SetImageFileAsync` (x64, без администратора).
- **Акцент/цвета**: HKCU `…\DWM`, `…\Explorer\Accent\AccentPalette`, `…\Themes\Personalize` через .NET Registry API.
- **UCPD.sys**: `TaskbarDa`, `ShellFeedsTaskbarViewMode` и т. п. защищены драйвером UserChoice и НЕ меняются
  сторонним процессом — используем только то, что разрешено (`TaskbarAl`, акцент, персонализация).

## Почему .NET 10 + WPF
Прямой доступ к DWM/хукам/COM, малый расход в простое, установка per-user без администратора, анимации на частоте
монитора. WinUI 3 удобнее для «магазинных» приложений, но сложнее для системных хуков и неупакованной per-user установки.

## Ограничения (честно)
Внутренний UI «Параметров»/Store/WinUI рисует Windows; встроенную панель задач без внедрения в Проводник не
перекрасить; окно входа, Win+X, центр уведомлений — системные; смена системного шрифта затрагивает только классические
части Windows и требует повторного входа.
