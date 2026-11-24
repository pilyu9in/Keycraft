using System.Globalization;

namespace Keycraft;

internal sealed record Language(string Code, string Name, int Index)
{
    internal static readonly Language[] All =
    [
        new("ru", "Русский", 0), new("en", "English", 1), new("de", "Deutsch", 2),
        new("fr", "Français", 3), new("es", "Español", 4), new("ja", "日本語", 5),
        new("zh-Hans", "简体中文", 6), new("zh-Hant", "繁體中文", 7)
    ];
    private static string SettingsPath => Path.Combine(Path.GetDirectoryName(Profile.PathToProfile)!, "language.txt");
    internal static Language Load()
    {
        try { if (File.Exists(SettingsPath)) return Find(File.ReadAllText(SettingsPath).Trim()); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return Find(CultureInfo.CurrentUICulture.Name);
    }

    internal static Language Find(string code)
    {
        var parts = code.Split('-');
        if (parts[0].Equals("zh", StringComparison.OrdinalIgnoreCase))
        {
            var scriptOrRegion = parts.Length > 1 ? parts[1].ToUpperInvariant() : "";
            return All[scriptOrRegion is "HANT" or "TW" or "HK" or "MO" or "CHT" ? 7 : 6];
        }
        return All.FirstOrDefault(item => item.Code.Equals(parts[0], StringComparison.OrdinalIgnoreCase)) ?? All[1];
    }
    internal void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, Code);
    }
    internal string Text(string key) => Copy[key][Index];
    internal string Format(string key, params object[] values) => string.Format(Text(key), values);

    internal string Source(string id, string fallback) => id switch
    {
        "HID-000C-019E" => Text("Lock"),
        "HID-00FF-0003" => "Fn / Globe",
        "HID-000C-0030" or "HID-0001-0081" => Text("Power"),
        "HID-000C-00B8" => Text("Eject"),
        "HID-000C-0040" => Text("MenuKey"),
        "SC-E0-1C" => Text("NumpadEnter"),
        "HID-000C-006F" => Text("BrightnessUp"),
        "HID-000C-0070" => Text("BrightnessDown"),
        "HID-000C-00B5" => Text("NextTrack"),
        "HID-000C-00B6" => Text("PreviousTrack"),
        "HID-000C-00CD" or "SC-E0-22" => Text("Play"),
        "HID-000C-00E2" or "SC-E0-20" => Text("Mute"),
        "HID-000C-00E9" or "SC-E0-30" => Text("VolumeUp"),
        "HID-000C-00EA" or "SC-E0-2E" => Text("VolumeDown"),
        "HID-000C-0221" => Text("Search"),
        "HID-0001-0082" => Text("Sleep"),
        "SC-E0-53" => Key(0x2e), "SC-0E" => Key(8), "SC-39" => Key(0x20),
        "SC-2A" => Key(0xa0), "SC-36" => Key(0xa1), "SC-1D" => Key(0xa2), "SC-E0-1D" => Key(0xa3),
        "SC-38" => Key(0xa4), "SC-E0-38" => Key(0xa5), "SC-E0-5B" => Key(0x5b), "SC-E0-5C" => Key(0x5c),
        _ when id.StartsWith("HID-", StringComparison.Ordinal) => Format("Special", id[4..].Replace('-', ':')),
        _ => fallback
    };

    internal string Chord(int[] keys) => keys.Length == 0 ? Text("Disable") : string.Join(" + ", keys.Select(Key));

    internal string Trigger(string id, string fallback, int[] modifierGroups) =>
        string.Join(" + ", modifierGroups.Select(key => key == 0x5b ? "Win" : Key(key)).Append(Source(id, fallback)));

    internal string Action(Rule rule) => rule.Launch is { } launch
        ? Format(launch.Kind == LaunchKind.Application ? "AppAction" : "CommandAction", launch.DisplayName)
        : Chord(rule.Target);

    private string Key(int key) => key switch
    {
        0x2e => Text("DeleteKey"), 8 => Text("Backspace"), 0x20 => Text("Space"), 0xd => "Enter", KeyNames.NumpadEnter => Text("NumpadEnter"),
        0x10 => "Shift", 0x11 => Index == 2 ? "Strg" : "Ctrl", 0x12 => "Alt",
        0xa0 => Format("Left", "Shift"), 0xa1 => Format("Right", "Shift"),
        0xa2 => Format("Left", Index == 2 ? "Strg" : "Ctrl"), 0xa3 => Format("Right", Index == 2 ? "Strg" : "Ctrl"),
        0xa4 => Format("Left", "Alt"), 0xa5 => Format("Right", "Alt"),
        0x5b => Format("Left", "Win"), 0x5c => Format("Right", "Win"),
        0xb3 => Text("Play"), 0xad => Text("Mute"), 0xae => Text("VolumeDown"), 0xaf => Text("VolumeUp"),
        0x21 => Text("PageUp"), 0x22 => Text("PageDown"), 0x24 => Text("Home"), 0x23 => Text("End"),
        0x25 => "←", 0x26 => "↑", 0x27 => "→", 0x28 => "↓", 0x2d => Text("InsertKey"),
        _ => KeyNames.Name(key)
    };

    private static readonly Dictionary<string, string[]> Copy = new()
    {
        ["About"] = ["О программе", "About", "Über Keycraft", "À propos", "Acerca de", "このアプリについて", "关于", "關於"],
        ["AboutTitle"] = ["О Keycraft", "About Keycraft", "Über Keycraft", "À propos de Keycraft", "Acerca de Keycraft", "Keycraft について", "关于 Keycraft", "關於 Keycraft"],
        ["RightsReserved"] = ["Все права защищены.", "All rights reserved.", "Alle Rechte vorbehalten.", "Tous droits réservés.", "Todos los derechos reservados.", "すべての権利を留保します。", "保留所有权利。", "保留所有權利。"],
        ["Close"] = ["Закрыть", "Close", "Schließen", "Fermer", "Cerrar", "閉じる", "关闭", "關閉"],
        ["Language"] = ["Язык интерфейса", "Interface language", "Sprache der Oberfläche", "Langue de l’interface", "Idioma de la interfaz", "表示言語", "界面语言", "介面語言"],
        ["LaunchApp"] = ["Открыть приложение…", "Open an app…", "App öffnen…", "Ouvrir une application…", "Abrir una aplicación…", "アプリを開く…", "打开应用程序…", "開啟應用程式…"],
        ["RunCommand"] = ["Выполнить команду…", "Run a command…", "Befehl ausführen…", "Exécuter une commande…", "Ejecutar un comando…", "コマンドを実行…", "运行命令…", "執行命令…"],
        ["AppPath"] = ["Путь к приложению или ярлыку", "Path to an app or shortcut", "Pfad zur App oder Verknüpfung", "Chemin de l’application ou du raccourci", "Ruta de la aplicación o del acceso directo", "アプリまたはショートカットのパス", "应用程序或快捷方式的路径", "應用程式或捷徑的路徑"],
        ["CommandText"] = ["Команда Windows (cmd)", "Windows command (cmd)", "Windows-Befehl (cmd)", "Commande Windows (cmd)", "Comando de Windows (cmd)", "Windows コマンド (cmd)", "Windows 命令 (cmd)", "Windows 命令 (cmd)"],
        ["AppFiles"] = ["Приложения и ярлыки", "Apps and shortcuts", "Apps und Verknüpfungen", "Applications et raccourcis", "Aplicaciones y accesos directos", "アプリとショートカット", "应用程序和快捷方式", "應用程式與捷徑"],
        ["Browse"] = ["Обзор…", "Browse…", "Durchsuchen…", "Parcourir…", "Examinar…", "参照…", "浏览…", "瀏覽…"],
        ["ApplyAction"] = ["Применить", "Apply", "Übernehmen", "Appliquer", "Aplicar", "適用", "应用", "套用"],
        ["AppAction"] = ["Открыть {0}", "Open {0}", "{0} öffnen", "Ouvrir {0}", "Abrir {0}", "{0} を開く", "打开 {0}", "開啟 {0}"],
        ["CommandAction"] = ["Команда · {0}", "Command · {0}", "Befehl · {0}", "Commande · {0}", "Comando · {0}", "コマンド · {0}", "命令 · {0}", "命令 · {0}"],
        ["LaunchError"] = ["Не удалось запустить действие", "Could not launch the action", "Aktion konnte nicht gestartet werden", "Impossible de lancer l’action", "No se pudo iniciar la acción", "操作を開始できませんでした", "无法启动操作", "無法啟動操作"],
        ["NumpadEnter"] = ["Enter (цифровой блок)", "Numpad Enter", "Enter (Nummernblock)", "Entrée (pavé numérique)", "Intro (teclado numérico)", "テンキーの Enter", "数字键盘 Enter", "數字鍵盤 Enter"],
        ["MenuKey"] = ["Меню", "Menu", "Menü", "Menu", "Menú", "メニュー", "菜单", "選單"],
        ["KeyboardModel"] = ["Модель клавиатуры", "Keyboard model", "Tastaturmodell", "Modèle de clavier", "Modelo de teclado", "キーボードモデル", "键盘型号", "鍵盤型號"],
        ["AutoModel"] = ["Автовыбор модели", "Automatic model", "Automatisches Modell", "Modèle auto", "Modelo automático", "モデル自動選択", "自动选择型号", "自動選擇型號"],
        ["AutoShort"] = ["Авто", "Auto", "Auto", "Auto", "Auto", "自動", "自动", "自動"],
        ["CompactLock"] = ["Компактная с замком", "Compact with Lock", "Kompakt mit Sperre", "Compact avec verrou", "Compacto con bloqueo", "コンパクト（ロック）", "紧凑型（锁定键）", "精巧型（鎖定鍵）"],
        ["FullLock"] = ["С цифровым блоком", "With numeric keypad", "Mit Nummernblock", "Avec pavé numérique", "Con teclado numérico", "テンキー付き", "带数字键盘", "含數字鍵盤"],
        ["CompactTouchId"] = ["Компактная Touch ID", "Compact Touch ID", "Kompakt mit Touch ID", "Compact Touch ID", "Compacto con Touch ID", "コンパクト（Touch ID）", "紧凑型（Touch ID）", "精巧型（Touch ID）"],
        ["FullTouchId"] = ["Полная Touch ID", "Full-size Touch ID", "Groß mit Touch ID", "Complet Touch ID", "Completo con Touch ID", "フルサイズ（Touch ID）", "全尺寸（Touch ID）", "全尺寸（Touch ID）"],
        ["CompactEject"] = ["Компактная с ⏏", "Compact with ⏏", "Kompakt mit ⏏", "Compact avec ⏏", "Compacto con ⏏", "コンパクト（⏏）", "紧凑型（⏏）", "精巧型（⏏）"],
        ["FullEject"] = ["Полная с ⏏", "Full-size with ⏏", "Groß mit ⏏", "Complet avec ⏏", "Completo con ⏏", "フルサイズ（⏏）", "全尺寸（⏏）", "全尺寸（⏏）"],
        ["TouchIdHint"] = ["Распознавание отпечатка недоступно в Windows. Нажатие можно записать, если Windows получает событие.", "Windows has no fingerprint input here. You can capture a press if Windows receives an event.", "Windows erhält keinen Fingerabdruck. Ein Tastendruck lässt sich erfassen, wenn Windows ihn empfängt.", "La lecture d’empreintes est indisponible dans Windows. La pression sur la touche peut être détectée si Windows reçoit un événement.", "La lectura de huellas no está disponible en Windows. Se puede capturar la pulsación si Windows recibe el evento.", "Windows では指紋認証を利用できません。Windows がキーイベントを受け取る場合は、押下を記録できます。", "Windows 无法读取指纹。如果 Windows 收到按键事件，仍可记录按键。", "Windows 無法讀取指紋。如果 Windows 收到按鍵事件，仍可記錄該次按鍵。"],
        ["KeyboardTitle"] = ["Клавиатура", "Keyboard", "Tastatur", "Clavier", "Teclado", "キーボード", "键盘", "鍵盤"],
        ["KeyboardLayout"] = ["Форма клавиатуры", "Keyboard shape", "Tastaturform", "Disposition physique", "Distribución física", "キーボード配列", "键盘布局", "鍵盤配置"],
        ["KeyboardHint"] = ["Нажми клавишу на схеме, чтобы настроить её. Точкой отмечены назначения.", "Click a key to edit it. Dots mark mappings.", "Klicke auf eine Taste zum Bearbeiten. Punkte markieren Belegungen.", "Cliquez sur une touche pour la configurer. Un point indique une affectation.", "Haz clic en una tecla para configurarla. Los puntos indican asignaciones.", "キーをクリックして設定します。割り当て済みのキーには点が付きます。", "点击按键进行设置。圆点表示已有映射。", "點選按鍵即可設定。圓點表示已有對應設定。"],
        ["PickKey"] = ["Нажать клавишу", "Press a key", "Taste drücken", "Presser une touche", "Pulsar una tecla", "キーを押す", "按下按键", "按下按鍵"],
        ["DetailsShort"] = ["Устройства", "Devices", "Geräte", "Appareils", "Dispositivos", "デバイス", "设备", "裝置"],
        ["FnHint"] = ["Fn по USB добавляет действие. Встроенный слой Fn продолжает работать.", "Fn over USB adds an action. The built-in Fn layer still works.", "Fn fügt per USB eine Aktion hinzu. Die interne Fn-Ebene bleibt aktiv.", "Via USB, Fn peut déclencher une action. Les fonctions Fn intégrées restent actives.", "Fn por USB añade una acción. Las funciones integradas de Fn siguen activas.", "USB 接続時、Fn に操作を追加できます。内蔵の Fn 機能は引き続き使えます。", "通过 USB 可为 Fn 添加操作。内置 Fn 功能仍然有效。", "透過 USB 可為 Fn 新增動作。內建 Fn 功能仍然有效。"],
        ["Rules"] = ["Назначения", "Mappings", "Belegungen", "Affectations", "Asignaciones", "キー割り当て", "按键映射", "按鍵對應"],
        ["New"] = ["Новое назначение", "New mapping", "Neue Belegung", "Nouvelle affectation", "Nueva asignación", "新しい割り当て", "新建映射", "新增對應"],
        ["Add"] = ["+  Добавить", "+  Add", "+  Hinzufügen", "+  Ajouter", "+  Añadir", "+  追加", "+  添加", "+  新增"],
        ["Edit"] = ["Изменить", "Edit", "Bearbeiten", "Modifier", "Editar", "編集", "编辑", "編輯"],
        ["EditTitle"] = ["Изменить назначение", "Edit mapping", "Belegung bearbeiten", "Modifier l’affectation", "Editar asignación", "割り当てを編集", "编辑映射", "編輯對應"],
        ["Remove"] = ["Удалить", "Remove", "Entfernen", "Supprimer", "Eliminar", "削除", "删除", "刪除"],
        ["Enabled"] = ["Включено", "Active", "Aktiv", "Actif", "Activo", "有効", "已启用", "已啟用"],
        ["Paused"] = ["На паузе", "Paused", "Pausiert", "En pause", "En pausa", "一時停止中", "已暂停", "已暫停"],
        ["Source"] = ["Исходная клавиша", "Source key", "Ausgangstaste", "Touche source", "Tecla de origen", "元のキー", "源按键", "來源按鍵"],
        ["Action"] = ["Действие", "Action", "Aktion", "Action", "Acción", "操作", "操作", "動作"],
        ["Capture"] = ["Поймать", "Capture", "Erfassen", "Détecter", "Capturar", "検出", "捕获", "偵測"],
        ["Record"] = ["Записать", "Record", "Aufzeichnen", "Enregistrer", "Grabar", "記録", "录制", "錄製"],
        ["Cancel"] = ["Отмена", "Cancel", "Abbrechen", "Annuler", "Cancelar", "キャンセル", "取消", "取消"],
        ["Save"] = ["Сохранить", "Save", "Speichern", "Enregistrer", "Guardar", "保存", "保存", "儲存"],
        ["NoSource"] = ["Клавиша не выбрана", "No key selected", "Keine Taste gewählt", "Aucune touche sélectionnée", "No hay ninguna tecla seleccionada", "キーが選択されていません", "未选择按键", "尚未選取按鍵"],
        ["ChooseAction"] = ["Выбери действие", "Choose an action", "Aktion wählen", "Choisir une action", "Elegir una acción", "操作を選択", "选择操作", "選擇動作"],
        ["Custom"] = ["Своё сочетание…", "Custom shortcut…", "Eigene Tastenkombination…", "Raccourci personnalisé…", "Atajo personalizado…", "カスタムショートカット…", "自定义快捷键…", "自訂快速鍵…"],
        ["Empty"] = ["Назначений пока нет. Добавь первое.", "No mappings yet. Add your first one.", "Noch keine Belegungen. Füge eine hinzu.", "Aucune affectation. Ajoutez-en une pour commencer.", "Aún no hay asignaciones. Añade la primera.", "割り当てがありません。最初の割り当てを追加してください。", "还没有映射。请添加第一条。", "尚無按鍵對應。請新增第一項設定。"],
        ["Test"] = ["Проверка", "Test input", "Eingabetest", "Zone de test", "Prueba", "入力テスト", "输入测试", "輸入測試"],
        ["TestHint"] = ["Набери текст и проверь клавишу", "Type here and try a mapped key", "Hier tippen und eine Belegung testen", "Saisissez du texte et testez une touche", "Escribe aquí para probar una tecla asignada", "ここに入力して割り当てたキーを試す", "在此输入文字并测试映射按键", "在此輸入文字並測試已設定的按鍵"],
        ["Connected"] = ["{0} · {1}", "{0} · {1}", "{0} · {1}", "{0} · {1}", "{0} · {1}", "{0} · {1}", "{0} · {1}", "{0} · {1}"],
        ["NoDevice"] = ["Клавиатура Apple не найдена", "No Apple keyboard detected", "Keine Apple-Tastatur erkannt", "Aucun clavier Apple détecté", "No se detectó ningún teclado Apple", "Apple キーボードが見つかりません", "未检测到 Apple 键盘", "未偵測到 Apple 鍵盤"],
        ["SpecialReady"] = ["Спецклавиши доступны по USB", "Special keys ready over USB", "Sondertasten per USB bereit", "Touches spéciales disponibles par USB", "Teclas especiales disponibles por USB", "特殊キーを USB で使用できます", "特殊按键可通过 USB 使用", "可透過 USB 使用特殊按鍵"],
        ["LockReady"] = ["Замок доступен по USB", "Lock key ready over USB", "Sperrtaste per USB bereit", "Touche de verrouillage disponible par USB", "Tecla de bloqueo disponible por USB", "ロックキーを USB で使用できます", "锁定键可通过 USB 使用", "可透過 USB 使用鎖定鍵"],
        ["UsbWaiting"] = ["Доступность спецклавиш зависит от модели и подключения", "Special key availability depends on the model and connection", "Sondertasten hängen vom Modell und der Verbindung ab", "La disponibilité des touches spéciales dépend du modèle et de la connexion", "Las teclas especiales dependen del modelo y de la conexión", "特殊キーの対応はモデルと接続方法によって異なります", "特殊按键是否可用取决于型号和连接方式", "特殊按鍵是否可用取決於型號與連線方式"],
        ["UsbError"] = ["USBPcap: {0}", "USBPcap: {0}", "USBPcap: {0}", "USBPcap : {0}", "USBPcap: {0}", "USBPcap: {0}", "USBPcap：{0}", "USBPcap：{0}"],
        ["CaptureSource"] = ["Нажми исходную клавишу или сочетание. Ожидание 12 секунд.", "Press a source key or shortcut. Waiting for 12 seconds.", "Drücke eine Ausgangstaste oder Kombination. Wartezeit: 12 Sekunden.", "Appuyez sur une touche source ou un raccourci dans les 12 secondes.", "Pulsa una tecla de origen o un atajo en los próximos 12 segundos.", "12 秒以内に元のキーまたはショートカットを押してください。", "请在 12 秒内按下源按键或快捷键。", "請在 12 秒內按下來源按鍵或快速鍵。"],
        ["CaptureTarget"] = ["Нажми клавишу или сочетание. Ожидание 12 секунд.", "Press a key or shortcut. Waiting for 12 seconds.", "Drücke eine Taste oder Kombination. Wartezeit: 12 Sekunden.", "Appuyez sur une touche ou un raccourci dans les 12 secondes.", "Pulsa una tecla o un atajo en los próximos 12 segundos.", "12 秒以内にキーまたはショートカットを押してください。", "请在 12 秒内按下按键或快捷键。", "請在 12 秒內按下按鍵或快速鍵。"],
        ["Timeout"] = ["Время ожидания истекло", "Capture timed out", "Zeit für die Eingabe abgelaufen", "Délai de saisie dépassé", "Se agotó el tiempo de captura", "入力待ち時間が終了しました", "等待按键超时", "等待按鍵逾時"],
        ["CaptureError"] = ["Не удалось подключить перехват клавиш", "Could not start keyboard capture", "Tastenerfassung konnte nicht gestartet werden", "Impossible de détecter les touches", "No se pudo iniciar la captura de teclas", "キー入力の検出を開始できませんでした", "无法开始捕获按键", "無法開始偵測按鍵"],
        ["LoadError"] = ["Не удалось открыть профиль", "Could not open the profile", "Profil konnte nicht geöffnet werden", "Impossible d’ouvrir le profil", "No se pudo abrir el perfil", "プロファイルを開けませんでした", "无法打开配置文件", "無法開啟設定檔"],
        ["SaveError"] = ["Не удалось сохранить изменения", "Could not save changes", "Änderungen konnten nicht gespeichert werden", "Impossible d’enregistrer les modifications", "No se pudieron guardar los cambios", "変更を保存できませんでした", "无法保存更改", "無法儲存變更"],
        ["Saved"] = ["Назначение сохранено", "Mapping saved", "Belegung gespeichert", "Affectation enregistrée", "Asignación guardada", "割り当てを保存しました", "映射已保存", "已儲存對應設定"],
        ["Removed"] = ["Назначение удалено", "Mapping removed", "Belegung entfernt", "Affectation supprimée", "Asignación eliminada", "割り当てを削除しました", "映射已删除", "已刪除對應設定"],
        ["HidDisable"] = ["Для этой клавиши можно добавить действие Windows", "You can assign an additional action to this key", "Dieser Taste kann eine zusätzliche Aktion zugewiesen werden", "Vous pouvez attribuer une action supplémentaire à cette touche", "Puedes asignar una acción adicional a esta tecla", "このキーには操作を追加できます", "可以为此按键添加操作", "可為此按鍵新增動作"],
        ["Open"] = ["Открыть Keycraft", "Open Keycraft", "Keycraft öffnen", "Ouvrir Keycraft", "Abrir Keycraft", "Keycraft を開く", "打开 Keycraft", "開啟 Keycraft"],
        ["Toggle"] = ["Пауза / возобновить", "Pause / resume", "Pausieren / fortsetzen", "Mettre en pause / reprendre", "Pausar / reanudar", "一時停止 / 再開", "暂停 / 恢复", "暫停 / 繼續"],
        ["Exit"] = ["Выход", "Quit", "Beenden", "Quitter", "Salir", "終了", "退出", "結束"],
        ["Details"] = ["Диагностика", "Diagnostics", "Diagnose", "Diagnostic", "Diagnóstico", "診断", "诊断", "診斷"],
        ["AlreadyOpen"] = ["Keycraft уже запущен. Открой его из области уведомлений Windows.", "Keycraft is already running. Open it from the Windows notification area.", "Keycraft läuft bereits. Öffne es im Windows-Infobereich.", "Keycraft est déjà en cours d’exécution. Ouvrez-le depuis la zone de notification de Windows.", "Keycraft ya está en ejecución. Ábrelo desde el área de notificación de Windows.", "Keycraft はすでに起動しています。Windows の通知領域から開いてください。", "Keycraft 已在运行。请从 Windows 通知区域打开。", "Keycraft 已在執行中。請從 Windows 通知區域開啟。"],
        ["Lock"] = ["Замок Apple", "Apple lock key", "Apple-Sperrtaste", "Verrouillage Apple", "Bloqueo de Apple", "Apple のロックキー", "Apple 锁定键", "Apple 鎖定鍵"],
        ["Power"] = ["Питание", "Power", "Ein/Aus", "Alimentation", "Encendido", "電源", "电源", "電源"],
        ["Eject"] = ["Извлечь", "Eject", "Auswerfen", "Éjecter", "Expulsar", "取り出し", "推出", "退出媒體"],
        ["BrightnessUp"] = ["Яркость +", "Brightness +", "Helligkeit +", "Luminosité +", "Brillo +", "明るさ +", "亮度 +", "亮度 +"],
        ["BrightnessDown"] = ["Яркость −", "Brightness −", "Helligkeit −", "Luminosité −", "Brillo −", "明るさ −", "亮度 −", "亮度 −"],
        ["NextTrack"] = ["Следующий трек", "Next track", "Nächster Titel", "Piste suivante", "Pista siguiente", "次の曲", "下一曲", "下一首"],
        ["PreviousTrack"] = ["Предыдущий трек", "Previous track", "Vorheriger Titel", "Piste précédente", "Pista anterior", "前の曲", "上一曲", "上一首"],
        ["Play"] = ["Пуск / пауза", "Play / pause", "Wiedergabe / Pause", "Lecture / pause", "Reproducir / pausar", "再生 / 一時停止", "播放 / 暂停", "播放 / 暫停"],
        ["Mute"] = ["Без звука", "Mute", "Stumm", "Couper le son", "Silenciar", "ミュート", "静音", "靜音"],
        ["VolumeUp"] = ["Громкость +", "Volume +", "Lautstärke +", "Volume +", "Volumen +", "音量 +", "音量 +", "音量 +"],
        ["VolumeDown"] = ["Громкость −", "Volume −", "Lautstärke −", "Volume −", "Volumen −", "音量 −", "音量 −", "音量 −"],
        ["Search"] = ["Поиск", "Search", "Suche", "Rechercher", "Buscar", "検索", "搜索", "搜尋"],
        ["Sleep"] = ["Спящий режим", "Sleep", "Ruhezustand", "Veille", "Suspender", "スリープ", "睡眠", "睡眠"],
        ["Special"] = ["Спецклавиша {0}", "Special key {0}", "Sondertaste {0}", "Touche spéciale {0}", "Tecla especial {0}", "特殊キー {0}", "特殊按键 {0}", "特殊按鍵 {0}"],
        ["Disable"] = ["Отключить клавишу", "Disable key", "Taste deaktivieren", "Désactiver la touche", "Desactivar tecla", "キーを無効にする", "禁用按键", "停用按鍵"],
        ["Left"] = ["Левый {0}", "Left {0}", "{0} links", "{0} gauche", "{0} izquierdo", "左 {0}", "左 {0}", "左 {0}"],
        ["Right"] = ["Правый {0}", "Right {0}", "{0} rechts", "{0} droit", "{0} derecho", "右 {0}", "右 {0}", "右 {0}"],
        ["DeleteKey"] = ["Delete", "Delete", "Entf", "Suppr", "Supr", "Delete", "Delete", "Delete"],
        ["Backspace"] = ["Backspace", "Backspace", "Rücktaste", "Retour arrière", "Retroceso", "Backspace", "退格键", "退格鍵"],
        ["Space"] = ["Пробел", "Space", "Leertaste", "Espace", "Espacio", "スペース", "空格键", "空白鍵"],
        ["PageUp"] = ["Page Up", "Page Up", "Bild auf", "Page préc.", "Re Pág", "Page Up", "向上翻页", "向上翻頁"],
        ["PageDown"] = ["Page Down", "Page Down", "Bild ab", "Page suiv.", "Av Pág", "Page Down", "向下翻页", "向下翻頁"],
        ["Home"] = ["Home", "Home", "Pos1", "Début", "Inicio", "Home", "Home", "Home"],
        ["End"] = ["End", "End", "Ende", "Fin", "Fin", "End", "End", "End"],
        ["InsertKey"] = ["Insert", "Insert", "Einfg", "Inser", "Insert", "Insert", "Insert", "Insert"]
    };
}
