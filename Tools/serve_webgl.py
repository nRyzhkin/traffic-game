#!/usr/bin/env python3
"""Serves a Unity WebGL build locally, adding the headers Brotli/gzip-compressed builds need.

Usage: python3 Tools/serve_webgl.py [build folder] [port]   (defaults: Builds/WebGL 8080)
"""
import http.server
import os
import sys

TYPES = {
    ".wasm": "application/wasm",
    ".js": "application/javascript",
    ".data": "application/octet-stream",
    ".symbols.json": "application/octet-stream",
}


class Handler(http.server.SimpleHTTPRequestHandler):
    def end_headers(self):
        path = self.path.split("?", 1)[0]
        for suffix, encoding in ((".br", "br"), (".gz", "gzip")):
            if path.endswith(suffix):
                self.send_header("Content-Encoding", encoding)
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def guess_type(self, path):
        base = path
        for suffix in (".br", ".gz"):
            if base.endswith(suffix):
                base = base[: -len(suffix)]
        for suffix, content_type in TYPES.items():
            if base.endswith(suffix):
                return content_type
        return super().guess_type(path)


if __name__ == "__main__":
    folder = sys.argv[1] if len(sys.argv) > 1 else "Builds/WebGL"
    port = int(sys.argv[2]) if len(sys.argv) > 2 else 8080
    os.chdir(folder)
    print(f"Serving {folder} at http://localhost:{port}")
    http.server.ThreadingHTTPServer(("localhost", port), Handler).serve_forever()
