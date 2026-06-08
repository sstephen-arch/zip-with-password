import http.server
import socketserver
import os

PORT = 7842
WORKSPACE = r"C:\Users\shem_\Claude\Projects\Zip+Password"

class CORSHandler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=WORKSPACE, **kwargs)

    def end_headers(self):
        self.send_header('Access-Control-Allow-Origin', '*')
        self.send_header('Access-Control-Allow-Methods', 'GET')
        self.send_header('Cache-Control', 'no-cache')
        super().end_headers()

    def log_message(self, format, *args):
        pass  # suppress logs

with socketserver.TCPServer(("", PORT), CORSHandler) as httpd:
    print(f"Serving on http://localhost:{PORT}")
    httpd.serve_forever()
