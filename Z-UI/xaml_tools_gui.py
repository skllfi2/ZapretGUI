"""
xaml_tools_gui.py
─────────────────
Графический интерфейс для xaml_uid_generator.py и xaml_resw_cleanup.py.
Поддержка RU/EN интерфейса, выбор нескольких целевых языков, исходного языка.
Запуск: python xaml_tools_gui.py
"""

import tkinter as tk
from tkinter import ttk, filedialog, scrolledtext
import subprocess
import sys
import threading
import os
from pathlib import Path

# ─── Цвета ────────────────────────────────────────────────────────────────────

BG          = "#0f0f0f"
BG_CARD     = "#1a1a1a"
BG_INPUT    = "#141414"
BORDER      = "#2a2a2a"
ACCENT      = "#7c6af7"
ACCENT_DARK = "#5a4ed1"
GREEN       = "#4ade80"
RED         = "#f87171"
YELLOW      = "#fbbf24"
FG          = "#e8e8e8"
FG_DIM      = "#666666"
FG_MID      = "#999999"
FONT_MONO   = ("Consolas", 10)
FONT_UI     = ("Segoe UI", 10)
FONT_TITLE  = ("Segoe UI Semibold", 11)
FONT_SMALL  = ("Segoe UI", 9)

SCRIPTS_DIR = Path(__file__).parent

# ─── Доступные языки ──────────────────────────────────────────────────────────

# (locale, display_name, lang_code)
TARGET_LANGS = [
    ("ru-RU", "Русский",     "ru"),
    ("en-US", "English",     "en"),
    ("de-DE", "Deutsch",     "de"),
    ("fr-FR", "Français",    "fr"),
    ("es-ES", "Español",     "es"),
    ("zh-CN", "中文(简)",    "zh"),
    ("zh-TW", "中文(繁)",    "zh"),
    ("ja-JP", "日本語",      "ja"),
    ("ko-KR", "한국어",      "ko"),
    ("tr-TR", "Türkçe",      "tr"),
    ("pt-BR", "Português",   "pt"),
    ("it-IT", "Italiano",    "it"),
    ("pl-PL", "Polski",      "pl"),
    ("uk-UA", "Українська",  "uk"),
    ("ar-SA", "العربية",     "ar"),
    ("nl-NL", "Nederlands",  "nl"),
]

SOURCE_LANGS = [
    ("ru", "Русский"),
    ("en", "English"),
    ("de", "Deutsch"),
    ("fr", "Français"),
    ("es", "Español"),
    ("zh", "中文"),
    ("ja", "日本語"),
    ("tr", "Türkçe"),
    ("uk", "Українська"),
    ("pl", "Polski"),
    ("it", "Italiano"),
    ("pt", "Português"),
]

# ─── Строки интерфейса ────────────────────────────────────────────────────────

