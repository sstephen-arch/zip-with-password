"""
Zip with Password — main application
Requires: pyzipper (pip install pyzipper)
Usage: python app.py [source_path]
"""

import os
import sys
import threading
import tkinter as tk
from tkinter import ttk, filedialog, messagebox
import pyzipper


# ─────────────────────────────────────────────────────────────────── ZIP LOGIC
def build_default_output(source: str) -> str:
    source = source.rstrip("\\/")
    directory = os.path.dirname(source) or os.path.expanduser("~")
    base = os.path.splitext(os.path.basename(source))[0] or os.path.basename(source)
    candidate = os.path.join(directory, base + ".zip")
    i = 1
    while os.path.exists(candidate):
        candidate = os.path.join(directory, f"{base} ({i}).zip")
        i += 1
    return candidate


def create_encrypted_zip(source: str, output: str, password: str,
                          on_progress, on_done, on_error):
    """Runs in a background thread. Uses AES-256 encryption."""
    try:
        os.makedirs(os.path.dirname(output) or ".", exist_ok=True)
        source = source.rstrip("\\/")

        with pyzipper.AESZipFile(output, "w",
                                  compression=pyzipper.ZIP_DEFLATED,
                                  encryption=pyzipper.WZ_AES) as zf:
            zf.setpassword(password.encode("utf-8"))

            if os.path.isdir(source):
                all_files = []
                for root, dirs, files in os.walk(source):
                    for f in files:
                        all_files.append(os.path.join(root, f))

                total = len(all_files) or 1
                for idx, fpath in enumerate(all_files):
                    arc_name = os.path.relpath(fpath, os.path.dirname(source))
                    zf.write(fpath, arc_name)
                    pct = int((idx + 1) / total * 100)
                    on_progress(pct, os.path.basename(fpath))
            else:
                on_progress(10, os.path.basename(source))
                zf.write(source, os.path.basename(source))
                on_progress(100, os.path.basename(source))

        on_done()
    except Exception as exc:
        on_error(str(exc))


