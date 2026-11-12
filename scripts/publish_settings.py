"""Prints production settings as shell exports, so they never appear in a file or a log."""
import json
import os
import shlex
import subprocess


def variables(service):
    return json.loads(subprocess.check_output(["railway", "variables", "--service", service, "--json"]))


postgres = variables("Postgres")
api = variables("api")
port = os.environ["RAILWAY_TUNNEL_PORT"]

settings = {
    "PUBLISH_DB": f"Host=127.0.0.1;Port={port};Database={postgres['PGDATABASE']};Username={postgres['PGUSER']};Password={postgres['PGPASSWORD']};SSL Mode=Disable",
    "MEDIA_ENDPOINT": api["Media__Endpoint"],
    "MEDIA_BUCKET": api["Media__Bucket"],
    "MEDIA_ACCESS_KEY": api["Media__AccessKey"],
    "MEDIA_SECRET_KEY": api["Media__SecretKey"],
    "MEDIA_PATH_STYLE": "false",
}

for name, value in settings.items():
    print(f"export {name}={shlex.quote(value)}")
