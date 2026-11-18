import json
import os
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

hits = []
policy_file = "/tmp/source-policy.json"

class Source(BaseHTTPRequestHandler):
    def do_GET(self):
        if self.path == "/ready":
            body = b"ready"
        elif self.path == "/hits":
            body = json.dumps(hits).encode()
        elif self.path.partition("?")[0] == "/feed.xml":
            hits.append(self.client_address[0])
            if os.path.exists(policy_file):
                with open(policy_file) as policy:
                    status = json.load(policy).get(self.client_address[0])
                if status:
                    self.send_error(status)
                    return
            label = os.environ["LABEL"]
            body = f"<rss version='2.0'><channel><title>{label}</title><item><guid>1</guid><title>{label} article</title></item></channel></rss>".encode()
        else:
            self.send_error(404)
            return
        self.send_response(200)
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

ThreadingHTTPServer(("0.0.0.0", 8080), Source).serve_forever()