# ─────────────────────────────────────────────────────────────────── GUI
class App(tk.Tk):
    ACCENT   = "#2563EB"
    HOVER    = "#1D4ED8"
    DANGER   = "#DC2626"
    SUCCESS  = "#16A34A"
    BG       = "#FFFFFF"
    SURFACE  = "#F8FAFC"
    BORDER   = "#CBD5E1"
    TEXT     = "#0F172A"
    MUTED    = "#64748B"
    FONT     = "Segoe UI"

    def __init__(self, source_path: str | None):
        super().__init__()
        self.title("Zip with Password")
        self.resizable(False, False)
        self.configure(bg=self.BG)
        self._build_ui()
        self._center()

        if source_path:
            self.src_var.set(source_path)
            self.out_var.set(build_default_output(source_path))

    # ── Layout ──────────────────────────────────────────────────────────────
    def _build_ui(self):
        pad = dict(padx=28)
        self.columnconfigure(0, weight=1)

        # Header
        hdr = tk.Frame(self, bg=self.BG)
        hdr.grid(row=0, column=0, sticky="ew", pady=(24, 0), **pad)
        tk.Label(hdr, text="🔒", font=(self.FONT, 20), bg=self.BG).pack(side="left")
        info = tk.Frame(hdr, bg=self.BG)
        info.pack(side="left", padx=(10, 0))
        tk.Label(info, text="Zip with Password",
                 font=(self.FONT, 14, "bold"), bg=self.BG, fg=self.TEXT
                 ).pack(anchor="w")
        tk.Label(info, text="Create an AES-256 encrypted ZIP archive",
                 font=(self.FONT, 9), bg=self.BG, fg=self.MUTED
                 ).pack(anchor="w")

        # Divider
        tk.Frame(self, height=1, bg=self.BORDER
                 ).grid(row=1, column=0, sticky="ew", pady=16, **pad)

        # Source
        self._label(2, "SOURCE")
        self.src_var = tk.StringVar()
        self._entry(3, self.src_var, readonly=True)

        # Output
        self._label(4, "SAVE ZIP AS", pady=(10, 0))
        self.out_var = tk.StringVar()
        row = tk.Frame(self, bg=self.BG)
        row.grid(row=5, column=0, sticky="ew", **pad)
        self._entry_in_frame(row, self.out_var)
        self._button(row, "Browse…", self._browse_output,
                     style="secondary", side="right", padx=(6, 0))

        # Password
        self._label(6, "PASSWORD", pady=(10, 0))
        self.pw_var  = tk.StringVar()
        self._entry(7, self.pw_var, show="●", on_change=self._clear_error)

        # Confirm
        self._label(8, "CONFIRM PASSWORD", pady=(10, 0))
        self.pw2_var = tk.StringVar()
        self._entry(9, self.pw2_var, show="●", on_change=self._clear_error)

        # Validation message
        self.err_var = tk.StringVar()
        self.err_lbl = tk.Label(self, textvariable=self.err_var,
                                font=(self.FONT, 9), bg=self.BG, fg=self.DANGER,
                                wraplength=380, justify="left")
        self.err_lbl.grid(row=10, column=0, sticky="w", **pad, pady=(6, 0))

        # Progress area
        self.prog_frame = tk.Frame(self, bg=self.BG)
        self.prog_frame.grid(row=11, column=0, sticky="ew", **pad, pady=(12, 0))
        self.status_var = tk.StringVar()
        tk.Label(self.prog_frame, textvariable=self.status_var,
                 font=(self.FONT, 9), bg=self.BG, fg=self.MUTED
                 ).pack(anchor="w")
        self.progress = ttk.Progressbar(self.prog_frame, length=420,
                                         mode="determinate", maximum=100)
        self.progress.pack(fill="x", pady=(4, 0))
        self.prog_frame.grid_remove()

        # Success banner
        self.ok_frame = tk.Frame(self, bg="#F0FDF4",
                                  highlightbackground="#86EFAC", highlightthickness=1)
        self.ok_frame.grid(row=12, column=0, sticky="ew", **pad, pady=(12, 0))
        tk.Label(self.ok_frame, text="✓  ZIP created successfully!",
                 font=(self.FONT, 10, "bold"), bg="#F0FDF4", fg=self.SUCCESS,
                 pady=8).pack()
        self.ok_frame.grid_remove()

        # Buttons
        btn_row = tk.Frame(self, bg=self.BG)
        btn_row.grid(row=13, column=0, sticky="ew", **pad, pady=(18, 24))
        self._button(btn_row, "Cancel", self.destroy, style="secondary", side="left")
        self.create_btn = self._button(
            btn_row, "  Create ZIP  ", self._create, style="primary", side="right")

        self.update_idletasks()

    # ── Widget helpers ───────────────────────────────────────────────────────
    def _label(self, row, text, pady=(0, 0)):
        tk.Label(self, text=text,
                 font=(self.FONT, 8, "bold"), bg=self.BG, fg=self.MUTED
                 ).grid(row=row, column=0, sticky="w", padx=28, pady=pady)

    def _entry(self, row, var, readonly=False, show=None, on_change=None):
        frame = tk.Frame(self, bg=self.BG)
        frame.grid(row=row, column=0, sticky="ew", padx=28, pady=(2, 0))
        self._entry_in_frame(frame, var, readonly=readonly, show=show, on_change=on_change)

    def _entry_in_frame(self, frame, var, readonly=False, show=None, on_change=None):
        state = "readonly" if readonly else "normal"
        kwargs = {}
        if show:
            kwargs["show"] = show
        e = tk.Entry(frame, textvariable=var, state=state,
                     font=(self.FONT, 10), bg=self.SURFACE if readonly else "white",
                     fg=self.MUTED if readonly else self.TEXT,
                     relief="solid", bd=1, highlightthickness=1,
                     highlightbackground=self.BORDER,
                     highlightcolor=self.ACCENT,
                     insertbackground=self.TEXT,
                     **kwargs)
        e.pack(side="left", fill="x", expand=True, ipady=6, ipadx=6)
        if on_change:
            var.trace_add("write", lambda *_: on_change())
        return e

    def _button(self, parent, text, cmd, style="primary", side="left", padx=0):
        if style == "primary":
            bg, fg, hover = self.ACCENT, "white", self.HOVER
        else:
            bg, fg, hover = "white", self.TEXT, self.SURFACE

        btn = tk.Button(parent, text=text, command=cmd,
                        font=(self.FONT, 10, "bold"), bg=bg, fg=fg,
                        relief="solid", bd=1,
                        highlightbackground=self.BORDER if style == "secondary" else bg,
                        activebackground=hover, activeforeground=fg,
                        padx=14, pady=6, cursor="hand2")
        btn.pack(side=side, padx=padx)
        return btn

    def _center(self):
        self.update_idletasks()
        w, h = self.winfo_width(), self.winfo_height()
        sw, sh = self.winfo_screenwidth(), self.winfo_screenheight()
        self.geometry(f"+{(sw - w) // 2}+{(sh - h) // 2}")

    # ── Actions ──────────────────────────────────────────────────────────────
    def _browse_output(self):
        current = self.out_var.get()
        init_dir = os.path.dirname(current) if current else os.path.expanduser("~")
        path = filedialog.asksaveasfilename(
            title="Save ZIP As",
            defaultextension=".zip",
            filetypes=[("ZIP Archive", "*.zip")],
            initialdir=init_dir,
            initialfile=os.path.basename(current))
        if path:
            self.out_var.set(path)

    def _clear_error(self):
        self.err_var.set("")

    def _validate(self):
        if not self.src_var.get().strip():
            return "Please specify a source file or folder."
        if not self.out_var.get().strip():
            return "Please specify a destination path."
        if not self.out_var.get().lower().endswith(".zip"):
            return "Destination must end with .zip"
        if not self.pw_var.get():
            return "Please enter a password."
        if len(self.pw_var.get()) < 6:
            return "Password must be at least 6 characters."
        if self.pw_var.get() != self.pw2_var.get():
            return "Passwords do not match."
        return None

    def _create(self):
        error = self._validate()
        if error:
            self.err_var.set(error)
            return

        self._clear_error()
        self.ok_frame.grid_remove()
        self.prog_frame.grid()
        self.progress["value"] = 0
        self.status_var.set("Starting…")
        self.create_btn.config(state="disabled")

        source   = self.src_var.get().strip()
        output   = self.out_var.get().strip()
        password = self.pw_var.get()

        def on_progress(pct, filename):
            self.after(0, lambda: (
                self.progress.__setitem__("value", pct),
                self.status_var.set(f"Compressing {filename}…")
            ))

        def on_done():
            self.after(0, self._on_success)

        def on_error(msg):
            self.after(0, lambda: self._on_error(msg))

        threading.Thread(
            target=create_encrypted_zip,
            args=(source, output, password, on_progress, on_done, on_error),
            daemon=True
        ).start()

    def _on_success(self):
        self.prog_frame.grid_remove()
        self.ok_frame.grid()
        self.create_btn.config(state="normal")

    def _on_error(self, msg):
        self.prog_frame.grid_remove()
        self.err_var.set(f"Error: {msg}")
        self.create_btn.config(state="normal")


# ─────────────────────────────────────────────────────────────────── Entry
if __name__ == "__main__":
    source = sys.argv[1] if len(sys.argv) > 1 else None
    app = App(source)
    app.mainloop()
