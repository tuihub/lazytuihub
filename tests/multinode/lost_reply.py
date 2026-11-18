import json
from http.client import HTTPConnection
from http.server import BaseHTTPRequestHandler, HTTPServer


class LostReply(BaseHTTPRequestHandler):
    writes = 0

    def do_GET(self):
        if self.path == "/count":
            self.respond(200, json.dumps(self.writes).encode())
        else:
            self.forward()

    def do_POST(self):
        self.forward()

    def forward(self):
        peer = HTTPConnection("a", 8080)
        try:
            body = self.rfile.read(int(self.headers.get("Content-Length", 0)))
            headers = {"Authorization": self.headers["Authorization"], "Content-Type": "application/json"}
            peer.request(self.command, self.path, body, headers)
            response = peer.getresponse()
            data = response.read()
            if self.command == "POST" and response.status == 200:
                type(self).writes += 1
                self.respond(504, b'{"message":"Worker committed, acknowledgement lost"}')
            else:
                self.respond(response.status, data)
        finally:
            peer.close()

    def respond(self, status, data):
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)


HTTPServer(("0.0.0.0", 8080), LostReply).serve_forever()
