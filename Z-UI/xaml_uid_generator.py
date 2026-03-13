"""
xaml_uid_generator.py
─────────────────────
Обходит все .xaml файлы в папке проекта:
  1. Находит контролы с текстом (TextBlock, Button, ComboBoxItem, ToggleSwitch, …)
  2. Проверяет наличие x:Uid — если есть, пропускает
  3. Генерирует x:Uid из x:Name или из текста контрола
  4. Вставляет x:Uid в .xaml (форматирование сохраняется)
  5. Добавляет строки в Resources.resw для каждой указанной локали

Запуск (минимальный):
    python xaml_uid_generator.py --root F:\\Dev\\ZUI\\ZUI

Все параметры:
    --root          Корень проекта (.xaml ищутся рекурсивно)
    --resw          Пути к .resw через запятую
    --dry           Только вывод, без записи файлов
    --controls      Список контролов через запятую
    --translate     Локали для перевода, например: en-US,de-DE,fr-FR
    --source-lang   Исходный язык строк в .xaml (по умолчанию: ru)
    --retranslate   Переводить заново строки где target == source
    --engine        google | ollama
    --ollama-model  Модель Ollama (по умолчанию: qwen2.5:7b)
    --ollama-url    URL Ollama (по умолчанию: http://localhost:11434)
"""

import argparse
import json
import re
import sys
from pathlib import Path
import xml.etree.ElementTree as ET

# ─── Контролы и их текстовые атрибуты ────────────────────────────────────────

CONTROL_TEXT_ATTR: dict[str, list[str]] = {
    "TextBlock":          ["Text"],
    "Button":             ["Content"],
    "ComboBoxItem":       ["Content"],
    "ToggleSwitch":       ["Header"],
    "HyperlinkButton":    ["Content"],
    "RadioButton":        ["Content"],
    "CheckBox":           ["Content"],
    "AppBarButton":       ["Label"],
    "MenuFlyoutItem":     ["Text"],
    "NavigationViewItem": ["Content"],
    "TextBox":            ["PlaceholderText", "Header"],
    "PasswordBox":        ["PlaceholderText", "Header"],
}

DEFAULT_CONTROLS = list(CONTROL_TEXT_ATTR.keys())

X_NS   = "http://schemas.microsoft.com/winfx/2006/xaml"
X_UID  = f"{{{X_NS}}}Uid"
X_NAME = f"{{{X_NS}}}Name"

# ─── Коды языков ──────────────────────────────────────────────────────────────

LANG_DISPLAY: dict[str, str] = {
    "ru": "Russian",    "en": "English",    "de": "German",
    "fr": "French",     "es": "Spanish",    "zh": "Chinese",
    "ja": "Japanese",   "ko": "Korean",     "tr": "Turkish",
    "pt": "Portuguese", "it": "Italian",    "pl": "Polish",
    "uk": "Ukrainian",  "ar": "Arabic",     "nl": "Dutch",
    "sv": "Swedish",    "fi": "Finnish",    "cs": "Czech",
    "hu": "Hungarian",  "ro": "Romanian",
}

_LANG_TO_LOCALE: dict[str, str] = {
    "ru": "ru-RU", "en": "en-US", "de": "de-DE", "fr": "fr-FR",
    "es": "es-ES", "zh": "zh-CN", "ja": "ja-JP", "ko": "ko-KR",
    "tr": "tr-TR", "pt": "pt-BR", "it": "it-IT", "pl": "pl-PL",
    "uk": "uk-UA", "ar": "ar-SA", "nl": "nl-NL", "sv": "sv-SE",
    "fi": "fi-FI", "cs": "cs-CZ", "hu": "hu-HU", "ro": "ro-RO",
}

def lang_to_locale(lang_code: str) -> str:
    code = lang_code.lower()
    return _LANG_TO_LOCALE.get(code, f"{code}-{code.upper()}")

# ─── Транслитерация и генерация идентификатора ────────────────────────────────

_RU_MAP = {
    "а":"a","б":"b","в":"v","г":"g","д":"d","е":"e","ё":"yo","ж":"zh",
    "з":"z","и":"i","й":"y","к":"k","л":"l","м":"m","н":"n","о":"o",
    "п":"p","р":"r","с":"s","т":"t","у":"u","ф":"f","х":"kh","ц":"ts",
    "ч":"ch","ш":"sh","щ":"sch","ъ":"","ы":"y","ь":"","э":"e","ю":"yu","я":"ya",
}

