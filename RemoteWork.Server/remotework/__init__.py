import os
import secrets
from datetime import timedelta
from pathlib import Path
import click
from dotenv import load_dotenv
from flask import Flask, g, request, jsonify, render_template, redirect, url_for
from sqlalchemy import URL, delete
from sqlalchemy.exc import SQLAlchemyError, IntegrityError
from werkzeug.exceptions import HTTPException
from werkzeug.security import generate_password_hash
from . import db
from .models import AuthSession, LoginFailure, utcnow
from .common import AppError


def create_app(test_config=None):
    root = Path(__file__).resolve().parent.parent
    load_dotenv(root / ".env")
    app = Flask(__name__, instance_path=str(root / "instance"))
    database_url = os.getenv("DATABASE_URL") or URL.create("mysql+pymysql",
        username=os.getenv("MYSQL_USER", "remote_work"), password=os.getenv("MYSQL_PASSWORD", ""),
        host=os.getenv("MYSQL_HOST", "127.0.0.1"), port=int(os.getenv("MYSQL_PORT", "3306")),
        database=os.getenv("MYSQL_DATABASE", "remote_work"), query={"charset": "utf8mb4"})
    app.config.from_mapping(DATABASE_URL=database_url, SECRET_KEY=os.getenv("SECRET_KEY", ""),
        ENABLE_TASK_QUERY_DRAFT=os.getenv("REMOTEWORK_ENABLE_TASK_QUERY_DRAFT") == "1",
        ENABLE_COMMON_WRITE_DRAFT=os.getenv("REMOTEWORK_ENABLE_COMMON_WRITE_DRAFT") == "1",
        ENABLE_DEADLINE_ALERTS_DRAFT=os.getenv("REMOTEWORK_ENABLE_DEADLINE_ALERTS_DRAFT") == "1",
        ENABLE_DEADLINE_RESULT_READS_DRAFT=os.getenv("REMOTEWORK_ENABLE_DEADLINE_RESULT_READS_DRAFT") == "1",
        ENABLE_COLLABORATION_DRAFT=os.getenv("REMOTEWORK_ENABLE_COLLABORATION_DRAFT") == "1",
        ENABLE_COLLABORATION_RESULT_READS_DRAFT=os.getenv("REMOTEWORK_ENABLE_COLLABORATION_RESULT_READS_DRAFT") == "1",
        ENABLE_DEVICE_ENROLLMENT_DRAFT=os.getenv("REMOTEWORK_ENABLE_DEVICE_ENROLLMENT_DRAFT") == "1",
        ENABLE_ENROLLMENT_DURABILITY_DRAFT=os.getenv("REMOTEWORK_ENABLE_ENROLLMENT_DURABILITY_DRAFT") == "1",
        ENABLE_ENROLLMENT_RECOVERY_DRAFT=os.getenv("REMOTEWORK_ENABLE_ENROLLMENT_RECOVERY_DRAFT") == "1",
        ENABLE_DEVICE_IDENTITY_DRAFT=os.getenv("REMOTEWORK_ENABLE_DEVICE_IDENTITY_DRAFT") == "1",
        WRITE_REQUEST_TTL_SECONDS=int(os.getenv("WRITE_REQUEST_TTL_SECONDS", "86400")),
        COMPANY_NAME=os.getenv("COMPANY_NAME", "회사명"), SESSION_HOURS=int(os.getenv("SESSION_HOURS", "8")),
        COOKIE_SECURE=os.getenv("COOKIE_SECURE", "false").lower() == "true", MAX_CONTENT_LENGTH=32 * 1024,
        SESSION_COOKIE_HTTPONLY=True, SESSION_COOKIE_SAMESITE="Lax", SESSION_REFRESH_EACH_REQUEST=False)
    if test_config:
        app.config.update(test_config)
    # Reject incomplete private/service configuration before database initialization.
    if app.config["ENABLE_DEVICE_IDENTITY_DRAFT"]:
        from .features.identity import validate_configuration
        identity_profiles = validate_configuration(app.config)
    if ((app.config["ENABLE_COLLABORATION_DRAFT"] or app.config["ENABLE_COLLABORATION_RESULT_READS_DRAFT"])
            and not app.config["ENABLE_COMMON_WRITE_DRAFT"]):
        raise RuntimeError("Collaboration requires explicit common write support")
    if app.config["ENABLE_ENROLLMENT_DURABILITY_DRAFT"] and not app.config["ENABLE_DEVICE_ENROLLMENT_DRAFT"]:
        raise RuntimeError("Enrollment MySQL durability requires explicit enrollment activation")
    if app.config["ENABLE_ENROLLMENT_RECOVERY_DRAFT"] and not app.config["ENABLE_ENROLLMENT_DURABILITY_DRAFT"]:
        raise RuntimeError("Enrollment recovery requires explicit MySQL durability activation")
    secret = app.config["SECRET_KEY"]
    if len(secret) < 32 or "CHANGE_ME" in secret:
        raise RuntimeError("SECRET_KEY를 무작위 32자 이상으로 설정해 주세요. .env.example 또는 --demo 실행 안내를 확인하세요.")
    if not 1 <= app.config["SESSION_HOURS"] <= 168:
        raise RuntimeError("SESSION_HOURS must be between 1 and 168")
    app.config["SESSION_COOKIE_SECURE"] = app.config["COOKIE_SECURE"]
    app.config["PERMANENT_SESSION_LIFETIME"] = timedelta(hours=app.config["SESSION_HOURS"])
    app.config["DUMMY_HASH"] = generate_password_hash(secrets.token_urlsafe(32), method="scrypt")
    db.init_app(app)
    if app.config["ENABLE_DEVICE_IDENTITY_DRAFT"]:
        from .features.identity import install as install_identity
        install_identity(app, identity_profiles)
    from .auth import load_identity
    from .api import bp as api_bp
    from .web import bp as web_bp
    from .web import protect_forms

    @app.before_request
    def before():
        g.request_id = secrets.token_hex(8)
        g.user, g.auth_session = None, None
        # Public login/health/static must work without a stale-cookie DB query first.
        if request.endpoint not in ("static", "api.health", "api.login", "web.login"):
            load_identity()
        if not request.path.startswith("/api/"):
            protect_forms()

    @app.after_request
    def headers(response):
        response.headers["X-Request-ID"] = g.request_id
        response.headers["X-Content-Type-Options"] = "nosniff"
        response.headers["X-Frame-Options"] = "DENY"
        response.headers["Referrer-Policy"] = "same-origin"
        response.headers["Content-Security-Policy"] = "default-src 'self'; style-src 'self'; frame-ancestors 'none'; form-action 'self'; base-uri 'self'"
        response.headers["Cache-Control"] = "no-store"
        return response

    def error_response(status, code, message):
        if request.path.startswith("/api/"):
            return jsonify(ok=False, error={"code": code, "message": message}, request_id=g.request_id), status
        if status == 401:
            return redirect(url_for("web.login"))
        return render_template("error.html", message=message, status=status), status

    @app.errorhandler(AppError)
    def app_error(exc):
        return error_response(exc.status, exc.code, exc.message)

    @app.errorhandler(IntegrityError)
    def conflict(exc):
        db.get_db().rollback()
        return error_response(409, "CONFLICT", "이미 사용 중인 아이디·사번이거나 연결된 데이터와 충돌합니다.")

    @app.errorhandler(SQLAlchemyError)
    def database_error(exc):
        db.get_db().rollback()
        app.logger.error("database_error request_id=%s type=%s", g.request_id, type(exc).__name__)
        return error_response(503, "DATABASE_UNAVAILABLE", "데이터베이스 연결 또는 초기화 상태를 확인해 주세요.")

    @app.errorhandler(HTTPException)
    def http_error(exc):
        return error_response(exc.code, f"HTTP_{exc.code}",
            {400: "요청 내용을 확인해 주세요.", 404: "요청한 주소를 찾을 수 없습니다.",
             405: "지원하지 않는 요청 방식입니다.", 413: "요청 내용이 너무 큽니다.",
             415: "JSON 형식으로 요청해 주세요."}.get(exc.code, "요청을 처리할 수 없습니다."))

    @app.errorhandler(Exception)
    def unexpected(exc):
        app.logger.error("server_error request_id=%s type=%s", g.request_id, type(exc).__name__)
        return error_response(500, "SERVER_ERROR", "요청을 처리하지 못했습니다. 문의 시 요청 번호를 알려 주세요.")

    app.register_blueprint(api_bp)
    app.register_blueprint(web_bp)

    # Explicit integration point: feature drafts must opt in before model import.
    from .features.registry import get_blueprints
    for feature in get_blueprints(app.config):
        app.register_blueprint(feature)

    if app.config["ENABLE_DEVICE_ENROLLMENT_DRAFT"]:
        from .features.enrollment.setup import install as install_enrollment
        from .features.enrollment.routes import bp as enrollment_bp
        install_enrollment(app)
        app.register_blueprint(enrollment_bp)

    # Independent common support; no task action or authorization is enabled here.
    if app.config["ENABLE_COMMON_WRITE_DRAFT"]:
        from .write_requests import bp as write_bp
        app.register_blueprint(write_bp)
        if app.config["ENABLE_TASK_QUERY_DRAFT"]:
            from .task_write_support import install
            install(app)

        @app.cli.command("cleanup-write-requests")
        def cleanup_write_requests():
            """Remove expired receipts; signed expired keys still cannot execute."""
            from .write_requests import WriteReceipt
            with db.transaction() as session:
                removed = session.execute(delete(WriteReceipt).where(WriteReceipt.expires_at <= utcnow()))
            click.echo(f"만료된 요청 결과 정리: {removed.rowcount}건")

    # The ordinary entry point uses the reviewed owner bridge and retained-result
    # readers. Startup never creates/repairs 012 or installs a deployment ACK.
    if app.config["ENABLE_COLLABORATION_DRAFT"] or app.config["ENABLE_COLLABORATION_RESULT_READS_DRAFT"]:
        from .features.collaboration import install as install_collaboration, install_result_readers
        from .features.collaboration.task_bridge import current_task_context
        if app.config["ENABLE_COLLABORATION_DRAFT"]:
            install_collaboration(app, task_context=current_task_context)
        else:
            install_result_readers(app, task_context=current_task_context, read_only=True)

    if app.config["ENABLE_DEADLINE_ALERTS_DRAFT"] or app.config["ENABLE_DEADLINE_RESULT_READS_DRAFT"]:
        from .deadline_http import install as install_deadline_http
        install_deadline_http(app, writes=app.config["ENABLE_DEADLINE_ALERTS_DRAFT"])

    @app.cli.command("init-db")
    def init_db_command():
        """Create common tables; never drops existing data."""
        db.init_schema()
        click.echo("공통 테이블 준비 완료 (기존 데이터 보존)")

    @app.cli.command("create-user")
    @click.option("--login-id", prompt=True)
    @click.option("--display-name", prompt=True)
    @click.option("--employee-no", prompt=True)
    @click.option("--department", default="")
    @click.option("--role", type=click.Choice(["admin", "employee"]), default="employee")
    @click.password_option(confirmation_prompt=True)
    def create_user_command(login_id, display_name, employee_no, department, role, password):
        from .auth import create_user
        from .common import audit
        with db.transaction() as session:
            user = create_user(session, locals())
            audit("user.created_cli", "user", user.id, {"role": role})
        click.echo(f"사용자 생성 완료: {user.login_id}")

    @app.cli.command("cleanup-auth")
    def cleanup_auth():
        with db.transaction() as session:
            session.execute(delete(AuthSession).where(AuthSession.expires_at <= utcnow()))
            session.execute(delete(LoginFailure).where(LoginFailure.created_at < utcnow() - timedelta(days=1)))
        click.echo("만료된 접속과 오래된 실패 기록 정리 완료")

    # Member1 leave proposal: opt-in API; no DDL/calendar/member grants at startup.
    if app.config.get('ENABLE_LEAVE_DRAFT', os.getenv('REMOTEWORK_ENABLE_LEAVE_DRAFT') == '1'):
        app.config.setdefault('LEAVE_WRITE_POLICY', os.getenv('REMOTEWORK_LEAVE_WRITE_POLICY', ''))
        from .leave_service import install as install_leave
        install_leave(app)

    return app
