// Interface texts: Russian, English, Ukrainian. S.T(key) / S.F(key, args).
using System;
using System.Collections.Generic;
using System.Globalization;

static class S
{
    static readonly string[] Codes = new string[] { "ru", "en", "uk" };
    static int lang = 1;
    static readonly Dictionary<string, string[]> D = new Dictionary<string, string[]>();

    public static int LangIndex { get { return lang; } }
    public static string Code { get { return Codes[lang]; } }

    public static void SetLang(int i) { lang = Math.Max(0, Math.Min(2, i)); }

    public static void Init()
    {
        string saved = Settings.Get("lang", "");
        int idx = Array.IndexOf(Codes, saved);
        if (idx < 0)
        {
            string two = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            idx = two == "ru" ? 0 : (two == "uk" ? 2 : 1);
        }
        lang = idx;
        if (D.Count == 0) Fill();
    }

    public static string T(string key)
    {
        if (D.Count == 0) Fill();
        string[] v;
        if (!D.TryGetValue(key, out v)) return key;
        string s = v[lang];
        return string.IsNullOrEmpty(s) ? v[1] : s;
    }

    public static string F(string key, params object[] args)
    {
        try { return string.Format(CultureInfo.CurrentCulture, T(key), args); } catch (FormatException) { return T(key); }
    }

    public static IEnumerable<string> Keys { get { if (D.Count == 0) Fill(); return D.Keys; } }
    public static string Raw(string key, int l) { return D[key][l]; }

    static void A(string key, string ru, string en, string uk) { D[key] = new string[] { ru, en, uk }; }