def _translit(s: str) -> str:
    return "".join(_RU_MAP.get(c.lower(), c) for c in s)

def slugify(text: str, max_len: int = 50) -> str:
    words = re.findall(r"[A-Za-zА-Яа-яЁё0-9]+", text)
    if not words:
        return "Control"
    result = "".join(_translit(w).capitalize() for w in words)
    result = re.sub(r"[^A-Za-z0-9]", "", result)
    if result and result[0].isdigit():
        result = "Item" + result
    return result[:max_len] or "Control"

CTRL_SUFFIX = {
    "TextBlock":          "Title",
    "Button":             "Btn",
    "ComboBoxItem":       "Item",
    "ToggleSwitch":       "Toggle",
    "NavigationViewItem": "Nav",
    "AppBarButton":       "AppBtn",
    "CheckBox":           "Check",
    "RadioButton":        "Radio",
    "HyperlinkButton":    "Link",
    "TextBox":            "Input",
    "PasswordBox":        "Pwd",
}

def make_uid(elem: ET.Element, control: str, used: set, uid_ctrl: dict) -> str:
    base = elem.get(X_NAME) or elem.get("x:Name", "")
    if not base:
        for attr in CONTROL_TEXT_ATTR.get(control, []):
            val = elem.get(attr, "").strip()
            if val and not val.startswith("{"):
                base = slugify(val)
                break
    if not base:
        base = control

    if base in uid_ctrl and uid_ctrl[base] != control:
        base = f"{base}{CTRL_SUFFIX.get(control, control)}"

    uid = base
    n = 1
    while uid in used:
        uid = f"{base}{n}"
        n += 1
    used.add(uid)
    uid_ctrl[uid] = control
    return uid

# ─── Вставка x:Uid в сырой текст XAML ────────────────────────────────────────

def inject_xuid(xaml: str, elem: ET.Element, control: str, uid: str) -> str:
    """
    Корректно обрабатывает многострочные теги — отслеживает состояние кавычек
    при сканировании до конца открывающего тега.
    """
    xname = elem.get(X_NAME) or elem.get("x:Name", "")

    for m in re.finditer(r"<(?:\w+:)?" + re.escape(control) + r"(?=[\s/>])", xaml):
        i = m.end()
        in_quote: str | None = None
        tag_end: int | None = None

        while i < len(xaml):
            c = xaml[i]
            if in_quote:
                if c == in_quote:
                    in_quote = None
            elif c in ('"', "'"):
                in_quote = c
            elif c == ">":
                tag_end = i + 1
                break
            i += 1

        if tag_end is None:
            continue

        tag_slice = xaml[m.start():tag_end]
        if "x:Uid" in tag_slice:
            continue
        if xname and f'"{xname}"' not in tag_slice:
            continue

        return xaml[:m.end()] + f' x:Uid="{uid}"' + xaml[m.end():]

    return xaml

# ─── Обработка одного XAML файла ─────────────────────────────────────────────

def process_xaml(path: Path, controls: list, used_uids: set, uid_ctrl: dict, dry: bool) -> list:
    text = path.read_text(encoding="utf-8-sig")

    for prefix, uri in re.findall(r'xmlns(?::(\\w+))?="([^"]+)"', text):
        try:
            ET.register_namespace(prefix or "", uri)
        except ValueError:
            pass
    ET.register_namespace("x", X_NS)

    try:
        root = ET.fromstring(text)
    except ET.ParseError as e:
        print(f"  [!] ParseError в {path.name}: {e}", file=sys.stderr)
        return []

    entries = []
    modified = False

    for elem in root.iter():
        local = elem.tag.split("}")[-1] if "}" in elem.tag else elem.tag
        if local not in controls:
            continue

        existing_uid = elem.get(X_UID) or elem.get("x:Uid")
        if existing_uid:
            uid = existing_uid
        else:
            uid = make_uid(elem, local, used_uids, uid_ctrl)
            new_text = inject_xuid(text, elem, local, uid)
            if new_text != text:
                text = new_text
                modified = True
            else:
                print(f"    [warn] не удалось вставить x:Uid для {local} (uid={uid})")

        for attr in CONTROL_TEXT_ATTR.get(local, []):
            val = elem.get(attr, "").strip()
            if val and not val.startswith("{"):
                entries.append((uid, f"{uid}.{attr}", val))

    status = "✏  Изменён" if modified else "✓  Без изменений"
    if dry:
        status = "~  [dry] " + ("будет изменён" if modified else "без изменений")
    print(f"  {status}: {path.name}  ({len(entries)} строк)")

    if modified and not dry:
        path.write_text(text, encoding="utf-8")

    return entries

