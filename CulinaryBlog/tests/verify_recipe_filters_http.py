"""Run HTTP checks against the built API with an isolated PostgreSQL database.

Requires Python, dotnet, and the project's PostgreSQL/Redis Docker containers.
Build the solution first. No application database is migrated or modified.
"""
import json
import os
from pathlib import Path
import socket
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
import uuid


def main():
    project = Path(__file__).resolve().parents[1] / "CulinaryBlog.API"
    assembly = project / "bin/Debug/net10.0/CulinaryBlog.API.dll"
    if not assembly.exists():
        raise RuntimeError("Build CulinaryBlog.sln before running HTTP checks.")
    database = "recipe_filter_http_" + uuid.uuid4().hex
    with socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        port = listener.getsockname()[1]
    base = f"http://127.0.0.1:{port}/api/v1/recipes"
    environment = dict(os.environ)
    environment.update({
        "ASPNETCORE_ENVIRONMENT": "Production",
        "ASPNETCORE_URLS": f"http://127.0.0.1:{port}",
        "ConnectionStrings__DefaultConnection":
            f"Host=localhost;Port=5432;Database={database};Username=postgres;Password=postgres",
        "ConnectionStrings__Redis": "localhost:6379",
    })
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))

    def request(query):
        try:
            response = opener.open(base + "?" + query, timeout=5)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            payload = response.read()
            return response.status, json.loads(payload) if payload else None

    process = None
    created = False
    with tempfile.TemporaryFile() as log:
        try:
            subprocess.run(["docker", "exec", "culinaryblog-postgres", "createdb",
                            "-U", "postgres", database], check=True)
            created = True
            process = subprocess.Popen(["dotnet", str(assembly)], cwd=project,
                                       env=environment, stdout=log, stderr=subprocess.STDOUT)
            deadline = time.monotonic() + 60
            while time.monotonic() < deadline:
                if process.poll() is not None:
                    raise RuntimeError("API exited before becoming ready.")
                try:
                    if request("page=1")[0] == 200:
                        break
                except (OSError, urllib.error.URLError):
                    pass
                time.sleep(0.25)
            else:
                raise RuntimeError("API did not become ready within 60 seconds.")

            cases = {
                "page=0": 422, "pageSize=51": 422,
                "maxCookTime=-1": 422, "minServings=0": 422, "minServings=-1": 422,
                "difficulty=999": 422, "difficulty=0": 422, "difficulty=unknown": 422,
                "difficulty=Easy%2CMedium": 422,
                "categoryId=invalid": 400, "minServings=1.5": 400,
                "minServings=2147483648": 400, "maxCookTime=1.5": 400,
                "maxCookTime=2147483648": 400,
                "difficulty=eAsY": 200, "difficulty=1": 200,
                "difficulty=Expert": 200, "difficulty=4": 200,
                "maxCookTime=0&minServings=1": 200,
            }
            for query, expected in cases.items():
                actual, payload = request(query)
                assert actual == expected, (query, expected, actual)
                if expected == 422:
                    assert payload["status"] == 422 and payload.get("type") and payload.get("title")
                    field = query.split("=", 1)[0]
                    field = field[0].upper() + field[1:]
                    messages = payload["errors"][field]
                    assert isinstance(messages, list) and messages and all(isinstance(m, str) and m for m in messages)
            _, empty = request("categoryId=" + str(uuid.uuid4()))
            assert empty["items"] == [] and empty["totalCount"] == 0
            _, by_name = request("difficulty=eAsY")
            _, by_number = request("difficulty=1")
            assert by_name == by_number
            # These requests previously collided in the concatenated cache key.
            status, unfiltered = request("sort=foo_cid__max_min_-createdAt")
            assert status == 200 and unfiltered["totalCount"] > 0
            for _ in range(2):
                status, collision = request("keyword=_cid__max_min_foo&sort=-createdAt")
                assert status == 200 and collision["totalCount"] == 0 and collision["items"] == []
            _, default_sort = request("sort=-createdAt")
            assert default_sort == unfiltered
            _, all_recipes = request("pageSize=50")
            recipes = all_recipes["items"]
            assert recipes, "The isolated database should contain startup seed recipes."
            chosen = recipes[0]
            filters = (f"categoryId={chosen['categoryId']}&difficulty={chosen['difficulty']}"
                       f"&maxCookTime={chosen['cookingTimeMinutes']}&minServings={chosen['servings']}")
            expected_ids = {r["id"] for r in recipes if r["categoryId"] == chosen["categoryId"]
                            and r["difficulty"] == chosen["difficulty"]
                            and r["cookingTimeMinutes"] <= chosen["cookingTimeMinutes"]
                            and r["servings"] >= chosen["servings"]}
            for _ in range(2):  # repeat request exercises cached responses
                status, result = request(filters + "&pageSize=50")
                assert status == 200 and {r["id"] for r in result["items"]} == expected_ids
                assert result["totalCount"] == len(expected_ids)
            _, page = request(filters + "&pageSize=1")
            assert len(page["items"]) == 1 and page["totalPages"] == len(expected_ids)
            print(f"PASS: {len(cases)} HTTP status cases, empty category, enum compatibility, AND filters, cache collision regression and pagination.")
        except BaseException:
            log.seek(0)
            print(log.read().decode("utf-8", errors="replace")[-8000:])
            raise
        finally:
            if process is not None and process.poll() is None:
                process.terminate()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait()
            if created:
                # Only remove the random database created by this invocation.
                subprocess.run(["docker", "exec", "culinaryblog-postgres", "dropdb",
                                "-U", "postgres", "--force", database], check=True)


if __name__ == "__main__":
    main()
