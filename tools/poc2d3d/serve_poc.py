"""Local static server for the Phase 1 POC WebGL build (127.0.0.1 only).

  python serve_poc.py [--port 8890] [--root BUILD_DIR] [--capture-dir DIR]

With --capture-dir, POST /__capture?name=<file> saves a PNG body there. It is used only
to keep browser screenshots for the Phase 1 report; without the flag the server is
read-only like `python -m http.server`.
"""
import argparse
import functools
import http.server
import os
import re
import urllib.parse


class Handler(http.server.SimpleHTTPRequestHandler):
    capture_dir = None

    def end_headers(self):
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def do_POST(self):
        parsed = urllib.parse.urlparse(self.path)
        name = urllib.parse.parse_qs(parsed.query).get("name", [""])[0]
        if parsed.path != "/__capture" or not self.capture_dir or not re.fullmatch(r"[\w.-]{1,80}\.png", name):
            self.send_error(404)
            return
        length = int(self.headers.get("Content-Length", "0"))
        if length <= 0 or length > 30 * 1024 * 1024:
            self.send_error(413)
            return
        os.makedirs(self.capture_dir, exist_ok=True)
        with open(os.path.join(self.capture_dir, name), "wb") as f:
            f.write(self.rfile.read(length))
        self.send_response(204)
        self.end_headers()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=8890)
    parser.add_argument("--root", default=os.path.dirname(os.path.abspath(__file__)))
    parser.add_argument("--capture-dir")
    args = parser.parse_args()
    Handler.capture_dir = os.path.abspath(args.capture_dir) if args.capture_dir else None
    handler = functools.partial(Handler, directory=os.path.abspath(args.root))
    with http.server.ThreadingHTTPServer(("127.0.0.1", args.port), handler) as server:
        print(f"HALKA WORLD Phase 1 POC: http://127.0.0.1:{args.port}/")
        server.serve_forever()


if __name__ == "__main__":
    main()