# ─── Дедупликация записей ─────────────────────────────────────────────────────

def deduplicate_entries(entries: list) -> list:
    seen: set = set()
    result = []
    for entry in entries:
        if entry[1] not in seen:
            seen.add(entry[1])
            result.append(entry)
    return result

# ─── Обработка Resources.resw ─────────────────────────────────────────────────

RESW_SKELETON = (
    '<?xml version="1.0" encoding="utf-8"?>\n'
    "<root>\n"
    '  <xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema"\n'
    '              xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">\n'
    '    <xsd:element name="root" msdata:IsDataSet="true"/>\n'
    "  </xsd:schema>\n"
    '  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>\n'
    '  <resheader name="version"><value>2.0</value></resheader>\n'
    "</root>\n"
)

def update_resw(resw_path: Path, entries: list, dry: bool) -> int:
    file_ok = resw_path.exists() and resw_path.stat().st_size > 0
    if file_ok:
        try:
            tree = ET.parse(resw_path)
            root = tree.getroot()
        except ET.ParseError:
            file_ok = False

    if not file_ok:
        root = ET.fromstring(RESW_SKELETON)
        tree = ET.ElementTree(root)

    existing = {d.get("name") for d in root.findall("data")}
    added = 0

    for _uid, key, value in entries:
        if key in existing:
            continue
        data = ET.SubElement(root, "data")
        data.set("name", key)
        data.set("xml:space", "preserve")
        ET.SubElement(data, "value").text = value
        existing.add(key)
        added += 1

    if dry:
        print(f"  [dry] {resw_path.name}: +{added} новых строк")
        return added

    if added:
        ET.indent(tree, space="  ")
        if not resw_path.parent.exists():
            resw_path.parent.mkdir(parents=True, exist_ok=True)
            print(f"  📁 Создана папка: {resw_path.parent}")
        else:
            resw_path.parent.mkdir(parents=True, exist_ok=True)
        tree.write(resw_path, encoding="utf-8", xml_declaration=True)
        print(f"  ✏  +{added} строк → {resw_path}")
    else:
        print(f"  ✓  Без изменений: {resw_path}")

    return added

# ─── Перевод ──────────────────────────────────────────────────────────────────

def _is_translatable(text: str) -> bool:
    return bool(text.strip()) and any(c.isalpha() for c in text)


def translate_google(texts: list, target_lang: str, source_lang: str = "ru") -> list:
    try:
        from deep_translator import GoogleTranslator
    except ImportError:
        print("  [!] Установите: pip install deep-translator")
        return texts

    tr = GoogleTranslator(source=source_lang, target=target_lang)
    result = []
    for text in texts:
        try:
            result.append(tr.translate(text) if _is_translatable(text) else text)
        except Exception as e:
            print(f"  [warn] Google: {text!r} → {e}")
            result.append(text)
    return result


