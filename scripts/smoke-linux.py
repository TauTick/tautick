#!/usr/bin/env python3
"""Launch the built app on a dedicated X11 display, then request normal window close.

Run with xvfb-run -a python3 scripts/smoke-linux.py (after a Debug build).
Uses Python's standard library and libX11; no browser or Avalonia.Headless backend.
"""

import ctypes as c
import ctypes.util
from pathlib import Path
import subprocess
import tempfile
import time


class ClientData(c.Union):
    _fields_ = [("b", c.c_char * 20), ("s", c.c_short * 10), ("l", c.c_long * 5)]


class ClientMessage(c.Structure):
    _fields_ = [
        ("type", c.c_int), ("serial", c.c_ulong), ("send_event", c.c_int),
        ("display", c.c_void_p), ("window", c.c_ulong), ("message_type", c.c_ulong),
        ("format", c.c_int), ("data", ClientData),
    ]


class Event(c.Union):
    _fields_ = [("client", ClientMessage), ("pad", c.c_long * 24)]


class WindowAttributes(c.Structure):
    _fields_ = [
        ("x", c.c_int), ("y", c.c_int), ("width", c.c_int), ("height", c.c_int),
        ("border_width", c.c_int), ("depth", c.c_int), ("visual", c.c_void_p),
        ("root", c.c_ulong), ("window_class", c.c_int), ("bit_gravity", c.c_int),
        ("win_gravity", c.c_int), ("backing_store", c.c_int), ("backing_planes", c.c_ulong),
        ("backing_pixel", c.c_ulong), ("save_under", c.c_int), ("colormap", c.c_ulong),
        ("map_installed", c.c_int), ("map_state", c.c_int), ("all_event_masks", c.c_long),
        ("your_event_mask", c.c_long), ("do_not_propagate_mask", c.c_long),
        ("override_redirect", c.c_int), ("screen", c.c_void_p),
    ]


def main():
    x = c.CDLL(ctypes.util.find_library("X11") or "libX11.so.6")
    signatures = {
        "XOpenDisplay": ([c.c_char_p], c.c_void_p),
        "XDefaultRootWindow": ([c.c_void_p], c.c_ulong),
        "XQueryTree": ([c.c_void_p, c.c_ulong, c.POINTER(c.c_ulong), c.POINTER(c.c_ulong),
                        c.POINTER(c.POINTER(c.c_ulong)), c.POINTER(c.c_uint)], c.c_int),
        "XFetchName": ([c.c_void_p, c.c_ulong, c.POINTER(c.c_void_p)], c.c_int),
        "XGetWindowAttributes": ([c.c_void_p, c.c_ulong, c.POINTER(WindowAttributes)], c.c_int),
        "XInternAtom": ([c.c_void_p, c.c_char_p, c.c_int], c.c_ulong),
        "XSendEvent": ([c.c_void_p, c.c_ulong, c.c_int, c.c_long, c.POINTER(Event)], c.c_int),
        "XFree": ([c.c_void_p], c.c_int),
        "XFlush": ([c.c_void_p], c.c_int),
        "XCloseDisplay": ([c.c_void_p], c.c_int),
    }
    for name, (args, result) in signatures.items():
        getattr(x, name).argtypes = args
        getattr(x, name).restype = result

    display = x.XOpenDisplay(None)
    if not display:
        raise RuntimeError("No X11 display. Run under xvfb-run -a.")
    root = x.XDefaultRootWindow(display)
    repo = Path(__file__).resolve().parents[1]
    app = repo / "src/TauTick.Desktop/bin/Debug/net10.0/TauTick.Desktop.dll"
    if not app.is_file():
        x.XCloseDisplay(display)
        raise RuntimeError("Build TauTick.slnx in Debug first.")

    def find_window():
        returned_root, parent, count = c.c_ulong(), c.c_ulong(), c.c_uint()
        children = c.POINTER(c.c_ulong)()
        x.XQueryTree(display, root, c.byref(returned_root), c.byref(parent), c.byref(children), c.byref(count))
        try:
            for index in range(count.value):
                window = children[index]
                name = c.c_void_p()
                if x.XFetchName(display, window, c.byref(name)) and name.value:
                    try:
                        title = c.string_at(name).decode("utf-8", errors="replace")
                    finally:
                        x.XFree(name)
                    attrs = WindowAttributes()
                    x.XGetWindowAttributes(display, window, c.byref(attrs))
                    if title == "TauTick — synthetic foundation demo" and attrs.map_state == 2:
                        return window, title, attrs.width, attrs.height
        finally:
            if children:
                x.XFree(children)
        return None

    with tempfile.TemporaryFile(mode="w+") as log:
        process = subprocess.Popen(["dotnet", str(app)], cwd=repo, stdout=log, stderr=log)
        try:
            deadline = time.monotonic() + 20
            found = None
            while time.monotonic() < deadline and process.poll() is None:
                found = find_window()
                if found:
                    break
                time.sleep(0.1)
            if not found:
                raise RuntimeError("The application did not map its native X11 window.")
            window, title, width, height = found
            print(f"Mapped native window: {title} ({width}x{height})", flush=True)
            time.sleep(0.5)  # Allow the native event/render loop to run before closing.
            event = Event()
            event.client.type = 33  # ClientMessage
            event.client.window = window
            event.client.message_type = x.XInternAtom(display, b"WM_PROTOCOLS", 0)
            event.client.format = 32
            event.client.data.l[0] = x.XInternAtom(display, b"WM_DELETE_WINDOW", 0)
            if not x.XSendEvent(display, window, 0, 0, c.byref(event)):
                raise RuntimeError("Could not send the normal window-close request.")
            x.XFlush(display)
            code = process.wait(timeout=10)
            if code != 0:
                raise RuntimeError(f"Application exited with code {code}.")
            print("WM_DELETE_WINDOW closed the app cleanly; exit code 0.")
        finally:
            if process.poll() is None:
                process.kill()
                process.wait()
            log.seek(0)
            output = log.read()
            if output:
                print(output)
            x.XCloseDisplay(display)


if __name__ == "__main__":
    main()