    static void Fill()
    {
        // ---- general ----
        A("app.title", "Windows Installer", "Windows Installer", "Windows Installer");
        A("tab.install", "Установка", "Install", "Встановлення");
        A("tab.capture", "Захват", "Capture", "Захоплення");
        A("tab.about", "О программе", "About", "Про програму");
        A("hdr.theme", "Светлая / тёмная тема", "Light / dark theme", "Світла / темна тема");
        A("hdr.upd.avail", "Доступна {0}", "{0} available", "Доступна {0}");
        A("hdr.upd.ready", "Обновить до {0} и перезапустить", "Update to {0} and restart", "Оновити до {0} і перезапустити");
        A("hdr.upd.dl", "Загрузка обновления {0}%", "Downloading update {0}%", "Завантаження оновлення {0}%");
        A("main.busy", "Идёт операция. Дождитесь её завершения.", "An operation is running. Please wait for it to finish.", "Триває операція. Дочекайтеся її завершення.");
        A("u.gb", "ГБ", "GB", "ГБ");
        A("u.tb", "ТБ", "TB", "ТБ");
        A("dlg.ok", "OK", "OK", "OK");
        A("dlg.yes", "Продолжить", "Continue", "Продовжити");
        A("dlg.no", "Отмена", "Cancel", "Скасувати");
        A("dlg.cancel", "Отмена", "Cancel", "Скасувати");
        A("dlg.type", "Для продолжения введите: {0}", "To continue, type: {0}", "Щоб продовжити, введіть: {0}");

        // ---- disks ----
        A("disk.col.model", "Модель", "Model", "Модель");
        A("disk.col.size", "Размер", "Size", "Розмір");
        A("disk.col.bus", "Шина", "Bus", "Шина");
        A("disk.col.style", "Стиль", "Style", "Стиль");
        A("disk.col.volumes", "Тома", "Volumes", "Томи");
        A("disk.col.status", "Статус", "Status", "Статус");
        A("disk.system", "СИСТЕМНЫЙ", "SYSTEM", "СИСТЕМНИЙ");
        A("disk.source", "ТУТ ОБРАЗ", "HOLDS IMAGE", "ТУТ ОБРАЗ");
        A("disk.usb", "USB", "USB", "USB");
        A("disk.err", "Не удалось получить список дисков:", "Could not read the disk list:", "Не вдалося отримати список дисків:");

        // ---- images ----
        A("img.noname", "Образ без имени", "Unnamed image", "Образ без назви");
        A("img.index", "индекс", "index", "індекс");
        A("img.noinstall", "В образе нет sources\\install.wim / install.esd - это не установочный образ Windows (или install.swm, он не поддерживается).",
            "The image has no sources\\install.wim / install.esd - it is not a Windows setup image (or it is install.swm, which is not supported).",
            "В образі немає sources\\install.wim / install.esd - це не інсталяційний образ Windows (або install.swm, він не підтримується).");
        A("img.cantmount", "В этой среде нельзя смонтировать ISO, и не найден 7-Zip.\r\nПодключите ISO средствами системы (правый клик - Подключить), затем выберите файл <буква>:\\sources\\install.wim кнопкой «ISO / WIM...».",
            "This environment cannot mount the ISO and 7-Zip was not found.\r\nMount the ISO with the system (right-click - Mount), then pick <letter>:\\sources\\install.wim with the \"ISO / WIM...\" button.",
            "У цьому середовищі не можна змонтувати ISO, і не знайдено 7-Zip.\r\nПідключіть ISO засобами системи (правий клік - Підключити), потім виберіть файл <літера>:\\sources\\install.wim кнопкою «ISO / WIM...».");
        A("img.nospace", "На диске {0} мало места для распаковки install.wim.", "Not enough free space on {0} to extract install.wim.", "На диску {0} мало місця для розпакування install.wim.");
        A("img.extracting", "Распаковываю install.wim из ISO (может занять несколько минут)...", "Extracting install.wim from the ISO (may take a few minutes)...", "Розпаковую install.wim з ISO (може тривати кілька хвилин)...");
        A("img.dismfail", "DISM не смог прочитать образ:", "DISM could not read the image:", "DISM не зміг прочитати образ:");

        // ---- install page ----
        A("inst.card1", "Образ Windows", "Windows image", "Образ Windows");
        A("inst.card2", "Диск для установки (будет полностью очищен)", "Target disk (will be erased completely)", "Диск для встановлення (буде повністю очищено)");
        A("inst.card3", "Параметры", "Options", "Параметри");
        A("inst.btn.file", "ISO / WIM...", "ISO / WIM...", "ISO / WIM...");
        A("inst.btn.folder", "Папка...", "Folder...", "Тека...");
        A("inst.btn.search", "Найти ISO", "Find ISO", "Знайти ISO");
        A("inst.btn.refresh", "Обновить", "Refresh", "Оновити");
        A("inst.btn.browse", "Обзор...", "Browse...", "Огляд...");
        A("inst.btn.install", "УСТАНОВИТЬ WINDOWS", "INSTALL WINDOWS", "ВСТАНОВИТИ WINDOWS");
        A("inst.folder.ph", "Папка поиска", "Search folder", "Тека пошуку");
        A("inst.drivers.ph", "Папка с драйверами (необязательно)", "Drivers folder (optional)", "Тека з драйверами (необов'язково)");
        A("inst.edition", "Редакция:", "Edition:", "Редакція:");
        A("inst.edition.ph", "Сначала выберите образ", "Pick an image first", "Спочатку виберіть образ");
        A("inst.diskhint", "Серые диски (системный и с образом) выбрать нельзя.", "Grey disks (system and the one holding the image) cannot be chosen.", "Сірі диски (системний і з образом) вибрати не можна.");
        A("inst.diskblocked", "Этот диск выбрать нельзя (системный или на нём образ/программа).", "This disk cannot be chosen (system disk, or it holds the image/app).", "Цей диск вибрати не можна (системний або на ньому образ/програма).");
        A("inst.thispc", "Этот ПК:", "This PC:", "Цей ПК:");
        A("inst.recovery", "Раздел восстановления WinRE (1 ГБ)", "WinRE recovery partition (1 GB)", "Розділ відновлення WinRE (1 ГБ)");
        A("inst.size.all", "Весь диск", "Whole disk", "Весь диск");
        A("inst.size.custom", "Свой размер Windows:", "Custom Windows size:", "Власний розмір Windows:");
        A("inst.data", "Остаток - раздел «Data»", "Rest of the disk - \"Data\" volume", "Залишок - розділ «Data»");
        A("inst.drivers", "Драйверы:", "Drivers:", "Драйвери:");
        A("inst.ready", "Выберите образ, диск и параметры.", "Choose an image, a disk and options.", "Виберіть образ, диск і параметри.");
        A("inst.pickfolder", "Папка с ISO-файлами", "Folder with ISO files", "Тека з ISO-файлами");
        A("inst.pickdrivers", "Папка с драйверами (с файлами .inf)", "Drivers folder (with .inf files)", "Тека з драйверами (з файлами .inf)");
        A("inst.pickimage", "Выберите образ Windows (ISO / WIM / ESD)", "Choose a Windows image (ISO / WIM / ESD)", "Виберіть образ Windows (ISO / WIM / ESD)");
        A("inst.filter", "Образы Windows (*.iso;*.wim;*.esd)", "Windows images (*.iso;*.wim;*.esd)", "Образи Windows (*.iso;*.wim;*.esd)");
        A("inst.searching", "Поиск ISO...", "Searching for ISO files...", "Пошук ISO...");
        A("inst.found", "Найдено ISO: {0}", "ISO files found: {0}", "Знайдено ISO: {0}");
        A("inst.noiso", "ISO-файлы не найдены.", "No ISO files found.", "ISO-файли не знайдено.");
        A("inst.opening", "Открываю образ...", "Opening the image...", "Відкриваю образ...");
        A("inst.opened", "Образ открыт: {0}", "Image opened: {0}", "Образ відкрито: {0}");
        A("inst.openfail", "Не удалось открыть образ.", "Could not open the image.", "Не вдалося відкрити образ.");
        A("inst.need", "Осталось выбрать:", "Still to choose:", "Залишилося вибрати:");
        A("inst.need.image", "образ", "image", "образ");
        A("inst.need.edition", "редакцию", "edition", "редакцію");
        A("inst.need.disk", "диск", "disk", "диск");
        A("inst.sum.size", "Windows {0} ГБ", "Windows {0} GB", "Windows {0} ГБ");
        A("inst.sum.all", "Windows на весь диск", "Windows on the whole disk", "Windows на весь диск");
        A("inst.sum.data", "раздел Data {0} ГБ", "Data volume {0} GB", "розділ Data {0} ГБ");
        A("inst.sum.disk", "Диск {0} ({1}, {2})", "Disk {0} ({1}, {2})", "Диск {0} ({1}, {2})");
        A("inst.sum.erase", "Диск будет полностью очищен.", "The disk will be erased completely.", "Диск буде повністю очищено.");
        A("inst.warn.mbr2tb", "MBR использует только первые 2 ТБ диска. Продолжить?", "MBR can use only the first 2 TB of a disk. Continue?", "MBR використовує лише перші 2 ТБ диска. Продовжити?");
        A("inst.warn.small", "{0} ГБ - очень мало. Windows 10 требует минимум 32 ГБ, Windows 11 - 64 ГБ, а с обновлениями и программами нужно больше. Продолжить?",
            "{0} GB is very little. Windows 10 needs at least 32 GB, Windows 11 - 64 GB, and updates and programs need more. Continue?",
            "{0} ГБ - дуже мало. Windows 10 потребує щонайменше 32 ГБ, Windows 11 - 64 ГБ, а з оновленнями і програмами потрібно більше. Продовжити?");
        A("inst.confirm.title", "ПОДТВЕРЖДЕНИЕ - данные будут удалены", "CONFIRM - data will be erased", "ПІДТВЕРДЖЕННЯ - дані буде видалено");
        A("inst.confirm.warn", "ВСЕ ДАННЫЕ НА ДИСКЕ БУДУТ БЕЗВОЗВРАТНО УДАЛЕНЫ!", "ALL DATA ON THE DISK WILL BE ERASED IRREVERSIBLY!", "УСІ ДАНІ НА ДИСКУ БУДЕ БЕЗПОВОРОТНО ВИДАЛЕНО!");
        A("inst.confirm.disk", "Диск {0}: {1}", "Disk {0}: {1}", "Диск {0}: {1}");
        A("inst.confirm.size", "Размер: {0}, интерфейс: {1}", "Size: {0}, interface: {1}", "Розмір: {0}, інтерфейс: {1}");
        A("inst.confirm.scheme", "Схема:", "Scheme:", "Схема:");
        A("inst.confirm.edition", "Устанавливается:", "Installing:", "Встановлюється:");
        A("inst.confirm.ok", "Стереть и установить", "Erase and install", "Стерти і встановити");
        A("inst.step1", "Шаг 1/4: разметка диска...", "Step 1/4: partitioning the disk...", "Крок 1/4: розмітка диска...");
        A("inst.step2", "Шаг 2/4: копирование файлов Windows (самый долгий шаг)...", "Step 2/4: copying Windows files (the longest step)...", "Крок 2/4: копіювання файлів Windows (найдовший крок)...");
        A("inst.step3", "Добавляю драйверы...", "Adding drivers...", "Додаю драйвери...");
        A("inst.step4", "Шаг 3/4: создание загрузчика...", "Step 3/4: creating the boot files...", "Крок 3/4: створення завантажувача...");
        A("inst.log.part", "Разметка диска", "Partitioning", "Розмітка диска");
        A("inst.log.apply", "Применение образа", "Applying the image", "Застосування образу");
        A("inst.log.drivers", "Драйверы", "Drivers", "Драйвери");
        A("inst.log.boot", "Загрузчик", "Boot files", "Завантажувач");
        A("inst.log.error", "ОШИБКА:", "ERROR:", "ПОМИЛКА:");
        A("inst.retry", "Разметка не удалась, повторяю через 5 секунд...", "Partitioning failed, retrying in 5 seconds...", "Розмітка не вдалася, повторюю через 5 секунд...");
        A("inst.done", "Готово!", "Done!", "Готово!");
        A("inst.failed", "ОШИБКА", "ERROR", "ПОМИЛКА");
        A("inst.done.msg", "Windows установлена на диск {0}.\r\n\r\nЗагрузитесь с этого диска (при необходимости выберите его в Boot Menu). При первой загрузке пройдёт начальная настройка Windows.",
            "Windows is installed on disk {0}.\r\n\r\nBoot from that disk (use the Boot Menu if needed). Windows setup will finish on the first start.",
            "Windows встановлено на диск {0}.\r\n\r\nЗавантажтеся з цього диска (за потреби оберіть його в Boot Menu). Під час першого запуску відбудеться початкове налаштування Windows.");
        A("inst.warn.drivers", "ВНИМАНИЕ: часть драйверов не добавилась.", "WARNING: some drivers could not be added.", "УВАГА: частину драйверів не вдалося додати.");
        A("inst.warn.winre", "WinRE не настроен:", "WinRE was not configured:", "WinRE не налаштовано:");
        A("inst.err.incomplete", "Выберите образ, редакцию и диск.", "Choose an image, an edition and a disk.", "Виберіть образ, редакцію і диск.");
        A("inst.err.size", "Размер раздела Windows: не меньше 20 ГБ.", "Windows partition size: at least 20 GB.", "Розмір розділу Windows: не менше 20 ГБ.");
        A("inst.err.toobig", "Раздел {0} ГБ не помещается: на диске {1} ГБ (ещё ~{2} ГБ нужно под служебные разделы).",
            "A {0} GB partition does not fit: the disk is {1} GB (about {2} GB more is needed for system partitions).",
            "Розділ {0} ГБ не вміщується: на диску {1} ГБ (ще ~{2} ГБ потрібно під службові розділи).");
        A("inst.err.drivers", "Папка с драйверами не найдена.", "The drivers folder was not found.", "Теку з драйверами не знайдено.");
        A("inst.err.letters", "Не хватает свободных букв дисков.", "Not enough free drive letters.", "Не вистачає вільних літер дисків.");
        A("inst.err.diskpart", "diskpart завершился с ошибкой (код {0}).", "diskpart failed (exit code {0}).", "diskpart завершився з помилкою (код {0}).");
        A("inst.err.novol", "Раздел {0}: не появился после разметки.", "Volume {0}: did not appear after partitioning.", "Розділ {0}: не з'явився після розмітки.");
        A("inst.err.apply", "DISM /Apply-Image завершился с кодом {0}.", "DISM /Apply-Image failed (exit code {0}).", "DISM /Apply-Image завершився з кодом {0}.");
        A("inst.err.nowindows", "В выбранном образе нет папки Windows - это не системный образ (возможно, выбран индекс с данными или WinPE). Выберите другую редакцию.",
            "The selected image has no Windows folder - it is not a system image (maybe a data index or WinPE). Choose another edition.",
            "У вибраному образі немає папки Windows - це не системний образ (можливо, вибрано індекс з даними або WinPE). Виберіть іншу редакцію.");
        A("inst.err.bcd", "bcdboot не смог создать загрузчик.", "bcdboot could not create the boot files.", "bcdboot не зміг створити завантажувач.");

        // ---- capture page ----
        A("cap.card.checks", "Проверка системы", "System check", "Перевірка системи");
        A("cap.card.dest", "Куда сохранить образ", "Where to save the image", "Куди зберегти образ");
        A("cap.card.actions", "Действия", "Actions", "Дії");
        A("cap.card.capture", "Снять образ Windows", "Capture a Windows image", "Зняти образ Windows");
        A("cap.card.help", "Что дальше", "What next", "Що далі");
        A("cap.btn.check", "Проверить", "Check", "Перевірити");
        A("cap.btn.browse", "Обзор...", "Browse...", "Огляд...");
        A("cap.btn.refresh", "Обновить", "Refresh", "Оновити");
        A("cap.btn.cmd", "Только capture.cmd", "capture.cmd only", "Лише capture.cmd");
        A("cap.btn.run", "SYSPREP И ВЫКЛЮЧИТЬ ПК", "SYSPREP AND SHUT DOWN", "SYSPREP І ВИМКНУТИ ПК");
        A("cap.btn.capture", "СНЯТЬ ОБРАЗ", "CAPTURE IMAGE", "ЗНЯТИ ОБРАЗ");
        A("cap.dest", "Папка:", "Folder:", "Тека:");
        A("cap.name", "Имя:", "Name:", "Ім'я:");
        A("cap.comp", "Сжатие:", "Compress:", "Стиснення:");
        A("cap.comp.max", "Максимальное", "Maximum", "Максимальне");
        A("cap.comp.fast", "Быстрое", "Fast", "Швидке");
        A("cap.comp.none", "Без сжатия", "None", "Без стиснення");
        A("cap.opt.admin", "Оставить встроенного Администратора включённым", "Keep the built-in Administrator enabled", "Залишити вбудованого Адміністратора ввімкненим");
        A("cap.opt.xml", "Пропустить создание новой учётки (файл ответов)", "Skip creating a new account (answer file)", "Пропустити створення нового облікового запису");
        A("cap.opt.keep", "Страховка ярлыков рабочего стола и «Пуск»", "Safety net for desktop and Start shortcuts", "Страховка ярликів робочого столу та «Пуск»");
        A("cap.pickfolder", "Папка для образа (на внешнем диске)", "Folder for the image (on an external drive)", "Тека для образу (на зовнішньому диску)");
        A("cap.ready", "Готов.", "Ready.", "Готово.");
        A("cap.checking", "Проверяю систему...", "Checking the system...", "Перевіряю систему...");
        A("cap.src", "Windows:", "Windows:", "Windows:");
        A("cap.src.none", "Windows не найдена", "No Windows found", "Windows не знайдено");
        A("cap.nowindows", "Windows не найдена ни на одном диске.", "No Windows found on any drive.", "Windows не знайдено на жодному диску.");
        A("cap.used", "занято", "used", "зайнято");
        A("cap.state.reading", "Читаю состояние Sysprep...", "Reading the Sysprep state...", "Читаю стан Sysprep...");
        A("cap.state.ok", "Sysprep выполнен: система обобщена. Можно снимать.", "Sysprep done: the system is generalized. Ready to capture.", "Sysprep виконано: систему узагальнено. Можна знімати.");
        A("cap.state.no", "Sysprep НЕ выполнен (состояние {0}). Образ будет точной копией системы без первоначальной настройки.", "Sysprep NOT done (state {0}). The image will be an exact copy of the system, without first-run setup.", "Sysprep НЕ виконано (стан {0}). Образ буде точною копією системи без початкового налаштування.");
        A("cap.state.unk", "Состояние Sysprep определить не удалось.", "Could not determine the Sysprep state.", "Не вдалося визначити стан Sysprep.");
        A("cap.tag.ok", "OK", "OK", "OK");
        A("cap.tag.warn", "!", "!", "!");
        A("cap.tag.bad", "СТОП", "STOP", "СТОП");
        A("cap.build", "сборка", "build", "збірка");
        A("cap.enabled", "включена", "enabled", "увімкнено");
        A("cap.disabled", "отключена", "disabled", "вимкнено");
        A("cap.builtin", "встроенный администратор", "built-in administrator", "вбудований адміністратор");
        A("cap.chk.winpe", "Запущено в WinPE - Sysprep здесь невозможен. Запустите в обычной Windows.", "Running in WinPE - Sysprep is not possible here. Run in a normal Windows.", "Запущено у WinPE - Sysprep тут неможливий. Запустіть у звичайній Windows.");
        A("cap.chk.bitlocker", "BitLocker включён на {0}: manage-bde -off {0} и дождитесь расшифровки.", "BitLocker is on for {0}: run manage-bde -off {0} and wait for decryption.", "BitLocker увімкнено на {0}: manage-bde -off {0} і дочекайтеся розшифрування.");
        A("cap.chk.bitlocker.ok", "BitLocker выключен.", "BitLocker is off.", "BitLocker вимкнено.");
        A("cap.chk.bitlocker.unk", "BitLocker: проверить не удалось.", "BitLocker: could not check.", "BitLocker: перевірити не вдалося.");
        A("cap.chk.domain", "ПК в домене. Для Sysprep выйдите из домена.", "The PC is in a domain. Leave the domain before Sysprep.", "ПК у домені. Для Sysprep вийдіть з домену.");
        A("cap.chk.domain.ok", "ПК не в домене.", "The PC is not in a domain.", "ПК не в домені.");
        A("cap.chk.reboot", "Ожидается перезагрузка. Перезагрузите ПК и повторите проверку.", "A restart is pending. Restart the PC and check again.", "Очікується перезавантаження. Перезавантажте ПК і повторіть перевірку.");
        A("cap.chk.reboot.ok", "Отложенных перезагрузок нет.", "No pending restarts.", "Відкладених перезавантажень немає.");
        A("cap.chk.account", "Учётка:", "Account:", "Обліковий запис:");
        A("cap.chk.apps", "Приложений Store от пользователя: {0}. Sysprep может отказаться (при ошибке предложим удалить).", "User-installed Store apps: {0}. Sysprep may refuse (we offer to remove them on failure).", "Програм Store від користувача: {0}. Sysprep може відмовитись (при помилці запропонуємо видалити).");
        A("cap.chk.apps.ok", "Проблемных Store-приложений нет.", "No problematic Store apps.", "Проблемних програм Store немає.");
        A("cap.chk.apps.unk", "Store-приложения: проверить не удалось.", "Store apps: could not check.", "Програми Store: перевірити не вдалося.");
        A("cap.chk.other", "Программ НЕ на {0}: {1}. В образ не попадут.", "Programs NOT on {0}: {1}. Not included in the image.", "Програм НЕ на {0}: {1}. В образ не потраплять.");
        A("cap.chk.other.ok", "Все программы лежат на {0}.", "All programs are on {0}.", "Усі програми лежать на {0}.");
        A("cap.chk.lnk", "Ярлыков на рабочем столе", "Desktop shortcuts in", "Ярликів на робочому столі");
        A("cap.chk.lnk.off", "не на {0}, работать не будет", "not on {0}, will not work", "не на {0}, не працюватиме");
        A("cap.chk.lnk.unk", "Ярлыки: проверить не удалось.", "Shortcuts: could not check.", "Ярлики: перевірити не вдалося.");
        A("cap.chk.onedrive", "Рабочий стол в OneDrive: файлы только в облаке в образ не попадут.", "Desktop is in OneDrive: cloud-only files will not be in the image.", "Робочий стіл в OneDrive: файли лише в хмарі в образ не потраплять.");
        A("cap.chk.rearm", "Осталось сбросов активации (rearm):", "Remaining activation resets (rearm):", "Залишилося скидань активації (rearm):");
        A("cap.chk.activation", "Активация привязана к железу: на других ПК активируйте заново.", "Activation is tied to hardware: activate again on other PCs.", "Активація прив'язана до заліза: на інших ПК активуйте заново.");
        A("cap.err.blockers", "Сначала устраните проблемы из проверки (СТОП).", "Fix the problems marked STOP in the check first.", "Спочатку усуньте проблеми з перевірки (СТОП).");
        A("cap.err.dest", "Укажите папку на локальном диске, например E:\\Images.", "Enter a folder on a local drive, e.g. E:\\Images.", "Вкажіть теку на локальному диску, наприклад E:\\Images.");
        A("cap.err.sysdrive", "Образ нельзя сохранять на системный диск {0}: он и будет захватываться.", "The image cannot be saved on the system drive {0}: that is what gets captured.", "Образ не можна зберігати на системному диску {0}: саме він знімається.");
        A("cap.err.nodrive", "Диск {0} недоступен.", "Drive {0} is not available.", "Диск {0} недоступний.");
        A("cap.err.nosrc", "Выберите Windows для снятия.", "Choose the Windows to capture.", "Виберіть Windows для зняття.");
        A("cap.err.samedrive", "Образ нельзя сохранять на тот же диск ({0}), который снимается.", "The image cannot be saved on the same drive ({0}) that is being captured.", "Образ не можна зберігати на тому самому диску ({0}), який знімається.");
        A("cap.err.ram", "X: - это оперативная память WinPE. Выберите внешний диск.", "X: is the WinPE RAM disk. Choose an external drive.", "X: - це оперативна пам'ять WinPE. Виберіть зовнішній диск.");
        A("cap.err.dism", "DISM завершился с ошибкой (код {0}). Подробности в журнале.", "DISM failed (exit code {0}). See the log.", "DISM завершився з помилкою (код {0}). Подробиці в журналі.");
        A("cap.err.sysprep.unknown", "Sysprep не удался, причина не распознана. Смотрите C:\\Windows\\System32\\Sysprep\\Panther\\setuperr.log.", "Sysprep failed for an unknown reason. See C:\\Windows\\System32\\Sysprep\\Panther\\setuperr.log.", "Sysprep не вдався, причину не розпізнано. Дивіться C:\\Windows\\System32\\Sysprep\\Panther\\setuperr.log.");
        A("cap.warn.space", "На {0} свободно {1}, а занято на {2} {3}. Образ может не поместиться. Продолжить?", "{0} has {1} free, but {2} uses {3}. The image may not fit. Continue?", "На {0} вільно {1}, а зайнято на {2} {3}. Образ може не вміститися. Продовжити?");
        A("cap.capturing", "Снимаю {0} в {1} ...", "Capturing {0} into {1} ...", "Знімаю {0} у {1} ...");
        A("cap.done", "Готово!", "Done!", "Готово!");
        A("cap.failed", "ОШИБКА", "ERROR", "ПОМИЛКА");
        A("cap.done.msg", "Образ сохранён:\r\n{0}", "Image saved:\r\n{0}", "Образ збережено:\r\n{0}");
        A("cap.cmd.done", "Создан файл:\r\n{0}\r\n\r\nДля обычного установщика Windows запустите его из WinPE (Shift+F10).", "File created:\r\n{0}\r\n\r\nFor a stock Windows Setup WinPE, run it from there (Shift+F10).", "Створено файл:\r\n{0}\r\n\r\nДля звичайного інсталятора Windows запустіть його з WinPE (Shift+F10).");
        A("cap.confirm.title", "ПОДТВЕРЖДЕНИЕ - Sysprep", "CONFIRM - Sysprep", "ПІДТВЕРДЖЕННЯ - Sysprep");
        A("cap.confirm.body", "Сейчас будет выполнен SYSPREP /GENERALIZE, и ПК ВЫКЛЮЧИТСЯ.\r\n\r\n- Эта Windows станет «обобщённой»: при следующей загрузке с её диска начнётся первоначальная настройка.\r\n- Снимите образ СРАЗУ: загрузитесь в WinPE и запустите снятие образа.\r\n- Сделайте резервную копию важных данных и закройте все программы.",
            "SYSPREP /GENERALIZE will run now and the PC WILL SHUT DOWN.\r\n\r\n- This Windows becomes \"generalized\": the next boot from its disk starts first-run setup.\r\n- Capture the image RIGHT AWAY: boot WinPE and run the capture.\r\n- Back up important data and close all programs.",
            "Зараз буде виконано SYSPREP /GENERALIZE, і ПК ВИМКНЕТЬСЯ.\r\n\r\n- Ця Windows стане «узагальненою»: наступне завантаження з її диска почне початкове налаштування.\r\n- Зніміть образ ОДРАЗУ: завантажтеся у WinPE і запустіть зняття образу.\r\n- Зробіть резервну копію важливих даних і закрийте всі програми.");
        A("cap.confirm.ok", "Запустить Sysprep", "Run Sysprep", "Запустити Sysprep");
        A("cap.apps.ask", "Sysprep не может обобщить систему из-за приложений Store, установленных пользователем без provisioning:\r\n\r\n{0}\r\n\r\nУдалить их для всех пользователей и повторить? (Эти приложения Store будут потеряны.)",
            "Sysprep cannot generalize the system because of Store apps installed by a user without provisioning:\r\n\r\n{0}\r\n\r\nRemove them for all users and retry? (These Store apps will be lost.)",
            "Sysprep не може узагальнити систему через програми Store, встановлені користувачем без provisioning:\r\n\r\n{0}\r\n\r\nВидалити їх для всіх користувачів і повторити? (Ці програми Store буде втрачено.)");
        A("cap.log.cmd", "Создан {0}", "Created {0}", "Створено {0}");
        A("cap.log.admin", "SetupComplete.cmd: включение встроенного Администратора добавлено.", "SetupComplete.cmd: enabling the built-in Administrator added.", "SetupComplete.cmd: увімкнення вбудованого Адміністратора додано.");
        A("cap.log.keep.run", "Сохраняю ярлыки...", "Saving shortcuts...", "Зберігаю ярлики...");
        A("cap.log.keep", "Страховка ярлыков: сохранено папок - {0} (C:\\ProgramData\\ProfileKeeper)", "Shortcut safety net: folders saved - {0} (C:\\ProgramData\\ProfileKeeper)", "Страховка ярликів: збережено тек - {0} (C:\\ProgramData\\ProfileKeeper)");
        A("cap.log.xml", "Файл ответов: {0}", "Answer file: {0}", "Файл відповідей: {0}");
        A("cap.log.sysprep", "Запуск sysprep (попытка {0})...", "Starting sysprep (attempt {0})...", "Запуск sysprep (спроба {0})...");
        A("cap.sysprep.running", "Sysprep работает (может занять несколько минут)...", "Sysprep is running (may take a few minutes)...", "Sysprep працює (може тривати кілька хвилин)...");
        A("cap.sysprep.wait", "Sysprep отработал, ждём выключения ПК...", "Sysprep finished, waiting for the PC to power off...", "Sysprep відпрацював, чекаємо вимкнення ПК...");
        A("cap.sysprep.failed", "Sysprep завершился ошибкой. Журнал: C:\\Windows\\System32\\Sysprep\\Panther\\setuperr.log", "Sysprep failed. Log: C:\\Windows\\System32\\Sysprep\\Panther\\setuperr.log", "Sysprep завершився помилкою. Журнал: C:\\Windows\\System32\\Sysprep\\Panther\\setuperr.log");
        A("cap.sysprep.apps", "Мешающие приложения Store:", "Blocking Store apps:", "Програми Store, що заважають:");
        A("cap.sysprep.removed", "Удалено:", "Removed:", "Видалено:");
        A("cap.help.win", "1. «SYSPREP И ВЫКЛЮЧИТЬ ПК»: capture.cmd на внешний диск, Sysprep, выключение.\r\n2. Не включайте систему с её диска до снятия образа - иначе обобщение пропадёт.\r\n3. Загрузитесь в WinPE с этой программой: вкладка «Захват» снимет образ. Со стандартным установщиком: Shift+F10 и capture.cmd с внешнего диска.\r\n4. Готовый WIM ставится на вкладке «Установка» («ISO / WIM...»).", "1. \"SYSPREP AND SHUT DOWN\": capture.cmd to the external drive, Sysprep, power off.\r\n2. Do not boot the system from its own disk before capturing - the generalization would be lost.\r\n3. Boot WinPE with this program: the Capture tab captures the image. With a stock Windows Setup: Shift+F10 and run capture.cmd from the external drive.\r\n4. Install the finished WIM on the Install tab (\"ISO / WIM...\").", "1. «SYSPREP І ВИМКНУТИ ПК»: capture.cmd на зовнішній диск, Sysprep, вимкнення.\r\n2. Не вмикайте систему з її диска до зняття образу - інакше узагальнення зникне.\r\n3. Завантажтеся у WinPE з цією програмою: вкладка «Захоплення» зніме образ. Зі стандартним інсталятором: Shift+F10 і capture.cmd із зовнішнього диска.\r\n4. Готовий WIM встановлюється на вкладці «Встановлення» («ISO / WIM...»).");
        A("cap.help.winpe", "Тут снимается образ Windows, на которой уже выполнен Sysprep (вкладка «Захват» в обычной Windows).\r\n- Сохраняйте образ на другой (внешний) диск.\r\n- Без Sysprep образ - точная копия системы, без первой настройки.\r\n- Временные файлы DISM идут на диск назначения, а не в память WinPE.\r\n- Потом установите образ на вкладке «Установка».", "Here you capture a Windows that has already been through Sysprep (the Capture tab in a normal Windows).\r\n- Save the image to a different (external) drive.\r\n- Without Sysprep the image is an exact copy of the system, without first-run setup.\r\n- DISM temporary files go to the destination drive, not WinPE memory.\r\n- Then install the image on the Install tab.", "Тут знімається образ Windows, на якій уже виконано Sysprep (вкладка «Захоплення» у звичайній Windows).\r\n- Зберігайте образ на інший (зовнішній) диск.\r\n- Без Sysprep образ - точна копія системи, без першого налаштування.\r\n- Тимчасові файли DISM йдуть на диск призначення, а не в пам'ять WinPE.\r\n- Потім встановіть образ на вкладці «Встановлення».");

        A("cap.opt.admin.tip", "После первой настройки Windows отключает встроенного Администратора. Эта опция включит его обратно (через SetupComplete.cmd).", "Windows disables the built-in Administrator after first setup. This turns it back on (via SetupComplete.cmd).", "Після першого налаштування Windows вимикає вбудованого Адміністратора. Ця опція увімкне його знову (через SetupComplete.cmd).");
        A("cap.opt.xml.tip", "Создаёт unattend.xml: Windows пропустит экраны первой настройки (регион, соглашение, сеть, создание учётки) и сразу покажет вход в существующие учётки. Язык, раскладка и часовой пояс берутся с этой системы. Если сборка Windows игнорирует параметр, пройдёт обычная первая настройка.", "Creates unattend.xml: Windows skips the first-run screens (region, license, network, account creation) and shows the sign-in screen with the existing accounts. Language, keyboard and time zone are taken from this system. If the Windows build ignores it, the normal first-run setup runs.", "Створює unattend.xml: Windows пропустить екрани першого налаштування (регіон, ліцензія, мережа, створення облікового запису) і одразу покаже вхід в існуючі облікові записи. Мова, розкладка і часовий пояс беруться з цієї системи. Якщо збірка Windows ігнорує параметр, пройде звичайне перше налаштування.");
        A("cap.opt.keep.tip", "Ярлыки (.lnk/.url) рабочих столов и меню «Пуск» копируются в образ и возвращаются при первом входе каждого пользователя, если профиль окажется чистым. Существующие ярлыки не перезаписываются.", "Shortcuts (.lnk/.url) from desktops and the Start menu are copied into the image and restored at each user's first sign-in if the profile turns out clean. Existing shortcuts are never overwritten.", "Ярлики (.lnk/.url) робочих столів і меню «Пуск» копіюються в образ і повертаються при першому вході кожного користувача, якщо профіль виявиться чистим. Існуючі ярлики не перезаписуються.");
        // ---- about / updates ----
        A("about.card.app", "О программе", "About", "Про програму");
        A("about.card.upd", "Обновления", "Updates", "Оновлення");
        A("about.desc", "Установка Windows из ISO / WIM на выбранный диск (GPT/UEFI и MBR/BIOS) и снятие образа настроенной Windows. Работает в Windows и в WinPE.",
            "Installs Windows from ISO / WIM onto a chosen disk (GPT/UEFI and MBR/BIOS) and captures a configured Windows into an image. Works in Windows and in WinPE.",
            "Встановлення Windows з ISO / WIM на вибраний диск (GPT/UEFI та MBR/BIOS) і зняття образу налаштованої Windows. Працює у Windows і у WinPE.");
        A("about.env.os", "Система:", "System:", "Система:");
        A("about.env.mode", "Режим:", "Mode:", "Режим:");
        A("about.env.exe", "Программа:", "Program:", "Програма:");
        A("about.env.data", "Данные:", "Data:", "Дані:");
        A("about.repo", "Репозиторий", "Repository", "Репозиторій");
        A("about.releases", "Релизы", "Releases", "Релізи");
        A("about.check", "Проверить обновления", "Check for updates", "Перевірити оновлення");
        A("about.restart", "Перезапустить и обновить", "Restart and update", "Перезапустити й оновити");
        A("about.download", "Открыть страницу релиза", "Open the release page", "Відкрити сторінку релізу");
        A("upd.idle", "Проверка ещё не выполнялась.", "Not checked yet.", "Перевірку ще не виконано.");
        A("upd.checking", "Проверяю обновления...", "Checking for updates...", "Перевіряю оновлення...");
        A("upd.none", "У вас последняя версия.", "You have the latest version.", "У вас остання версія.");
        A("upd.available", "Доступна новая версия {0}.", "A new version {0} is available.", "Доступна нова версія {0}.");
        A("upd.downloading", "Скачиваю {0}: {1}%", "Downloading {0}: {1}%", "Завантажую {0}: {1}%");
        A("upd.ready", "Версия {0} скачана и проверена. Она установится при закрытии программы или по кнопке ниже.", "Version {0} is downloaded and verified. It installs when you close the program, or with the button below.", "Версію {0} завантажено і перевірено. Вона встановиться при закритті програми або за кнопкою нижче.");
        A("upd.error", "Не удалось обновиться:", "Update failed:", "Не вдалося оновитись:");
        A("upd.err.net", "нет подключения к GitHub", "cannot reach GitHub", "немає зв'язку з GitHub");
        A("upd.err.verify", "скачанный файл не прошёл проверку", "the downloaded file failed verification", "завантажений файл не пройшов перевірку");
        A("upd.dev", "Сборка для разработки: самообновление отключено.", "Development build: self-update is disabled.", "Збірка для розробки: самооновлення вимкнено.");
        A("upd.auto", "Новые версии скачиваются автоматически из релизов репозитория и ставятся при закрытии программы.", "New versions are downloaded automatically from the repository releases and installed when the program closes.", "Нові версії завантажуються автоматично з релізів репозиторію і встановлюються при закритті програми.");

        // ---- Windows settings (answer file + first-logon script) ----
        A("cap.opt.lnkall", "Ярлыки всех пользователей - на общий рабочий стол", "Shortcuts of all users - to the shared desktop", "Ярлики всіх користувачів - на спільний робочий стіл");
        A("cap.opt.lnkall.tip", "Ярлыки с рабочих столов всех пользователей (включая Public) собираются вместе и при первом входе кладутся на общий рабочий стол: их увидят все учётные записи, в том числе созданные позже. Работает вместе со страховкой ярлыков.", "Shortcuts from every user's desktop (including Public) are collected and put on the shared desktop at the first logon, so all accounts see them, including those created later. Works together with the shortcut safety net.", "Ярлики з робочих столів усіх користувачів (разом з Public) збираються разом і при першому вході кладуться на спільний робочий стіл: їх побачать усі облікові записи, зокрема створені пізніше. Працює разом зі страховкою ярликів.");
        A("cap.opt.tasks", "Сохранить планировщик задач", "Keep the Task Scheduler tasks", "Зберегти планувальник завдань");
        A("cap.opt.tasks.tip", "Перед Sysprep все ваши задачи (кроме системных Microsoft) выгружаются в образ. При первом запуске недостающие создаются заново, с заменой пользователей по имени. Результат пишется в ProgramData\\ProfileKeeper\\tasks_restore.log.", "Before Sysprep all your tasks (except the Microsoft system ones) are exported into the image. At the first start the missing ones are created again, with users matched by name. The result is logged to ProgramData\\ProfileKeeper\\tasks_restore.log.", "Перед Sysprep усі ваші завдання (крім системних Microsoft) вивантажуються в образ. При першому запуску відсутні створюються заново, з прив'язкою користувачів за іменем. Результат записується в ProgramData\\ProfileKeeper\\tasks_restore.log.");
        A("cap.log.tasks.run", "Сохраняю задачи планировщика...", "Saving the scheduled tasks...", "Зберігаю завдання планувальника...");
        A("cap.log.tasks", "Задач планировщика сохранено: {0}", "Scheduled tasks saved: {0}", "Завдань планувальника збережено: {0}");
        A("cap.chk.tasks", "Задач планировщика (кроме Microsoft): {0}", "Scheduled tasks (not Microsoft): {0}", "Завдань планувальника (крім Microsoft): {0}");
        A("cap.chk.tasks.pw", "Задач с сохранённым паролем: {0}. Sysprep сотрёт пароль, задачи нужно будет пересоздать или ввести пароль заново.", "Tasks with a stored password: {0}. Sysprep erases the password; re-create the tasks or enter the password again.", "Завдань зі збереженим паролем: {0}. Sysprep видалить пароль, завдання треба буде створити заново або ввести пароль знову.");
        A("win.btn", "Параметры Windows...", "Windows settings...", "Параметри Windows...");
        A("win.title", "Параметры Windows", "Windows settings", "Параметри Windows");
        A("win.org", "Организация:", "Organization:", "Організація:");
        A("win.owner", "Владелец:", "Owner:", "Власник:");
        A("win.tz", "Часовой пояс:", "Time zone:", "Часовий пояс:");
        A("win.input", "Раскладки:", "Keyboards:", "Розкладки:");
        A("win.ui", "Язык интерфейса:", "UI language:", "Мова інтерфейсу:");
        A("win.sysloc", "Язык системы:", "System locale:", "Мова системи:");
        A("win.userloc", "Региональные:", "User locale:", "Регіональні:");
        A("win.dns", "DNS-сервер:", "DNS server:", "DNS-сервер:");
        A("win.adapter", "Сетевой адаптер:", "Network adapter:", "Мережевий адаптер:");
        A("win.res", "Разрешение:", "Resolution:", "Роздільність:");
        A("win.admin.enable", "Включить встроенного Администратора", "Enable the built-in Administrator", "Увімкнути вбудованого Адміністратора");
        A("win.admin.name", "Имя админа:", "Admin name:", "Ім'я адміна:");
        A("win.admin.pw", "Пароль админа:", "Admin password:", "Пароль адміна:");
        A("win.user.name", "Пользователь:", "User:", "Користувач:");
        A("win.user.desc", "Описание:", "Description:", "Опис:");
        A("win.user.pw", "Пароль:", "Password:", "Пароль:");
        A("win.autologon", "Автовход (число входов):", "Auto logon (number of logons):", "Автовхід (кількість входів):");
        A("win.logons", "раз", "times", "разів");
        A("win.rdp", "Порт RDP (0 = выкл):", "RDP port (0 = off):", "Порт RDP (0 = вимк):");
        A("win.kms", "KMS-сервер:", "KMS server:", "KMS-сервер:");
        A("win.gvlk", "Ключ GVLK:", "GVLK key:", "Ключ GVLK:");
        A("win.icmp", "Разрешить ping (ICMPv4)", "Allow ping (ICMPv4)", "Дозволити ping (ICMPv4)");
        A("win.ipv6", "Отключить IPv6 и Teredo", "Disable IPv6 and Teredo", "Вимкнути IPv6 і Teredo");
        A("win.telemetry", "Отключить телеметрию", "Disable telemetry", "Вимкнути телеметрію");
        A("win.hibernate", "Отключить гибернацию", "Disable hibernation", "Вимкнути гібернацію");
        A("win.icons", "Значки рабочего стола (Этот компьютер и др.)", "Desktop icons (This PC etc.)", "Значки робочого столу (Цей комп'ютер тощо)");
        A("win.save", "Сохранить...", "Save...", "Зберегти...");
        A("win.load", "Загрузить...", "Load...", "Завантажити...");
        A("win.filter", "Профиль настроек", "Settings profile", "Профіль налаштувань");
        A("win.pw.later", "задаётся при установке", "set during installation", "задається при встановленні");
        A("win.note.install", "Пароли не сохраняются в профиле и не остаются на диске: файлы ответов удаляются при первом входе, а пароли в памяти программы стираются после установки.", "Passwords are not saved in a profile and do not stay on disk: the answer files are deleted at the first logon, and the passwords are wiped from the program memory after the installation.", "Паролі не зберігаються в профілі й не залишаються на диску: файли відповідей видаляються при першому вході, а паролі в пам'яті програми стираються після встановлення.");
        A("win.note.capture", "В образ попадают только настройки без паролей. Учётные записи и пароли задаются при установке образа.", "Only settings without passwords go into the image. Accounts and passwords are set when the image is installed.", "В образ потрапляють лише налаштування без паролів. Облікові записи та паролі задаються при встановленні образу.");
        A("win.err.tz", "Укажите часовой пояс, например FLE Standard Time.", "Enter a time zone, for example FLE Standard Time.", "Вкажіть часовий пояс, наприклад FLE Standard Time.");
        A("win.err.dns", "DNS-сервер должен быть адресом IPv4 (несколько через запятую).", "The DNS server must be an IPv4 address (several separated by commas).", "DNS-сервер має бути адресою IPv4 (кілька через кому).");
        A("win.err.port", "Порт RDP: число от 0 до 65535.", "RDP port: a number from 0 to 65535.", "Порт RDP: число від 0 до 65535.");
        A("win.err.kms", "KMS-сервер: имя или адрес, при желании с портом (host:1688).", "KMS server: a name or address, optionally with a port (host:1688).", "KMS-сервер: ім'я або адреса, за бажанням з портом (host:1688).");
        A("win.err.gvlk", "Ключ GVLK: пять групп по пять символов (XXXXX-XXXXX-XXXXX-XXXXX-XXXXX).", "GVLK key: five groups of five characters (XXXXX-XXXXX-XXXXX-XXXXX-XXXXX).", "Ключ GVLK: п'ять груп по п'ять символів (XXXXX-XXXXX-XXXXX-XXXXX-XXXXX).");
        A("win.err.logon", "Число автовходов: от 1 до 999.", "Number of auto logons: 1 to 999.", "Кількість автовходів: від 1 до 999.");
        A("win.err.res", "Разрешение: оба числа или оба 0.", "Resolution: both numbers or both 0.", "Роздільність: обидва числа або обидва 0.");
        A("win.err.num", "В числовом поле допустимы только цифры.", "Numeric fields accept digits only.", "У числовому полі допустимі лише цифри.");
        A("win.err.user", "Имя пользователя пустое или содержит недопустимые символы.", "The user name contains characters that are not allowed.", "Ім'я користувача містить недопустимі символи.");
        A("win.err.admin", "Имя встроенного администратора пустое или содержит недопустимые символы.", "The built-in administrator name is empty or has characters that are not allowed.", "Ім'я вбудованого адміністратора порожнє або містить недопустимі символи.");
        A("win.err.adminpw", "Для автовхода укажите пароль администратора.", "Enter the administrator password for auto logon.", "Для автовходу вкажіть пароль адміністратора.");
        A("win.err.userpw", "Укажите пароль пользователя.", "Enter the user password.", "Вкажіть пароль користувача.");
        A("win.log.head", "Параметры Windows", "Windows settings", "Параметри Windows");
        A("win.log.xml", "  Файл ответов записан в Windows\\Panther\\Unattend.", "  Answer file written to Windows\\Panther\\Unattend.", "  Файл відповідей записано в Windows\\Panther\\Unattend.");
        A("win.log.pretail", "  Скрипт pretail.cmd записан в System32.", "  pretail.cmd written to System32.", "  Скрипт pretail.cmd записано в System32.");
        A("win.log.cap", "Файл ответов без паролей создан, pretail.cmd скопирован в System32.", "Answer file without passwords created, pretail.cmd copied to System32.", "Файл відповідей без паролів створено, pretail.cmd скопійовано в System32.");
        A("inst.win.use", "Применить параметры Windows (файл ответов и pretail.cmd)", "Apply Windows settings (answer file and pretail.cmd)", "Застосувати параметри Windows (файл відповідей і pretail.cmd)");
        A("inst.confirm.win", "Параметры Windows: будут применены", "Windows settings: will be applied", "Параметри Windows: буде застосовано");
        A("cap.win.use", "Подготовить образ под параметры Windows", "Prepare the image for the Windows settings", "Підготувати образ під параметри Windows");
        A("cap.win.tip", "Создаёт файл ответов без паролей и кладёт pretail.cmd в образ. Учётные записи и пароли задаются при установке.", "Creates an answer file without passwords and puts pretail.cmd into the image. Accounts and passwords are set during installation.", "Створює файл відповідей без паролів і кладе pretail.cmd в образ. Облікові записи та паролі задаються при встановленні.");
        A("cap.clean.use", "Очистка перед Sysprep (DISM, временные файлы, журналы)", "Cleanup before Sysprep (DISM, temp files, logs)", "Очищення перед Sysprep (DISM, тимчасові файли, журнали)");
        A("cap.clean.tip", "Очищает хранилище компонентов, временные файлы, журналы событий и старые журналы Panther.", "Cleans the component store, temp files, event logs and old Panther logs.", "Очищає сховище компонентів, тимчасові файли, журнали подій і старі журнали Panther.");
        A("cap.clean.run", "Очистка перед Sysprep...", "Cleaning up before Sysprep...", "Очищення перед Sysprep...");
        A("cap.clean.dism", "Очистка хранилища компонентов (DISM)...", "Cleaning the component store (DISM)...", "Очищення сховища компонентів (DISM)...");
        A("cap.clean.temp", "Очистка временных файлов...", "Cleaning temp files...", "Очищення тимчасових файлів...");
        A("cap.clean.events", "Очистка журналов событий...", "Clearing event logs...", "Очищення журналів подій...");
        A("cap.clean.panther", "Удаление старых журналов Panther...", "Removing old Panther logs...", "Видалення старих журналів Panther...");
    }
}