def translate_ollama(
    texts: list,
    model: str = "qwen2.5:7b",
    target_lang: str = "English",
    source_lang: str = "Russian",
    url: str = "http://localhost:11434",
) -> list:
    import urllib.request, urllib.error

    numbered = chr(10).join(f"{i+1}. {t}" for i, t in enumerate(texts))
    prompt = (
        f"You are a professional UI translator. "
        f"Translate these {source_lang} desktop app strings to {target_lang}.\n"
        "Rules:\n"
        "- Short and natural for a desktop app UI\n"
        "- Preserve special characters (arrows, emoji) exactly as-is\n"
        "- Do NOT translate proper names: ZUI, zapret, WinDivert, Windows\n"
        "- Return ONLY a numbered list in the exact same format, nothing else\n\n"
        f"Strings:\n{numbered}"
    )

    body = json.dumps({
        "model": model,
        "prompt": prompt,
        "stream": True,
        "options": {"temperature": 0.1},
    }).encode("utf-8")

    try:
        req = urllib.request.Request(
            f"{url}/api/generate",
            data=body,
            headers={"Content-Type": "application/json"},
            method="POST",
        )
        chunks = []
        token_count = 0

        with urllib.request.urlopen(req, timeout=300) as resp:
            for line in resp:
                if not line.strip():
                    continue
                try:
                    chunk = json.loads(line)
                except json.JSONDecodeError:
                    continue
                chunks.append(chunk.get("response", ""))
                token_count += 1
                if token_count % 50 == 0:
                    print(f"     ...генерируем перевод ({token_count} токенов)", flush=True)
                if chunk.get("done"):
                    break

        raw = "".join(chunks).strip()
        print(f"     Перевод завершён ({token_count} токенов)")

        result = []
        for i, original in enumerate(texts):
            m = re.search(rf"^{i+1}[.)\s]+(.+)$", raw, re.MULTILINE)
            result.append(m.group(1).strip() if m else original)
        return result

    except urllib.error.URLError:
        print(f"  [!] Ollama недоступен ({url}). Запустите: ollama serve")
        return texts
    except Exception as e:
        print(f"  [!] Ошибка Ollama: {e}")
        return texts


def apply_translation(
    entries: list,
    target_lang: str = "en",
    source_lang: str = "ru",
    engine: str = "google",
    ollama_model: str = "qwen2.5:7b",
    ollama_url: str = "http://localhost:11434",
) -> list:
    values   = [v for _, _, v in entries]
    src_name = LANG_DISPLAY.get(source_lang, source_lang)
    tgt_name = LANG_DISPLAY.get(target_lang, target_lang)
    print(f"  🌐 {len(values)} строк  {src_name} → {tgt_name}  [{engine}]...")

    if engine == "ollama":
        translated = translate_ollama(values, ollama_model, tgt_name, src_name, ollama_url)
    else:
        BATCH = 50
        translated = []
        for i in range(0, len(values), BATCH):
            chunk = values[i:i + BATCH]
            n = i // BATCH + 1
            total = (len(values) + BATCH - 1) // BATCH
            print(f"     пакет {n}/{total} ({len(chunk)} строк)...")
            translated.extend(translate_google(chunk, target_lang, source_lang))

    return [(uid, key, tr) for (uid, key, _), tr in zip(entries, translated)]


def find_untranslated(target_resw: Path, source_resw: Path) -> dict:
    """Возвращает {key: src_value} для ключей где target == source или отсутствует."""
    def load(path: Path) -> dict:
        if not path.exists() or path.stat().st_size == 0:
            return {}
        try:
            tree = ET.parse(path)
            return {d.get("name"): (d.findtext("value") or "").strip()
                    for d in tree.getroot().findall("data")}
        except Exception:
            return {}

    source = load(source_resw)
    target = load(target_resw)
    return {k: v for k, v in source.items() if target.get(k) is None or target.get(k) == v}


def update_existing_translations(resw_path: Path, updates: dict) -> None:
    if not updates or not resw_path.exists():
        return
    try:
        tree = ET.parse(resw_path)
        root = tree.getroot()
        changed = 0
        for data in root.findall("data"):
            key = data.get("name", "")
            if key in updates:
                val = data.find("value")
                if val is not None:
                    val.text = updates[key]
                    changed += 1
        if changed:
            ET.indent(tree, space="  ")
            tree.write(resw_path, encoding="utf-8", xml_declaration=True)
            print(f"  ✏  Обновлено {changed} переводов → {resw_path.name}")
    except Exception as e:
        print(f"  [!] Ошибка обновления {resw_path.name}: {e}")


# ─── main ─────────────────────────────────────────────────────────────────────

