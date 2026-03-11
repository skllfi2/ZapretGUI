"""
xaml_tools_gui.py
─────────────────
Графический интерфейс для xaml_uid_generator.py и xaml_resw_cleanup.py.
Запуск: python xaml_tools_gui.py
"""

import tkinter as tk
from tkinter import ttk, filedialog, scrolledtext
import subprocess
import sys
import threading
import os
from pathlib import Path

# ─── Цвета и стили ────────────────────────────────────────────────────────────

BG          = "#0f0f0f"
BG_CARD     = "#1a1a1a"
BG_INPUT    = "#141414"
BORDER      = "#2a2a2a"
ACCENT      = "#7c6af7"        # фиолетовый
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

SCRIPTS_DIR = Path(__file__).parent

# ─── Главное окно ─────────────────────────────────────────────────────────────

class App(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("XAML Localization Tools")
        self.configure(bg=BG)
        self.minsize(780, 620)
        self.geometry("900x700")

        # Иконка (пустая, чтобы убрать дефолтную)
        try:
            self.iconbitmap(default="")
        except Exception:
            pass

        self._build_ui()
        self._running = False
        self._style_combobox()
        # Автоматически подгружаем модели при старте
        self.after(500, self._refresh_models)
        self._ollama_proc = None   # процесс ollama serve если мы его запустили
        self.after(600, self._check_ollama_status)

    # ─── UI ───────────────────────────────────────────────────────────────────

    def _build_ui(self):
        # ── Header ──
        header = tk.Frame(self, bg=BG, pady=0)
        header.pack(fill="x", padx=24, pady=(20, 0))

        tk.Label(header, text="XAML", font=("Segoe UI Semibold", 22),
                 bg=BG, fg=ACCENT).pack(side="left")
        tk.Label(header, text=" Localization Tools", font=("Segoe UI Light", 22),
                 bg=BG, fg=FG).pack(side="left")

        tk.Label(self, text="Генерация x:Uid и синхронизация .resw файлов",
                 font=("Segoe UI", 10), bg=BG, fg=FG_DIM).pack(anchor="w", padx=24, pady=(2, 16))

        sep = tk.Frame(self, bg=BORDER, height=1)
        sep.pack(fill="x", padx=24)

        # ── Settings card ──
        card = tk.Frame(self, bg=BG_CARD, padx=20, pady=16)
        card.pack(fill="x", padx=24, pady=16)
        self._add_border(card)

        tk.Label(card, text="Настройки", font=FONT_TITLE, bg=BG_CARD, fg=FG).grid(
            row=0, column=0, columnspan=3, sticky="w", pady=(0, 12))

        # Root
        tk.Label(card, text="Корень проекта", font=FONT_UI,
                 bg=BG_CARD, fg=FG_MID).grid(row=1, column=0, sticky="w", padx=(0, 12))
        self.root_var = tk.StringVar()
        root_entry = tk.Entry(card, textvariable=self.root_var, font=FONT_UI,
                              bg=BG_INPUT, fg=FG, insertbackground=FG,
                              relief="flat", bd=0, highlightthickness=1,
                              highlightbackground=BORDER, highlightcolor=ACCENT)
        root_entry.grid(row=1, column=1, sticky="ew", ipady=6, padx=(0, 8))
        browse_btn = self._make_btn(card, "📁", self._browse_root, small=True)
        browse_btn.grid(row=1, column=2)

        # resw (опционально)
        tk.Label(card, text=".resw пути (необязательно)", font=FONT_UI,
                 bg=BG_CARD, fg=FG_DIM).grid(row=2, column=0, sticky="w", padx=(0, 12), pady=(10, 0))
        self.resw_var = tk.StringVar()
        resw_entry = tk.Entry(card, textvariable=self.resw_var, font=FONT_UI,
                              bg=BG_INPUT, fg=FG, insertbackground=FG,
                              relief="flat", bd=0, highlightthickness=1,
                              highlightbackground=BORDER, highlightcolor=ACCENT)
        resw_entry.grid(row=2, column=1, sticky="ew", ipady=6, padx=(0, 8), pady=(10, 0))
        tk.Label(card, text="через запятую", font=("Segoe UI", 9),
                 bg=BG_CARD, fg=FG_DIM).grid(row=2, column=2, sticky="w", pady=(10, 0))

        card.columnconfigure(1, weight=1)

        # Флаги
        flags_frame = tk.Frame(card, bg=BG_CARD)
        flags_frame.grid(row=3, column=0, columnspan=3, sticky="w", pady=(14, 0))

        self.dry_var = tk.BooleanVar(value=True)
        self.orphans_var = tk.BooleanVar(value=False)
        self.translate_var = tk.BooleanVar(value=False)
        self.retranslate_var = tk.BooleanVar(value=False)

        self._make_check(flags_frame, "Dry-run", self.dry_var).pack(side="left", padx=(0, 16))
        self._make_check(flags_frame, "Переводить en-US", self.translate_var).pack(side="left", padx=(0, 16))
        self._make_check(flags_frame, "Перевести непереведённые", self.retranslate_var).pack(side="left", padx=(0, 16))
        self._make_check(flags_frame, "--keep-orphans", self.orphans_var).pack(side="left")

        # Движок перевода
        engine_frame = tk.Frame(card, bg=BG_CARD)
        engine_frame.grid(row=4, column=0, columnspan=3, sticky="w", pady=(12, 0))

        tk.Label(engine_frame, text="Движок перевода:", font=FONT_UI,
                 bg=BG_CARD, fg=FG_MID).pack(side="left", padx=(0, 10))

        self.engine_var = tk.StringVar(value="google")
        self._make_radio(engine_frame, "Google Translate", "google", self.engine_var).pack(side="left", padx=(0, 16))
        self._make_radio(engine_frame, "Ollama (локально)", "ollama", self.engine_var).pack(side="left", padx=(0, 16))

        tk.Label(engine_frame, text="Модель:", font=FONT_UI,
                 bg=BG_CARD, fg=FG_DIM).pack(side="left", padx=(0, 6))
        self.model_var = tk.StringVar(value="llama3.1:8b")
        self.model_combo = ttk.Combobox(
            engine_frame, textvariable=self.model_var, font=FONT_UI,
            width=24, state="normal",
            values=["llama3.1:8b", "qwen2.5:7b", "mistral:7b", "gemma2:9b"]
        )
        self.model_combo.pack(side="left", ipady=3, padx=(0, 6))
        refresh_btn = self._make_btn(engine_frame, "↻", self._refresh_models, small=True)
        refresh_btn.pack(side="left")

        tk.Label(card, text="Google: pip install deep-translator  |  Ollama: ollama serve",
                 font=("Segoe UI", 9), bg=BG_CARD, fg=FG_DIM).grid(
            row=5, column=0, columnspan=3, sticky="w", pady=(6, 0))

        # ── Кнопки запуска ──
        btn_frame = tk.Frame(self, bg=BG)
        btn_frame.pack(fill="x", padx=24, pady=(0, 12))

        self.gen_btn = self._make_btn(btn_frame, "▶  Генератор  (uid_generator)", self._run_generator)
        self.gen_btn.pack(side="left", padx=(0, 10))

        self.clean_btn = self._make_btn(btn_frame, "🧹  Очистка  (resw_cleanup)", self._run_cleanup, secondary=True)
        self.clean_btn.pack(side="left", padx=(0, 10))

        self.clear_btn = self._make_btn(btn_frame, "✕  Очистить вывод", self._clear_output, ghost=True)
        self.clear_btn.pack(side="right")

        # ── Статус-бар ──
        self.status_bar = tk.Frame(self, bg=BG_CARD, pady=0)
        self.status_bar.pack(fill="x", padx=24)
        self._add_border(self.status_bar, top=True)

        self.status_dot = tk.Label(self.status_bar, text="●", font=("Segoe UI", 10),
                                   bg=BG_CARD, fg=FG_DIM)
        self.status_dot.pack(side="left", padx=(12, 6), pady=8)
        self.status_label = tk.Label(self.status_bar, text="Готов к запуску",
                                     font=FONT_UI, bg=BG_CARD, fg=FG_DIM)
        self.status_label.pack(side="left", pady=8)

        # Ollama статус
        self.ollama_dot = tk.Label(self.status_bar, text="● Ollama",
                                   font=FONT_UI, bg=BG_CARD, fg=FG_DIM,
                                   cursor="hand2")
        self.ollama_dot.pack(side="right", padx=(0, 12), pady=8)
        self.ollama_dot.bind("<Button-1>", lambda e: self._toggle_ollama())

        self.progress = ttk.Progressbar(self.status_bar, mode="indeterminate", length=120)
        self.progress.pack(side="right", padx=12, pady=10)

        # ── Вывод ──
        out_label_frame = tk.Frame(self, bg=BG)
        out_label_frame.pack(fill="x", padx=24, pady=(8, 4))
        tk.Label(out_label_frame, text="Вывод", font=FONT_TITLE, bg=BG, fg=FG).pack(side="left")
        self.line_count = tk.Label(out_label_frame, text="", font=("Segoe UI", 9),
                                   bg=BG, fg=FG_DIM)
        self.line_count.pack(side="right")

        self.output = scrolledtext.ScrolledText(
            self, font=FONT_MONO, bg=BG_CARD, fg=FG,
            insertbackground=FG, relief="flat", bd=0,
            padx=16, pady=12, wrap="none",
            highlightthickness=1, highlightbackground=BORDER,
        )
        self.output.pack(fill="both", expand=True, padx=24, pady=(0, 20))

        # Цвета для подсветки вывода
        self.output.tag_config("ok",      foreground=GREEN)
        self.output.tag_config("err",     foreground=RED)
        self.output.tag_config("warn",    foreground=YELLOW)
        self.output.tag_config("accent",  foreground=ACCENT)
        self.output.tag_config("dim",     foreground=FG_DIM)
        self.output.tag_config("header",  foreground=FG, font=("Consolas", 10, "bold"))
        self.output.config(state="disabled")

    # ─── Вспомогательные виджеты ──────────────────────────────────────────────

    def _add_border(self, widget, top=False):
        widget.config(
            highlightthickness=1,
            highlightbackground=BORDER,
            highlightcolor=BORDER,
        )

    def _make_btn(self, parent, text, cmd, small=False, secondary=False, ghost=False):
        if ghost:
            bg, fg, abg = BG, FG_DIM, BG_CARD
        elif secondary:
            bg, fg, abg = BG_CARD, FG, "#252525"
        else:
            bg, fg, abg = ACCENT, "#ffffff", ACCENT_DARK

        font = ("Segoe UI", 9) if small else ("Segoe UI Semibold", 10)
        pad = (8, 6) if small else (18, 9)

        btn = tk.Button(parent, text=text, command=cmd,
                        bg=bg, fg=fg, activebackground=abg, activeforeground=fg,
                        font=font, relief="flat", bd=0, padx=pad[0], pady=pad[1],
                        cursor="hand2")

        def on_enter(e): btn.config(bg=abg)
        def on_leave(e): btn.config(bg=bg)
        btn.bind("<Enter>", on_enter)
        btn.bind("<Leave>", on_leave)
        return btn

    def _make_check(self, parent, text, var):
        return tk.Checkbutton(parent, text=text, variable=var,
                              font=FONT_UI, bg=BG_CARD, fg=FG_MID,
                              activebackground=BG_CARD, activeforeground=FG,
                              selectcolor=BG_INPUT,
                              relief="flat", bd=0, cursor="hand2")

    # ─── Действия ─────────────────────────────────────────────────────────────

    def _style_combobox(self):
        style = ttk.Style(self)
        style.theme_use("clam")
        style.configure("TCombobox",
                        fieldbackground=BG_INPUT,
                        background=BG_CARD,
                        foreground=FG,
                        selectbackground=ACCENT,
                        selectforeground="#ffffff",
                        bordercolor=BORDER,
                        arrowcolor=FG_DIM)
        self.option_add("*TCombobox*Listbox.background", BG_CARD)
        self.option_add("*TCombobox*Listbox.foreground", FG)
        self.option_add("*TCombobox*Listbox.selectBackground", ACCENT)
        self.option_add("*TCombobox*Listbox.selectForeground", "#ffffff")

    def _make_radio(self, parent, text, value, var):
        return tk.Radiobutton(parent, text=text, variable=var, value=value,
                              font=FONT_UI, bg=BG_CARD, fg=FG_MID,
                              activebackground=BG_CARD, activeforeground=FG,
                              selectcolor=BG_INPUT, relief="flat", cursor="hand2")

    def _ensure_ollama_running(self) -> bool:
        """Проверяет Ollama, при необходимости запускает. Возвращает True если готов."""
        if self._is_ollama_running():
            return True
        self._append("\n⚡ Ollama не запущен — запускаем автоматически...\n", "warn")
        self._start_ollama()
        # _start_ollama запустит polling асинхронно, поэтому возвращаем False
        # пользователь увидит статус и нажмёт повторно когда Ollama будет готов
        self._set_status("Дождитесь запуска Ollama (● зелёный) и запустите снова", "warn")
        return False

    def _refresh_models(self):
        """Подгружает список установленных моделей через ollama list."""
        import subprocess, sys
        try:
            result = subprocess.run(
                ["ollama", "list"],
                capture_output=True, text=True, timeout=5
            )
            lines = result.stdout.strip().splitlines()
            # Первая строка — заголовок (NAME ID SIZE ...), пропускаем
            models = []
            for line in lines[1:]:
                parts = line.split()
                if parts:
                    name = parts[0]
                    # Пропускаем embedding-модели — они не для генерации
                    if "embed" not in name.lower():
                        models.append(name)
            if models:
                self.model_combo["values"] = models
                # Если текущая модель не в списке — ставим первую
                if self.model_var.get() not in models:
                    self.model_var.set(models[0])
                self._set_status(f"Найдено моделей: {len(models)}", "ok")
            else:
                self._set_status("Ollama: модели не найдены", "warn")
        except FileNotFoundError:
            self._set_status("Ollama не установлен или не в PATH", "err")
        except subprocess.TimeoutExpired:
            self._set_status("Ollama не отвечает (timeout)", "err")
        except Exception as e:
            self._set_status(f"Ошибка: {e}", "err")

    def _is_ollama_running(self) -> bool:
        """Проверяет доступность Ollama через HTTP."""
        import urllib.request, urllib.error
        try:
            urllib.request.urlopen("http://localhost:11434", timeout=2)
            return True
        except Exception:
            return False

    def _check_ollama_status(self, silent: bool = True):
        """Обновляет индикатор Ollama в статус-баре."""
        running = self._is_ollama_running()
        if running:
            self.ollama_dot.config(text="● Ollama", fg=GREEN)
            if not silent:
                self._set_status("Ollama запущен", "ok")
        else:
            self.ollama_dot.config(text="● Ollama", fg=FG_DIM)
            if not silent:
                self._set_status("Ollama не запущен — нажмите ● Ollama", "warn")
        return running

    def _toggle_ollama(self):
        """Запускает или останавливает ollama serve."""
        if self._is_ollama_running():
            self._stop_ollama()
        else:
            self._start_ollama()

    def _start_ollama(self):
        """Запускает ollama serve в фоне."""
        import subprocess
        if self._is_ollama_running():
            self._set_status("Ollama уже запущен", "ok")
            self._check_ollama_status()
            return

        try:
            self._append("\n🚀 Запускаем ollama serve...\n", "accent")
            self._ollama_proc = subprocess.Popen(
                ["ollama", "serve"],
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
            )
            # Ждём готовности (до 10 сек)
            self.ollama_dot.config(text="● Ollama", fg=YELLOW)
            self._wait_ollama_ready()
        except FileNotFoundError:
            self._set_status("ollama не найден — установите Ollama", "err")
            self._append("  [!] ollama не найден в PATH\n", "err")

    def _wait_ollama_ready(self, attempt: int = 0):
        """Polling каждые 500мс пока Ollama не ответит (макс 20 попыток = 10 сек)."""
        if self._is_ollama_running():
            self.ollama_dot.config(text="● Ollama", fg=GREEN)
            self._set_status("Ollama запущен", "ok")
            self._append("  ✓ Ollama готов\n", "ok")
            self._refresh_models()
        elif attempt < 20:
            self.after(500, lambda: self._wait_ollama_ready(attempt + 1))
        else:
            self.ollama_dot.config(text="● Ollama", fg=RED)
            self._set_status("Ollama не отвечает", "err")
            self._append("  [!] Ollama не ответил за 10 секунд\n", "err")

    def _stop_ollama(self):
        """Останавливает ollama serve если мы его запустили."""
        if self._ollama_proc and self._ollama_proc.poll() is None:
            self._ollama_proc.terminate()
            self._ollama_proc = None
            self.ollama_dot.config(text="● Ollama", fg=FG_DIM)
            self._set_status("Ollama остановлен", "warn")
            self._append("\n⏹ Ollama остановлен\n", "warn")
        else:
            self._set_status("Ollama запущен не нами — остановите вручную", "warn")

    def _browse_root(self):
        path = filedialog.askdirectory(title="Выберите корень проекта")
        if path:
            self.root_var.set(path)

    def _clear_output(self):
        self.output.config(state="normal")
        self.output.delete("1.0", "end")
        self.output.config(state="disabled")
        self.line_count.config(text="")

    def _build_args(self):
        args = []
        root = self.root_var.get().strip()
        if not root:
            self._set_status("Укажите корень проекта", "err")
            return None
        args += ["--root", root]

        resw = self.resw_var.get().strip()
        if resw:
            args += ["--resw", resw]

        if self.dry_var.get():
            args.append("--dry")

        return args

    def _build_generator_args(self):
        args = self._build_args()
        if args is None:
            return None
        if self.translate_var.get():
            args += ["--translate", "en-US"]
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
        if args and "--engine" in args:
            idx = args.index("--engine")
            if idx + 1 < len(args) and args[idx + 1] == "ollama":
                if not self._ensure_ollama_running():
                    return
        self._run_script("xaml_uid_generator.py", args)

    def _run_cleanup(self):
        extra_args = self._build_args()
        if extra_args is None:
            return
        if self.orphans_var.get():
            extra_args.append("--keep-orphans")
        self._run_script("xaml_resw_cleanup.py", extra_args)

    def _run_script(self, script_name: str, args):
        if args is None:
            return
        if self._running:
            self._set_status("Уже выполняется...", "warn")
            return

        script_path = SCRIPTS_DIR / script_name
        if not script_path.exists():
            self._append(f"[ошибка] Файл не найден: {script_path}\n", "err")
            return

        cmd = [sys.executable, str(script_path)] + args

        self._set_status(f"Запуск {script_name}...", "accent")
        self._set_running(True)
        self._append(f"\n{'─'*60}\n", "dim")
        self._append(f"▶  {' '.join(cmd)}\n", "accent")
        self._append(f"{'─'*60}\n", "dim")

        threading.Thread(target=self._stream_output, args=(cmd,), daemon=True).start()

    def _stream_output(self, cmd):
        try:
            proc = subprocess.Popen(
                cmd,
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                text=True,
                encoding="utf-8",
                errors="replace",
                env={**os.environ, "PYTHONIOENCODING": "utf-8"},
            )
            for line in proc.stdout:
                self._append_colored(line)
            proc.wait()
            rc = proc.returncode
            if rc == 0:
                self._set_status("Завершено успешно", "ok")
                self._append("\n✅ Завершено успешно\n", "ok")
            else:
                self._set_status(f"Завершено с ошибкой (код {rc})", "err")
                self._append(f"\n❌ Завершено с ошибкой (код {rc})\n", "err")
        except Exception as e:
            self._append(f"\n[исключение] {e}\n", "err")
            self._set_status("Ошибка запуска", "err")
        finally:
            self._set_running(False)

    # ─── Подсветка вывода ─────────────────────────────────────────────────────

    def _append_colored(self, line: str):
        tag = "dim"
        l = line.lower()

        if any(x in l for x in ["✅", "готово", "без изменений", "✓"]):
            tag = "ok"
        elif any(x in l for x in ["[!]", "parseerror", "ошибка", "error", "❌"]):
            tag = "err"
        elif any(x in l for x in ["⚠", "warn", "[dry]", "dry-run", "конфликт"]):
            tag = "warn"
        elif any(x in l for x in ["📄", "📁", "▶", "✏", "корень", ".resw"]):
            tag = "accent"
        elif any(x in l for x in ["✗", "будет удалено", "нумерованный"]):
            tag = "err"
        elif any(x in l for x in ["─", "файлов:", "строк"]):
            tag = "header"
        else:
            tag = None  # обычный текст

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
            # Обновляем счётчик строк
            lines = int(self.output.index("end-1c").split(".")[0])
            self.line_count.config(text=f"{lines} строк")
        self.after(0, _do)

    # ─── Статус ───────────────────────────────────────────────────────────────

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