STRINGS: dict[str, dict[str, str]] = {
    "ru": {
        "header_rest":       " Localization Tools",
        "subtitle":          "Генерация x:Uid и синхронизация .resw файлов",
        "settings":          "Настройки",
        "root_label":        "Корень проекта",
        "resw_label":        ".resw пути (необязательно)",
        "resw_hint":         "через запятую",
        "dry_run":           "Dry-run",
        "retranslate":       "Перевести непереведённые",
        "keep_orphans":      "--keep-orphans",
        "source_lang_label": "Исходный язык:",
        "target_langs_label":"Целевые языки:",
        "engine_label":      "Движок перевода:",
        "engine_google":     "Google Translate",
        "engine_ollama":     "Ollama (локально)",
        "model_label":       "Модель:",
        "install_hint":      "Google: pip install deep-translator  |  Ollama: ollama serve",
        "btn_generator":     "▶  Генератор  (uid_generator)",
        "btn_cleanup":       "🧹  Очистка  (resw_cleanup)",
        "btn_clear":         "✕  Очистить вывод",
        "output_label":      "Вывод",
        "lines_fmt":         "{n} строк",
        "status_ready":      "Готов к запуску",
        "status_running":    "Уже выполняется...",
        "status_no_root":    "Укажите корень проекта",
        "status_models_ok":  "Найдено моделей: {n}",
        "status_no_models":  "Ollama: модели не найдены",
        "status_no_ollama":  "Ollama не установлен или не в PATH",
        "status_timeout":    "Ollama не отвечает (timeout)",
        "status_err":        "Ошибка: {e}",
        "status_ollama_ok":  "Ollama запущен",
        "status_ollama_off": "Ollama не запущен — нажмите ● Ollama",
        "status_wait_ollama":"Дождитесь запуска Ollama (● зелёный) и запустите снова",
        "status_done_ok":    "Завершено успешно",
        "status_done_err":   "Завершено с ошибкой (код {rc})",
        "status_launch_err": "Ошибка запуска",
        "status_ollama_started": "Ollama запущен",
        "status_ollama_stopped": "Ollama остановлен",
        "status_ollama_no_stop": "Ollama запущен не нами — остановите вручную",
        "status_ollama_already": "Ollama уже запущен",
        "out_starting_ollama":   "\n🚀 Запускаем ollama serve...\n",
        "out_ollama_ready":      "  ✓ Ollama готов\n",
        "out_ollama_timeout":    "  [!] Ollama не ответил за 10 секунд\n",
        "out_ollama_not_found":  "  [!] ollama не найден в PATH\n",
        "out_ollama_stopped":    "\n⏹ Ollama остановлен\n",
        "out_ollama_ensure":     "\n⚡ Ollama не запущен — запускаем...\n",
        "out_done_ok":           "\n✅ Завершено успешно\n",
        "out_done_err":          "\n❌ Завершено с ошибкой (код {rc})\n",
        "out_exception":         "\n[исключение] {e}\n",
        "out_not_found":         "[ошибка] Файл не найден: {path}\n",
        "lang_btn":              "EN",
        "browse_title":          "Выберите корень проекта",
        "no_targets_warn":       "Выберите хотя бы один целевой язык",
        "autodetect_ok":         "Язык определён: {lang}",
        "autodetect_fail":       "Не удалось определить язык",
        "autodetect_hint":       "🔍 определяю...",
    },
    "en": {
        "header_rest":       " Localization Tools",
        "subtitle":          "x:Uid generation and .resw file synchronization",
        "settings":          "Settings",
        "root_label":        "Project root",
        "resw_label":        ".resw paths (optional)",
        "resw_hint":         "comma-separated",
        "dry_run":           "Dry-run",
        "retranslate":       "Re-translate untranslated",
        "keep_orphans":      "--keep-orphans",
        "source_lang_label": "Source language:",
        "target_langs_label":"Target languages:",
        "engine_label":      "Translation engine:",
        "engine_google":     "Google Translate",
        "engine_ollama":     "Ollama (local)",
        "model_label":       "Model:",
        "install_hint":      "Google: pip install deep-translator  |  Ollama: ollama serve",
        "btn_generator":     "▶  Generator  (uid_generator)",
        "btn_cleanup":       "🧹  Cleanup  (resw_cleanup)",
        "btn_clear":         "✕  Clear output",
        "output_label":      "Output",
        "lines_fmt":         "{n} lines",
        "status_ready":      "Ready",
        "status_running":    "Already running...",
        "status_no_root":    "Specify project root",
        "status_models_ok":  "Models found: {n}",
        "status_no_models":  "Ollama: no models found",
        "status_no_ollama":  "Ollama not installed or not in PATH",
        "status_timeout":    "Ollama not responding (timeout)",
        "status_err":        "Error: {e}",
        "status_ollama_ok":  "Ollama running",
        "status_ollama_off": "Ollama not running — click ● Ollama",
        "status_wait_ollama":"Wait for Ollama to start (● green) then retry",
        "status_done_ok":    "Completed successfully",
        "status_done_err":   "Completed with error (code {rc})",
        "status_launch_err": "Launch error",
        "status_ollama_started": "Ollama started",
        "status_ollama_stopped": "Ollama stopped",
        "status_ollama_no_stop": "Ollama was not started by us — stop manually",
        "status_ollama_already": "Ollama already running",
        "out_starting_ollama":   "\n🚀 Starting ollama serve...\n",
        "out_ollama_ready":      "  ✓ Ollama ready\n",
        "out_ollama_timeout":    "  [!] Ollama did not respond within 10 seconds\n",
        "out_ollama_not_found":  "  [!] ollama not found in PATH\n",
        "out_ollama_stopped":    "\n⏹ Ollama stopped\n",
        "out_ollama_ensure":     "\n⚡ Ollama not running — starting...\n",
        "out_done_ok":           "\n✅ Completed successfully\n",
        "out_done_err":          "\n❌ Completed with error (code {rc})\n",
        "out_exception":         "\n[exception] {e}\n",
        "out_not_found":         "[error] File not found: {path}\n",
        "lang_btn":              "RU",
        "browse_title":          "Select project root",
        "no_targets_warn":       "Select at least one target language",
        "autodetect_ok":         "Language detected: {lang}",
        "autodetect_fail":       "Could not detect language",
        "autodetect_hint":       "🔍 detecting...",
    },
}

# ─── Автоопределение языка ────────────────────────────────────────────────────

def _char_block(ch: str) -> str | None:
    """Возвращает название Unicode-блока символа (для детектора языка)."""
    cp = ord(ch)
    if 0x0400 <= cp <= 0x04FF: return "cyrillic"
    if 0x4E00 <= cp <= 0x9FFF: return "cjk"
    if 0x3040 <= cp <= 0x309F or 0x30A0 <= cp <= 0x30FF: return "japanese"
    if 0xAC00 <= cp <= 0xD7AF: return "korean"
    if 0x0600 <= cp <= 0x06FF: return "arabic"
    if 0x0020 <= cp <= 0x007E and ch.isalpha(): return "latin"
    if 0x00C0 <= cp <= 0x024F and ch.isalpha(): return "latin_ext"
    return None

