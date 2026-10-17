# W1 실제 C# → HTTP → 전용 MySQL 검증

3번 연결 보완에서는 `CaptureSession()`의 실제 서버 사용자 정보·접속 전환/로그아웃 취소와 캡처한 접속으로 보내는 GET/키 발급/중복 저장·결과 조회를 함께 검사한다. 이전 화면이 다른 사용자의 새 접속으로 쓰기를 보내지 않는지도 확인한다. 제품 제출 행동이나 검토자 정책 검증은 아니다. 사용법은 [3번 공통 연결 안내](../../docs/RESULTS_INTEGRATION.md)를 참조한다.

실제 ApiClient 소스를 연결해 후보 페이지/권한·BIGINT 문맥/버전·서버 요청 키·중복 단일 효과·다른 본문 충돌·응답 유실 결과 조회를 확인한다. 서버 pytest가 소유한 loopback Waitress·전용 MySQL fixture에서만 실행하고 종료한다. 제품 업무 저장/수락 API나 전체 WinForms 사용 검증이 아니다. mutation 경로는 `test_task_write_support.py`가 시험 앱에만 등록한다.

저장소 루트:

```powershell
dotnet build .\Tests\WriteSupport.Http.Tests\WriteSupport.Http.Tests.csproj -c Release
```

준비한 서버 Python 환경에서 전용 MySQL의 `MYSQL_TEST_URL`을 지정하고 RemoteWork.Server 폴더에서:

```powershell
python -m pytest -q -s tests/test_task_write_support.py
```

`MYSQL_TEST_URL`은 **실제 행을 비우는 fixture**이므로 독립 `remote_work_test_` DB만 지정한다. 기본 Release DLL을 찾거나 `WRITE_SUPPORT_HTTP_CLIENT_DLL`로 직접 지정한다. DLL/전용MySQL가 없으면 해당 실제 HTTP 검증은 skip이며 통과로 보고하지 않는다. 합성 비밀번호는 시험 자식 프로세스 stdin으로 전달하고 stdout·명령 인자에는 싣지 않는다.

반복한 동일 요청이 실제 업무 fixture 버전/감사를1번만 증가시키는지 DB에서도 대조한다. 시험은 제목/버전만 바꾸고 업무 상태는 유지한다. 공유DB·두PC·제출/승인/마감 정책·2번 실제 UI 저장 연결의 완료 증거가 아니다. [공통 계약/연결 방법](../../docs/WRITE_SUPPORT.md)을 함께 확인한다.
