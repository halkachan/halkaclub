"""Local server for はるかわーるど ver3.0 RC1 (read-only static files).

  python serve_rc1.py [--port 8895] [--lan] [--capture-dir DIR]

Serves this folder: http://127.0.0.1:8895/halkaworld/ is the staging copy of the public game
page with the RC1 game in its frame (webgl/ below it, the same path as on halkaclub.com), and
http://127.0.0.1:8895/halkaworld/webgl/ is the game alone.

--lan listens on every network interface so a phone on the same Wi-Fi can open the pages
(only on a network you trust; the files are served read-only). Without it only this PC can.
--capture-dir lets POST /__capture?name=<file>.png store screenshots (test tooling only).
"""
import argparse
import functools
import http.server
import os
import re
import socket
import urllib.parse


class Handler(http.server.SimpleHTTPRequestHandler):
    capture_dir = None

    def end_headers(self):
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def do_GET(self):
        if urllib.parse.urlparse(self.path).path in ("", "/"):
            self.send_response(302)
            self.send_header("Location", "/halkaworld/")
            self.end_headers()
            return
        super().do_GET()

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


def lan_address():
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as s:
            s.connect(("192.0.2.1", 9))  # no packet is sent; picks the outgoing interface
            return s.getsockname()[0]
    except OSError:
        return None


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=8895)
    parser.add_argument("--lan", action="store_true")
    parser.add_argument("--root", default=os.path.dirname(os.path.abspath(__file__)))
    parser.add_argument("--capture-dir")
    args = parser.parse_args()
    Handler.capture_dir = os.path.abspath(args.capture_dir) if args.capture_dir else None
    handler = functools.partial(Handler, directory=os.path.abspath(args.root))
    host = "0.0.0.0" if args.lan else "127.0.0.1"
    with http.server.ThreadingHTTPServer((host, args.port), handler) as server:
        print("HARUKA WORLD ver3.0 RC1 (local)")
        print(f"  staging page : http://127.0.0.1:{args.port}/halkaworld/")
        print(f"  game only    : http://127.0.0.1:{args.port}/halkaworld/webgl/")
        print(f"  debug panel  : http://127.0.0.1:{args.port}/halkaworld/webgl/?debug=1")
        if args.lan:
            ip = lan_address()
            if ip:
                print(f"  phone (same Wi-Fi): http://{ip}:{args.port}/halkaworld/")
        print("Ctrl+C to stop.")
        server.serve_forever()


if __name__ == "__main__":
    main()