_BLOCK_TO_LANG: dict[str, str] = {
    "cyrillic":  "ru",
    "cjk":       "zh",
    "japanese":  "ja",
    "korean":    "ko",
    "arabic":    "ar",
    # latin / latin_ext — слишком неоднозначно, не маппируем
}

# ─── Главное окно ─────────────────────────────────────────────────────────────

class App(tk.Tk):
    def __init__(self):
        super().__init__()
        self._lang = "ru"
        self.title("XAML Localization Tools")
        self.configure(bg=BG)
        self.minsize(820, 680)
        self.geometry("960x780")

        try:
            self.iconbitmap(default="")
        except Exception:
            pass

        self._i18n: dict[str, tk.Widget] = {}
        # BooleanVar для каждого целевого языка: locale → BooleanVar
        self._target_vars: dict[str, tk.BooleanVar] = {
            loc: tk.BooleanVar(value=(loc == "en-US"))
            for loc, _, _ in TARGET_LANGS
        }
        self._detect_timer: str | None = None   # after() id для debounce

        self._build_ui()
        self._running     = False
        self._ollama_proc = None
        self._style_combobox()
        threading.Thread(target=self._refresh_models, daemon=True).start()
        self.after(600, self._check_ollama_status)

    def _s(self, key: str, **kw) -> str:
        tmpl = STRINGS[self._lang].get(key, key)
        return tmpl.format(**kw) if kw else tmpl

    # ─── Построение UI ────────────────────────────────────────────────────────

    def _build_ui(self):
        # ── Header ──
        hdr = tk.Frame(self, bg=BG)
        hdr.pack(fill="x", padx=24, pady=(20, 0))

        tk.Label(hdr, text="XAML", font=("Segoe UI Semibold", 22),
                 bg=BG, fg=ACCENT).pack(side="left")
        self._i18n["header_rest"] = tk.Label(
            hdr, text=self._s("header_rest"),
            font=("Segoe UI Light", 22), bg=BG, fg=FG)
        self._i18n["header_rest"].pack(side="left")

        # Кнопка переключения языка
        self._i18n["lang_btn"] = tk.Button(
            hdr, text=self._s("lang_btn"), command=self._switch_lang,
            bg=BG_CARD, fg=FG_MID, activebackground=BORDER, activeforeground=FG,
            font=("Segoe UI Semibold", 9), relief="flat", bd=0,
            padx=10, pady=4, cursor="hand2")
        self._i18n["lang_btn"].pack(side="right")

        self._i18n["subtitle"] = tk.Label(
            self, text=self._s("subtitle"),
            font=FONT_SMALL, bg=BG, fg=FG_DIM)
        self._i18n["subtitle"].pack(anchor="w", padx=24, pady=(2, 14))

        tk.Frame(self, bg=BORDER, height=1).pack(fill="x", padx=24)

        # ── Settings card ──
        card = tk.Frame(self, bg=BG_CARD, padx=20, pady=14)
        card.pack(fill="x", padx=24, pady=14)
        self._border(card)

        self._i18n["settings_lbl"] = tk.Label(
            card, text=self._s("settings"), font=FONT_TITLE, bg=BG_CARD, fg=FG)
        self._i18n["settings_lbl"].grid(row=0, column=0, columnspan=3, sticky="w", pady=(0, 10))

        # Project root
        self._i18n["root_label"] = tk.Label(
            card, text=self._s("root_label"), font=FONT_UI, bg=BG_CARD, fg=FG_MID)
        self._i18n["root_label"].grid(row=1, column=0, sticky="w", padx=(0, 12))
        self.root_var = tk.StringVar()
        self.root_var.trace_add("write", self._on_root_changed)
        self._entry(card, self.root_var).grid(row=1, column=1, sticky="ew", ipady=6, padx=(0, 8))
        self._make_btn(card, "📁", self._browse_root, small=True).grid(row=1, column=2)

        # .resw paths
        self._i18n["resw_label"] = tk.Label(
            card, text=self._s("resw_label"), font=FONT_UI, bg=BG_CARD, fg=FG_DIM)
        self._i18n["resw_label"].grid(row=2, column=0, sticky="w", padx=(0, 12), pady=(8, 0))
        self.resw_var = tk.StringVar()
        self._entry(card, self.resw_var).grid(row=2, column=1, sticky="ew", ipady=6,
                                               padx=(0, 8), pady=(8, 0))
        self._i18n["resw_hint"] = tk.Label(
            card, text=self._s("resw_hint"), font=FONT_SMALL, bg=BG_CARD, fg=FG_DIM)
        self._i18n["resw_hint"].grid(row=2, column=2, sticky="w", pady=(8, 0))

        card.columnconfigure(1, weight=1)

        # ── Flags row ──
        flags = tk.Frame(card, bg=BG_CARD)
        flags.grid(row=3, column=0, columnspan=3, sticky="w", pady=(12, 0))

        self.dry_var         = tk.BooleanVar(value=True)
        self.retranslate_var = tk.BooleanVar(value=False)
        self.orphans_var     = tk.BooleanVar(value=False)

        self._i18n["chk_dry"] = self._make_check(flags, self._s("dry_run"), self.dry_var)
        self._i18n["chk_dry"].pack(side="left", padx=(0, 16))
        self._i18n["chk_retranslate"] = self._make_check(
            flags, self._s("retranslate"), self.retranslate_var)
        self._i18n["chk_retranslate"].pack(side="left", padx=(0, 16))
        self._i18n["chk_orphans"] = self._make_check(
            flags, self._s("keep_orphans"), self.orphans_var)
        self._i18n["chk_orphans"].pack(side="left")

        # ── Source language row ──
        src_row = tk.Frame(card, bg=BG_CARD)
        src_row.grid(row=4, column=0, columnspan=3, sticky="w", pady=(12, 0))

        self._i18n["source_lang_label"] = tk.Label(
            src_row, text=self._s("source_lang_label"),
            font=FONT_UI, bg=BG_CARD, fg=FG_MID)
        self._i18n["source_lang_label"].pack(side="left", padx=(0, 10))

        self.source_lang_var = tk.StringVar(value="ru")
        src_values = [f"{code}  —  {name}" for code, name in SOURCE_LANGS]
        self.source_combo = ttk.Combobox(
            src_row, textvariable=self.source_lang_var,
            font=FONT_UI, width=22, state="readonly",
            values=src_values)
        self.source_combo.set(src_values[0])   # ru — Русский
        self.source_combo.pack(side="left", ipady=3)

        # Индикатор авто-определения
        self.autodetect_lbl = tk.Label(
            src_row, text="", font=FONT_SMALL, bg=BG_CARD, fg=FG_DIM)
        self.autodetect_lbl.pack(side="left", padx=(10, 0))

        # ── Target languages grid ──
        tgt_outer = tk.Frame(card, bg=BG_CARD)
        tgt_outer.grid(row=5, column=0, columnspan=3, sticky="w", pady=(12, 0))

        self._i18n["target_langs_label"] = tk.Label(
            tgt_outer, text=self._s("target_langs_label"),
            font=FONT_UI, bg=BG_CARD, fg=FG_MID)
        self._i18n["target_langs_label"].grid(row=0, column=0, sticky="nw", padx=(0, 12))

        tgt_grid = tk.Frame(tgt_outer, bg=BG_CARD)
        tgt_grid.grid(row=0, column=1, sticky="w")

        COLS = 5
        for idx, (locale, name, _) in enumerate(TARGET_LANGS):
            r, c = divmod(idx, COLS)
            cb = tk.Checkbutton(
                tgt_grid,
                text=f"{locale}  {name}",
                variable=self._target_vars[locale],
                font=FONT_SMALL, bg=BG_CARD, fg=FG_MID,
                activebackground=BG_CARD, activeforeground=FG,
                selectcolor=BG_INPUT, relief="flat", bd=0, cursor="hand2",
                anchor="w",
            )
            cb.grid(row=r, column=c, sticky="w", padx=(0, 16), pady=1)

        # ── Engine + model row ──
        eng_row = tk.Frame(card, bg=BG_CARD)
        eng_row.grid(row=6, column=0, columnspan=3, sticky="w", pady=(12, 0))

        self._i18n["engine_label"] = tk.Label(
            eng_row, text=self._s("engine_label"),
            font=FONT_UI, bg=BG_CARD, fg=FG_MID)
        self._i18n["engine_label"].pack(side="left", padx=(0, 10))

        self.engine_var = tk.StringVar(value="google")
        self._i18n["radio_google"] = self._make_radio(
            eng_row, self._s("engine_google"), "google", self.engine_var)
        self._i18n["radio_google"].pack(side="left", padx=(0, 14))
        self._i18n["radio_ollama"] = self._make_radio(
            eng_row, self._s("engine_ollama"), "ollama", self.engine_var)
        self._i18n["radio_ollama"].pack(side="left", padx=(0, 14))

        self._i18n["model_label"] = tk.Label(
            eng_row, text=self._s("model_label"),
            font=FONT_UI, bg=BG_CARD, fg=FG_DIM)
        self._i18n["model_label"].pack(side="left", padx=(0, 6))

        self.model_var   = tk.StringVar(value="qwen2.5:7b")
        self.model_combo = ttk.Combobox(
            eng_row, textvariable=self.model_var, font=FONT_UI,
            width=24, state="normal",
            values=["qwen2.5:7b", "llama3.1:8b", "mistral:7b", "gemma2:9b"])
        self.model_combo.pack(side="left", ipady=3, padx=(0, 6))

        self._make_btn(eng_row, "↻", self._refresh_models_async, small=True).pack(side="left")

        self._i18n["install_hint"] = tk.Label(
            card, text=self._s("install_hint"),
            font=FONT_SMALL, bg=BG_CARD, fg=FG_DIM)
        self._i18n["install_hint"].grid(row=7, column=0, columnspan=3,
                                         sticky="w", pady=(6, 0))

        # ── Run buttons ──
        btn_frame = tk.Frame(self, bg=BG)
        btn_frame.pack(fill="x", padx=24, pady=(0, 10))

        self.gen_btn   = self._make_btn(btn_frame, self._s("btn_generator"), self._run_generator)
        self.gen_btn.pack(side="left", padx=(0, 10))
        self._i18n["gen_btn"] = self.gen_btn

        self.clean_btn = self._make_btn(btn_frame, self._s("btn_cleanup"),
                                        self._run_cleanup, secondary=True)
        self.clean_btn.pack(side="left", padx=(0, 10))
        self._i18n["clean_btn"] = self.clean_btn

        self.clear_btn = self._make_btn(btn_frame, self._s("btn_clear"),
                                        self._clear_output, ghost=True)
        self.clear_btn.pack(side="right")
        self._i18n["clear_btn"] = self.clear_btn

        # ── Status bar ──
        sbar = tk.Frame(self, bg=BG_CARD)
        sbar.pack(fill="x", padx=24)
        self._border(sbar)

        self.status_dot = tk.Label(sbar, text="●", font=FONT_UI, bg=BG_CARD, fg=FG_DIM)
        self.status_dot.pack(side="left", padx=(12, 6), pady=8)
        self.status_label = tk.Label(sbar, text=self._s("status_ready"),
                                      font=FONT_UI, bg=BG_CARD, fg=FG_DIM)
        self.status_label.pack(side="left", pady=8)

        self.ollama_dot = tk.Label(sbar, text="● Ollama",
                                    font=FONT_UI, bg=BG_CARD, fg=FG_DIM, cursor="hand2")
        self.ollama_dot.pack(side="right", padx=(0, 12), pady=8)
        self.ollama_dot.bind("<Button-1>", lambda e: self._toggle_ollama())

        self.progress = ttk.Progressbar(sbar, mode="indeterminate", length=120)
        self.progress.pack(side="right", padx=12, pady=10)

        # ── Output ──
        out_hdr = tk.Frame(self, bg=BG)
        out_hdr.pack(fill="x", padx=24, pady=(8, 4))
        self._i18n["output_label"] = tk.Label(
            out_hdr, text=self._s("output_label"), font=FONT_TITLE, bg=BG, fg=FG)
        self._i18n["output_label"].pack(side="left")
        self.line_count = tk.Label(out_hdr, text="", font=FONT_SMALL, bg=BG, fg=FG_DIM)
        self.line_count.pack(side="right")

        self.output = scrolledtext.ScrolledText(
            self, font=FONT_MONO, bg=BG_CARD, fg=FG,
            insertbackground=FG, relief="flat", bd=0,
            padx=16, pady=12, wrap="none",
            highlightthickness=1, highlightbackground=BORDER)
        self.output.pack(fill="both", expand=True, padx=24, pady=(0, 18))

        for tag, color in [("ok", GREEN), ("err", RED), ("warn", YELLOW),
                            ("accent", ACCENT), ("dim", FG_DIM)]:
            self.output.tag_config(tag, foreground=color)
        self.output.tag_config("header", foreground=FG, font=("Consolas", 10, "bold"))
        self.output.config(state="disabled")

    # ─── Смена языка ──────────────────────────────────────────────────────────

    def _switch_lang(self):
        self._lang = "en" if self._lang == "ru" else "ru"
        self._apply_i18n()

    def _apply_i18n(self):
        mapping = {
            "header_rest":        "header_rest",
            "subtitle":           "subtitle",
            "settings_lbl":       "settings",
            "root_label":         "root_label",
            "resw_label":         "resw_label",
            "resw_hint":          "resw_hint",
            "chk_dry":            "dry_run",
            "chk_retranslate":    "retranslate",
            "chk_orphans":        "keep_orphans",
            "source_lang_label":  "source_lang_label",
            "target_langs_label": "target_langs_label",
            "engine_label":       "engine_label",
            "radio_google":       "engine_google",
            "radio_ollama":       "engine_ollama",
            "model_label":        "model_label",
            "install_hint":       "install_hint",
            "output_label":       "output_label",
            "lang_btn":           "lang_btn",
        }
        for wkey, skey in mapping.items():
            w = self._i18n.get(wkey)
            if w:
                w.config(text=self._s(skey))

        # Кнопки запуска
        self.gen_btn.config(text=self._s("btn_generator"))
        self.clean_btn.config(text=self._s("btn_cleanup"))
        self.clear_btn.config(text=self._s("btn_clear"))

        # Статус-лейбл если показывает "Готов"
        ready = {STRINGS["ru"]["status_ready"], STRINGS["en"]["status_ready"]}
        if self.status_label.cget("text") in ready:
            self.status_label.config(text=self._s("status_ready"))

    # ─── Вспомогательные виджеты ──────────────────────────────────────────────

    def _border(self, w):
        w.config(highlightthickness=1, highlightbackground=BORDER, highlightcolor=BORDER)

    def _entry(self, parent, var):
        return tk.Entry(parent, textvariable=var, font=FONT_UI,
                        bg=BG_INPUT, fg=FG, insertbackground=FG,
                        relief="flat", bd=0, highlightthickness=1,
                        highlightbackground=BORDER, highlightcolor=ACCENT)

    def _make_btn(self, parent, text, cmd, small=False, secondary=False, ghost=False):
        if ghost:
            bg, fg, abg = BG, FG_DIM, BG_CARD
        elif secondary:
            bg, fg, abg = BG_CARD, FG, "#252525"
        else:
            bg, fg, abg = ACCENT, "#ffffff", ACCENT_DARK

        font = FONT_SMALL if small else ("Segoe UI Semibold", 10)
        pad  = (8, 6)     if small else (18, 9)

        btn = tk.Button(parent, text=text, command=cmd,
                        bg=bg, fg=fg, activebackground=abg, activeforeground=fg,
                        font=font, relief="flat", bd=0,
                        padx=pad[0], pady=pad[1], cursor="hand2")
        btn.bind("<Enter>", lambda e: btn.config(bg=abg))
        btn.bind("<Leave>", lambda e: btn.config(bg=bg))
        return btn

    def _make_check(self, parent, text, var):
        return tk.Checkbutton(parent, text=text, variable=var,
                              font=FONT_UI, bg=BG_CARD, fg=FG_MID,
                              activebackground=BG_CARD, activeforeground=FG,
                              selectcolor=BG_INPUT, relief="flat", bd=0, cursor="hand2")

    def _make_radio(self, parent, text, value, var):
        return tk.Radiobutton(parent, text=text, variable=var, value=value,
                              font=FONT_UI, bg=BG_CARD, fg=FG_MID,
                              activebackground=BG_CARD, activeforeground=FG,
                              selectcolor=BG_INPUT, relief="flat", cursor="hand2")

    # ─── Ollama ───────────────────────────────────────────────────────────────

    def _is_ollama_running(self) -> bool:
        import urllib.request
        try:
            urllib.request.urlopen("http://localhost:11434", timeout=2)
            return True
        except Exception:
            return False

    def _ensure_ollama_running(self) -> bool:
        if self._is_ollama_running():
            return True
        self._append(self._s("out_ollama_ensure"), "warn")
        self._start_ollama()
        self._set_status(self._s("status_wait_ollama"), "warn")
        return False

    def _refresh_models_async(self):
        threading.Thread(target=self._refresh_models, daemon=True).start()

    def _refresh_models(self):
        try:
            result = subprocess.run(["ollama", "list"],
                                    capture_output=True, text=True, timeout=5)
            models = [
                line.split()[0] for line in result.stdout.strip().splitlines()[1:]
                if line.split() and "embed" not in line.split()[0].lower()
            ]
            if models:
                def _apply():
                    self.model_combo["values"] = models
                    if self.model_var.get() not in models:
                        self.model_var.set(models[0])
                    self._set_status(self._s("status_models_ok", n=len(models)), "ok")
                self.after(0, _apply)
            else:
                self.after(0, lambda: self._set_status(self._s("status_no_models"), "warn"))
        except FileNotFoundError:
            self.after(0, lambda: self._set_status(self._s("status_no_ollama"), "err"))
        except subprocess.TimeoutExpired:
            self.after(0, lambda: self._set_status(self._s("status_timeout"), "err"))
        except Exception as e:
            self.after(0, lambda: self._set_status(self._s("status_err", e=e), "err"))

    def _check_ollama_status(self, silent=True):
        running = self._is_ollama_running()
        color = GREEN if running else FG_DIM
        self.ollama_dot.config(fg=color)
        if not silent:
            key = "status_ollama_ok" if running else "status_ollama_off"
            self._set_status(self._s(key), "ok" if running else "warn")
        return running

    def _toggle_ollama(self):
        if self._is_ollama_running():
            self._stop_ollama()
        else:
            self._start_ollama()

    def _start_ollama(self):
        if self._is_ollama_running():
            self._set_status(self._s("status_ollama_already"), "ok")
            self._check_ollama_status()
            return
        try:
            self._append(self._s("out_starting_ollama"), "accent")
            self._ollama_proc = subprocess.Popen(
                ["ollama", "serve"],
                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            self.ollama_dot.config(fg=YELLOW)
            self._wait_ollama_ready()
        except FileNotFoundError:
            self._set_status(self._s("status_no_ollama"), "err")
            self._append(self._s("out_ollama_not_found"), "err")

    def _wait_ollama_ready(self, attempt=0):
        if self._is_ollama_running():
            self.ollama_dot.config(fg=GREEN)
            self._set_status(self._s("status_ollama_started"), "ok")
            self._append(self._s("out_ollama_ready"), "ok")
            threading.Thread(target=self._refresh_models, daemon=True).start()
        elif attempt < 20:
            self.after(500, lambda: self._wait_ollama_ready(attempt + 1))
        else:
            self.ollama_dot.config(fg=RED)
            self._set_status(self._s("status_timeout"), "err")
            self._append(self._s("out_ollama_timeout"), "err")

    def _stop_ollama(self):
        if self._ollama_proc and self._ollama_proc.poll() is None:
            self._ollama_proc.terminate()
            self._ollama_proc = None
            self.ollama_dot.config(fg=FG_DIM)
            self._set_status(self._s("status_ollama_stopped"), "warn")
            self._append(self._s("out_ollama_stopped"), "warn")
        else:
            self._set_status(self._s("status_ollama_no_stop"), "warn")

    # ─── Действия ─────────────────────────────────────────────────────────────

    def _style_combobox(self):
        style = ttk.Style(self)
        style.theme_use("clam")
        style.configure("TCombobox",
                        fieldbackground=BG_INPUT, background=BG_CARD,
                        foreground=FG, selectbackground=ACCENT,
                        selectforeground="#ffffff", bordercolor=BORDER,
                        arrowcolor=FG_DIM)
        self.option_add("*TCombobox*Listbox.background", BG_CARD)
        self.option_add("*TCombobox*Listbox.foreground", FG)
        self.option_add("*TCombobox*Listbox.selectBackground", ACCENT)
        self.option_add("*TCombobox*Listbox.selectForeground", "#ffffff")

    def _browse_root(self):
        path = filedialog.askdirectory(title=self._s("browse_title"))
        if path:
            self.root_var.set(path)

    # ─── Авто-определение исходного языка ─────────────────────────────────────

    def _on_root_changed(self, *_):
        """Debounce: запускаем детектор через 700ms после последнего изменения."""
        if self._detect_timer:
            self.after_cancel(self._detect_timer)
        self._detect_timer = self.after(700, self._trigger_detect)

    def _trigger_detect(self):
        root = self.root_var.get().strip()
        if not root or len(root) < 3:
            return
        self.autodetect_lbl.config(text=self._s("autodetect_hint"), fg=FG_DIM)
        threading.Thread(target=self._detect_source_lang, args=(root,), daemon=True).start()

    def _detect_source_lang(self, root: str):
        """
        Определяет исходный язык по двум методам (в порядке приоритета):
          1. Ищет существующие папки Strings/<locale>/ — если есть одна локаль,
             вероятнее всего это и есть исходный язык.
          2. Сканирует до 10 .xaml файлов, считает доминирующий Unicode-блок
             в значениях атрибутов и текстовых узлах.
        """
        from pathlib import Path as _P
        import re as _re

        root_path = _P(root)
        if not root_path.exists():
            self.after(0, lambda: self.autodetect_lbl.config(text="", fg=FG_DIM))
            return

        detected: str | None = None

        # ── Метод 1: папки Strings/ ──
        strings_dir = root_path / "Strings"
        if strings_dir.exists():
            locales = [d.name for d in strings_dir.iterdir()
                       if d.is_dir() and (d / "Resources.resw").exists()]
            if len(locales) == 1:
                detected = locales[0].split("-")[0].lower()

        # ── Метод 2: анализ текста в .xaml ──
        if detected is None:
            skip = {"obj", "bin", ".vs", "packages"}
            xaml_files = [
                p for p in root_path.rglob("*.xaml")
                if not any(part in skip for part in p.relative_to(root_path).parts)
            ][:10]

            # Считаем символы по блокам
            counters: dict[str, int] = {}
            attr_re = _re.compile(r'(?:Text|Content|Header|Label|PlaceholderText)="([^"]{2,})"')

            for f in xaml_files:
                try:
                    text = f.read_text(encoding="utf-8-sig", errors="ignore")
                except OSError:
                    continue
                for m in attr_re.finditer(text):
                    val = m.group(1)
                    if val.startswith("{"):
                        continue
                    for ch in val:
                        block = _char_block(ch)
                        if block:
                            counters[block] = counters.get(block, 0) + 1

            if counters:
                dominant = max(counters, key=counters.__getitem__)
                detected = _BLOCK_TO_LANG.get(dominant)

        # ── Применяем результат ──
        if detected:
            src_values = [f"{code}  —  {name}" for code, name in SOURCE_LANGS]
            match = next((v for v in src_values if v.startswith(detected + "  ")), None)
            display = dict(SOURCE_LANGS).get(detected, detected)

            def _apply():
                if match:
                    self.source_combo.set(match)
                self.autodetect_lbl.config(
                    text=self._s("autodetect_ok", lang=display), fg=GREEN)
            self.after(0, _apply)
        else:
            self.after(0, lambda: self.autodetect_lbl.config(
                text=self._s("autodetect_fail"), fg=FG_DIM))

    def _clear_output(self):
        self.output.config(state="normal")
        self.output.delete("1.0", "end")
        self.output.config(state="disabled")
        self.line_count.config(text="")

    def _get_source_lang(self) -> str:
        """Возвращает код исходного языка (ru, en, de, ...)."""
        raw = self.source_combo.get()
        return raw.split("  ")[0].strip()

    def _get_selected_targets(self) -> list[str]:
        """Возвращает список выбранных целевых локалей."""
        return [loc for loc, var in self._target_vars.items() if var.get()]

    def _build_base_args(self):
        args = []
        root = self.root_var.get().strip()
        if not root:
            self._set_status(self._s("status_no_root"), "err")
            return None
        args += ["--root", root]
        resw = self.resw_var.get().strip()
        if resw:
            args += ["--resw", resw]
        if self.dry_var.get():
            args.append("--dry")
        return args

    def _build_generator_args(self):
        args = self._build_base_args()
        if args is None:
            return None

        src = self._get_source_lang()
        args += ["--source-lang", src]

        targets = self._get_selected_targets()
        # Фильтруем: не переводим в исходный язык
        targets = [t for t in targets if not t.lower().startswith(src)]

        if targets:
            args += ["--translate", ",".join(targets)]
            args += ["--engine", self.engine_var.get()]
            if self.engine_var.get() == "ollama":
                model = self.model_var.get().strip()
                if model:
                    args += ["--ollama-model", model]

        if self.retranslate_var.get():
            args.append("--retranslate")

        return args

    def _run_generator(self):
        args = self._build_generator_args()
        if args is None:
            return
        if "--engine" in args:
            idx = args.index("--engine")
            if idx + 1 < len(args) and args[idx + 1] == "ollama":
                if not self._ensure_ollama_running():
                    return
        self._run_script("xaml_uid_generator.py", args)

    def _run_cleanup(self):
        args = self._build_base_args()
        if args is None:
            return
        if self.orphans_var.get():
            args.append("--keep-orphans")
        self._run_script("xaml_resw_cleanup.py", args)

    def _run_script(self, script_name: str, args: list):
        if self._running:
            self._set_status(self._s("status_running"), "warn")
            return

        script_path = SCRIPTS_DIR / script_name
        if not script_path.exists():
            self._append(self._s("out_not_found", path=script_path), "err")
            return

        cmd = [sys.executable, str(script_path)] + args
        self._set_status(f"▶  {script_name}...", "accent")
        self._set_running(True)
        self._append(f"\n{'─'*60}\n", "dim")
        self._append(f"▶  {' '.join(cmd)}\n", "accent")
        self._append(f"{'─'*60}\n", "dim")
        threading.Thread(target=self._stream_output, args=(cmd,), daemon=True).start()

    def _stream_output(self, cmd):
        try:
            proc = subprocess.Popen(
                cmd,
                stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                text=True, encoding="utf-8", errors="replace",
                env={**os.environ, "PYTHONIOENCODING": "utf-8"})
            for line in proc.stdout:
                self._append_colored(line)
            proc.wait()
            rc = proc.returncode
            if rc == 0:
                self._set_status(self._s("status_done_ok"), "ok")
                self._append(self._s("out_done_ok"), "ok")
            else:
                self._set_status(self._s("status_done_err", rc=rc), "err")
                self._append(self._s("out_done_err", rc=rc), "err")
        except Exception as e:
            self._append(self._s("out_exception", e=e), "err")
            self._set_status(self._s("status_launch_err"), "err")
        finally:
            self._set_running(False)

    # ─── Подсветка вывода ─────────────────────────────────────────────────────

    def _append_colored(self, line: str):
        lo = line.lower()
        if any(x in lo for x in ["✅", "✓", "без изменений", "without changes", "готово"]):
            tag = "ok"
        elif any(x in lo for x in ["[!]", "parseerror", "ошибка", "error", "❌"]):
            tag = "err"
        elif any(x in lo for x in ["⚠", "warn", "[dry]", "dry-run", "конфликт"]):
            tag = "warn"
        elif any(x in lo for x in ["📄", "📁", "▶", "✏", "🌐", "корень", "source", "locale"]):
            tag = "accent"
        elif any(x in lo for x in ["✗", "будет удалено", "нумерованный"]):
            tag = "err"
        elif any(x in lo for x in ["─", "файлов:", "строк", "lines", "files:"]):
            tag = "header"
        else:
            tag = None
        self._append(line, tag)

    def _append(self, text: str, tag: str = None):
        def _do():
            self.output.config(state="normal")
            if tag:
                self.output.insert("end", text, tag)
            else:
                self.output.insert("end", text)
            self.output.see("end")
            self.output.config(state="disabled")
            n = int(self.output.index("end-1c").split(".")[0])
            self.line_count.config(text=self._s("lines_fmt", n=n))
        self.after(0, _do)

    def _set_status(self, text: str, kind: str = "dim"):
        colors = {"ok": GREEN, "err": RED, "warn": YELLOW, "accent": ACCENT, "dim": FG_DIM}
        color = colors.get(kind, FG_DIM)
        def _do():
            self.status_dot.config(fg=color)
            self.status_label.config(text=text, fg=color)
        self.after(0, _do)

    def _set_running(self, running: bool):
        self._running = running
        def _do():
            state = "disabled" if running else "normal"
            self.gen_btn.config(state=state)
            self.clean_btn.config(state=state)
            if running:
                self.progress.start(12)
            else:
                self.progress.stop()
        self.after(0, _do)


# ─── Запуск ───────────────────────────────────────────────────────────────────

if __name__ == "__main__":
    app = App()

    def _on_close():
        if app._ollama_proc and app._ollama_proc.poll() is None:
            app._ollama_proc.terminate()
        app.destroy()

    app.protocol("WM_DELETE_WINDOW", _on_close)
    app.mainloop()
