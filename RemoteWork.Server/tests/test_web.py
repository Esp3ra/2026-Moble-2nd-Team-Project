import re


def csrf(client):
    response = client.get("/login")
    return re.search(r'name="csrf_token" value="([^"]+)"', response.text).group(1)


def test_web_login_csrf_cookie_and_logout(client):
    assert client.get("/").status_code == 302
    assert client.post("/login", data={"login_id": "staff1", "password": "Staff-test-2026!"}).status_code == 400
    token = csrf(client)
    result = client.post("/login", data={"csrf_token": token, "login_id": "staff1", "password": "Staff-test-2026!"})
    assert result.status_code == 302
    cookie = next(h for h in result.headers.getlist("Set-Cookie") if h.startswith("rw_session="))
    assert "HttpOnly" in cookie and "SameSite=Lax" in cookie
    home = client.get("/")
    assert "직원님" in home.text and "E001" in home.text
    assert client.get("/users").status_code == 403
    assert client.get("/features/tasks").status_code == 200
    assert client.get("/features/unknown").status_code == 404
    assert client.get("/api/v1/auth/me").status_code == 401
    assert client.post("/logout").status_code == 400
    assert client.post("/logout", data={"csrf_token": "한글입력"}).status_code == 400
    token = re.search(r'name="csrf_token" value="([^"]+)"', home.text).group(1)
    assert client.post("/logout", data={"csrf_token": token}).status_code == 302
    assert client.get("/").status_code == 302


def test_web_admin_user_list_escapes_names(client, login):
    headers, _ = login("admin1", "Admin-test-2026!")
    client.post("/api/v1/users", headers=headers, json={"login_id": "xsstest", "password": "Xss-password-2026!",
        "display_name": "<script>alert(1)</script>", "employee_no": "E999"})
    result = client.post("/login", data={"csrf_token": csrf(client), "login_id": "admin1", "password": "Admin-test-2026!"})
    assert result.status_code == 302
    users = client.get("/users")
    assert users.status_code == 200
    assert "<script>" not in users.text and "&lt;script&gt;" in users.text
    assert "frame-ancestors 'none'" in users.headers["Content-Security-Policy"]