def main():
    ap = argparse.ArgumentParser(description="XAML x:Uid + .resw generator")
    ap.add_argument("--root",         required=True)
    ap.add_argument("--resw",         default=None)
    ap.add_argument("--dry",          action="store_true")
    ap.add_argument("--controls",     default=",".join(DEFAULT_CONTROLS))
    ap.add_argument("--translate",    default=None,
                    help="Локали для перевода: en-US,de-DE,fr-FR,...")
    ap.add_argument("--source-lang",  default="ru",
                    help="Исходный язык (по умолчанию: ru)")
    ap.add_argument("--retranslate",  action="store_true")
    ap.add_argument("--engine",       default="google", choices=["google", "ollama"])
    ap.add_argument("--ollama-model", default="qwen2.5:7b")
    ap.add_argument("--ollama-url",   default="http://localhost:11434")
    args = ap.parse_args()

    root_dir = Path(args.root)
    controls = [c.strip() for c in args.controls.split(",") if c.strip()]
    src_lang = args.source_lang.lower()

    if args.resw:
        resw_paths = [Path(p.strip()) for p in args.resw.split(",")]
    else:
        src_locale = lang_to_locale(src_lang)
        resw_paths = [root_dir / "Strings" / src_locale / "Resources.resw"]
        if args.translate:
            for locale in [t.strip() for t in args.translate.split(",") if t.strip()]:
                p = root_dir / "Strings" / locale / "Resources.resw"
                if p not in resw_paths:
                    resw_paths.append(p)

    print(f"📁 Корень:        {root_dir.resolve()}")
    print(f"🌍 Исходный язык: {src_lang}  ({LANG_DISPLAY.get(src_lang, src_lang)})")
    for p in resw_paths:
        print(f"📄 .resw:         {p}")
    if args.dry:
        print("⚠️  DRY-RUN\n")
    print()

    SKIP_DIRS = {"obj", "bin", ".vs", "packages"}
    xaml_files = sorted(
        p for p in root_dir.rglob("*.xaml")
        if not any(part in SKIP_DIRS for part in p.relative_to(root_dir).parts)
    )
    if not xaml_files:
        print("Файлы .xaml не найдены.")
        return

    used_uids: set = set()
    uid_ctrl:  dict = {}
    all_entries: list = []

    for xaml_path in xaml_files:
        print(f"📄 {xaml_path.relative_to(root_dir)}")
        all_entries.extend(process_xaml(xaml_path, controls, used_uids, uid_ctrl, args.dry))

    all_entries = deduplicate_entries(all_entries)
    translate_locales = {t.strip() for t in args.translate.split(",") if t.strip()} if args.translate else set()

    print(f"\n{'─'*55}")
    print(f"Файлов:          {len(xaml_files)}")
    print(f"Строк для .resw: {len(all_entries)}")

    # Исходный .resw
    source_resw = next(
        (p for p in resw_paths if p.parent.name.lower().startswith(src_lang)),
        resw_paths[0],
    )

    for resw_path in resw_paths:
        locale    = resw_path.parent.name
        lang_code = locale.split("-")[0].lower()
        needs_tr  = bool(translate_locales and locale in translate_locales)
        is_source = (resw_path == source_resw)

        print(f"\n{'─'*55}")
        print(f"📄 {locale}  ({LANG_DISPLAY.get(lang_code, lang_code)})"
              f"{'  ← source' if is_source else ''}")

        if needs_tr and args.dry:
            print(f"  [dry] Перевод [{args.engine}] будет выполнен при реальном запуске")
            update_resw(resw_path, all_entries, args.dry)
            continue

        if needs_tr and not args.dry and not is_source:
            existing: set = set()
            if resw_path.exists() and resw_path.stat().st_size > 0:
                try:
                    existing = {d.get("name") for d in ET.parse(resw_path).getroot().findall("data")}
                except Exception:
                    pass

            new_entries = [(u, k, v) for u, k, v in all_entries if k not in existing]

            retranslate_entries = []
            if args.retranslate and source_resw != resw_path:
                untranslated = find_untranslated(resw_path, source_resw)
                retranslate_entries = [
                    (k.split(".")[0], k, v) for k, v in untranslated.items()
                    if k not in {ke for _, ke, _ in new_entries}
                ]
                if retranslate_entries:
                    print(f"  🔄 Непереведённых строк: {len(retranslate_entries)}")

            if new_entries:
                tr_new = apply_translation(new_entries, lang_code, src_lang,
                                           args.engine, args.ollama_model, args.ollama_url)
                update_resw(resw_path, tr_new, args.dry)
            else:
                update_resw(resw_path, all_entries, args.dry)

            if retranslate_entries:
                tr_old = apply_translation(retranslate_entries, lang_code, src_lang,
                                           args.engine, args.ollama_model, args.ollama_url)
                update_existing_translations(resw_path, {k: v for _, k, v in tr_old})
        else:
            update_resw(resw_path, all_entries, args.dry)

    print(f"\n{'─'*55}")
    print("✅ Готово!")


if __name__ == "__main__":
    main()
