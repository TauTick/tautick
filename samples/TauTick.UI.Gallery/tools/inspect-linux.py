#!/usr/bin/env python3
"""Small PID-scoped AT-SPI readiness/target dump and optional native capture."""
import argparse
import json
import pathlib
import time

import gi

gi.require_version("Atspi", "2.0")
from gi.repository import Atspi, GLib

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--pid", required=True, type=int)
parser.add_argument("--id", action="append", default=[])
parser.add_argument("--capture", type=pathlib.Path)
args = parser.parse_args()


def walk(node):
    node.clear_cache()
    yield node
    for index in range(node.get_child_count()):
        child = node.get_child_at_index(index)
        if child:
            yield from walk(child)


def targets():
    context = GLib.MainContext.default()
    while context.pending():
        context.iteration(False)
    desktop = Atspi.get_desktop(0)
    found = []
    for index in range(desktop.get_child_count()):
        app = desktop.get_child_at_index(index)
        if app and app.get_process_id() == args.pid:
            for node in walk(app):
                aid = node.get_accessible_id()
                if (args.id and aid in args.id) or (not args.id and aid):
                    rect = node.get_component_iface().get_extents(Atspi.CoordType.SCREEN)
                    found.append({"id": aid, "role": node.get_role_name(), "name": node.get_name(),
                                  "bounds": [rect.x, rect.y, rect.width, rect.height]})
    return found


deadline = time.monotonic() + 15
while True:
    try:
        found = targets()
        if found and all(any(n["id"] == aid and n["bounds"][2] > 0 for n in found) for aid in args.id):
            break
    except GLib.Error:
        pass  # Peers can disappear while a view reloads.
    if time.monotonic() >= deadline:
        raise SystemExit("Timed out: no usable targets for this PID/display/bus. Check gallery logs and session environment.")
    time.sleep(0.1)
print(json.dumps({"pid": args.pid, "targets": found}, indent=2))
if args.capture:
    destination = args.capture.resolve()
    repository = pathlib.Path(__file__).resolve().parents[3]
    if destination.is_relative_to(repository):
        raise SystemExit("Keep generated captures outside the repository.")
    from PIL import ImageGrab
    import os
    ImageGrab.grab(xdisplay=os.environ["DISPLAY"]).save(destination)
    print(f"Native capture: {destination}")
