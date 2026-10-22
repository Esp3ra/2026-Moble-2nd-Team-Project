# 공통 퇴근 결과 소유권 검사

`dotnet run --project Tests/ClockOutRecovery.Tests -c Release`

실제 ApiClient와 명시 HTTP 대역으로12시나리오35검사를 수행한다.
일반 in-flight 응답의 drain·늦은401, 일반요청 차단, 첫 finalize 응답유실 뒤 원UUID/본문/
도전/토큰의 재전송, 진짜복구401, 같은토큰 새로그인세대, 틀린목적/영수증,
동시 finalize, 호출자취소, 미송신폐기/미확정폐기·교체접속보존을 구분한다.
센서·공유서버/실제DB를 사용하지 않는다. 전역401무시나 일반토큰 접근을 제공하지 않는다.

`RemoteWork.Server/tests/test_clock_out_recovery_client.py`는 명시 `CLOCK_OUT_CLIENT` DLL과
안전검사를통과한독립소유 `MYSQL_TEST_URL`에서만 실행한다. raw identity/등록fixture가
시험회원·키·매핑·열린근무와검증된clock_out 도전을 준비한다. `__test/clock-out`은이시험의
동적Flaskroute이고제품등록/API가아니다. 실제 loopbackHTTP/C#→Flask/AttendanceWorkflow/
MySQL의정상퇴근과성공응답유실후원결과복구2건을검사한다. 실제AuthSession0·근무ended·
receipt1개를대조한다.서명/센서결과는fixture이며실물Pi/공유환경검사가아니다.
자식프로세스는기존Windows owned.run_owned_client의Job/timeout/정리를사용한다.

검증시 사용한순서: 테스트DLL Release build→독립소유MySQL/새DB→명시PYTHON_DOTENV_DISABLED와
Server/tests import경로→위pytest모듈→시험HTTP·자식/DB/프로세스/runtime정리.
운영.env/비밀번호/실키를넣거나기존DB에fixture를실행하지않는다.
