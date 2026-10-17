# 2번 업무 조회 — MySQL·실제 HTTP 반복 검증 실행기

작업일: **2026-10-17** · 구현 커밋: [16f76f0](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/16f76f0f0a4681083d7b9328cba43cb7e6eae65b) · **읽기/로컬 준비 검증 도구 구현 · 제품 쓰기 기능 미추가**

[현재 안내](README.md) · [기록목록](기록목록.md) · [기존 W1-05 MySQL 검증](2026-10-17_W1-05_상태CHECK_수정_MySQL_검증.md) · [W1 후속 제안](2026-10-17_W1_후속_행동별API_사용자선택_제안.md)

## 이번에 구현한 범위

기존 C# 실제 HTTP 검증을 **직접 소유한 새 MySQL 서버·새 업무 DB**에서 반복 실행할 수 있는 도구를 추가했다. 실제 로그인 → 공통 ApiClient → TCP HTTP → 업무 GET → raw002로 만든 MySQL 테이블 → WinForms 목록/상세/필터/준비 창을 연결해 검사한다. 이전 SQLite DEMO 검증이나 HTTP 대역 결과로 MySQL 연결을 대신하지 않는다.

제품의 조회 API·조회 DTO·화면 배치·CheckedInput·업무 쓰기 기능은 바꾸지 않았다. 테스트의 합성 사용자·업무 fixture와 권한 변경 제어는 검증 도구 내부에만 있으며, 일반 앱에 예시 업무를 자동 생성하거나 업무 등록/수락/재배정 API를 추가하지 않는다. 관리자 역할+해당 업무 지정 관리자 관계가 관리 조회 조건이며 관리자 역할만으로 전체 업무를 허용하지 않는 기존 계약을 검증한다.

업무 등록·본인 수락·마감 신청/처리의 실제 저장은 여전히 미구현이다. 3번의 제출·재제출·파일·차수·승인/반려 기능도 변경하지 않았다. **구현·DB 규약·지속관리·주요 검증은 MySQL 기준**이다. SQLite는 DEMO 종료 후 삭제 예정인 기존 임시 경로로 남기며 별도 기능 관리 대상으로 확장하지 않는다.

## 변경 파일 — 구현 커밋의 6파일

| alpha 경로 | 변경 내용 |
|---|---|
| Tests/TaskQueries.Http.Tests/run_mysql_http_check.py | 새 소유 MySQL 시작/식별·raw002 fixture·실제 HTTP/C# 호출·읽기 전후 해시·권한 회수·정리 실행기 |
| Tests/TaskQueries.Http.Tests/test_mysql_http_runner.py | 명시적 실행 동의·설정 차단·임시 경로/서버 소유권·프로세스 시작/정리 등의 가드16회귀 |
| Tests/TaskQueries.Http.Tests/run_http_check.py | 기존 fixture/클라이언트 호출 도구에 서버 스크립트·프로세스 실행 함수 주입 인자만 추가. 기본 경로 유지 |
| Tests/TaskQueries.Http.Tests/README.md | MySQL 준비·명령·실행 범위·성공/의도적 실패·가드와 실제 검사 구분 |
| docs/TASK_QUERIES.md | 실제 MySQL 반복 검증 진입점·raw002/버전1·공유 적용 및 쓰기 경계 안내 |
| README.md | MySQL 검증 방법·현재 구현 범위·미실행 조건 최신화 |

이 커밋에는 공통 인증·DB 연결·버전/멱등 지원 코드, 업무 모델·002 SQL, 3번 영역, DOCX 변경이 없다. W1-05 상태 CHECK는 [c56f3c8의 이전 변경](2026-10-17_W1-05_상태CHECK_수정_MySQL_검증.md)을 그대로 사용하며 이번 작업에서 다시 구현하지 않았다.

## 소유 환경·DDL·읽기 검증 방식

1. 사용자가 `--allow-disposable-mysql`과 MySQL 설치 절대 경로를 명시해야 실행한다. 기존 MySQL 서비스·Start-MySQL.cmd·외부 DB URL을 사용하지 않는다. 설치 파일은 읽기만 하고 새 임시 datadir·loopback 포트·고유 DB를 만든다.
2. `--no-defaults`를 첫 MySQL 옵션으로 전달하고 X Plugin을 비활성화한다. 상속된 API/DB/프록시/Python 설정·`.env`·MySQL 사용자/시스템 기본 설정을 차단한다. 합성 계정·임시 비밀값만 사용하며 실제 설정/계정은 게시하지 않는다.
3. MySQL·C# native 프로세스는 중지 상태로 생성하고 Windows Job에 등록한 뒤 공식 WinAPI로 재개한다. 등록 전에 자식 트리가 생기는 구간을 막고 이 실행이 소유한 프로세스 트리만 정리한다. 중단/타임아웃·등록 실패 처리도 가드 회귀로 검사한다.
4. 실제 접속 서버의 datadir·포트·MySQL 버전·InnoDB·REPEATABLE-READ가 자신이 시작한 환경과 일치하는지 확인한 후 DB를 만든다. DB 삭제/서버 종료 전에도 다시 대조한다. CREATE 성공한 DB에만 삭제 소유권을 기록하고 포트가 다른 서버로 바뀌면 그 서버에 DROP/SHUTDOWN하지 않는다.
5. 공통7개 테이블만 명시적으로 준비한 뒤 기존 **002 SQL의 실제3문장**으로 업무·배정·관리자 테이블을 만든다. 업무 ORM create_all이나 공통 init_schema로 DDL 실행을 대신하지 않는다. 공통 행을 보존하고 `schema_versions`는 **1**로 유지한다. 정식 migration/version2 지원이나 공유 DB 적용 도구가 아니다.
6. 합성 fixture의 정확한 본인/관리 집합·5상태를 준비해 실제 C# WinForms 메시지 루프에서 조회한다. 본인 업무의 수락/연장 준비, 관리 업무의 수정 준비·빈 등록 창은 로컬 입력 확인까지만 실행한다.
7. PK 순서 해시로 C# 조회/로컬 준비 전후의 업무·배정·관리자 행이 그대로인지 확인한다. C# 실행 중의 로그인/로그아웃은 검증용 공통 접속 동작이므로 공통 인증 행의 변경까지 없다고 주장하지 않는다. 별도 허용/거절 GET 구간은 모든 공통/업무 행의 비변경을 대조한다. 지정 관리자 회수·재배정은 임시 fixture의 stdin 제어로만 수행하고 이후 GET의 현재 권한을 검사한다. 제품 쓰기 API나 쓰기 합의 완료 증거로 사용하지 않는다.
8. 성공·실패 후 자신이 생성한 DB·native 서버·임시 runtime을 정리한다. 재귀 삭제 직전에 최종 절대 경로·부모·접두사·reparse 여부를 검사한다. 비밀번호·토큰·DB 원문·dump·자식 출력은 콘솔/기록에 노출하지 않는다.

## 실제 확인한 테스트와 로컬 반영

결과 근거는 실행 감사 `remote_work_member2_mysql_http_20261017/results.json` 및 해당 최종 로그, 로컬 반영 감사 `remote_work_member2_next_20261017/workspace-sync.json`이다. 감사 산출물·임시 설정/DB·비밀값 자체는 코드 브랜치에 올리지 않는다. 최종 실행의 MySQL binary는 **8.2.0.0**이며 기존 검증 수치와 합산하지 않는다.

| 실제 실행 | 결과·해석 |
|---|---|
| 실행기 가드 회귀 | **16통과**, exit0. DB/native 서버를 시작하지 않은 단위·대역 안전 회귀 |
| 실제 C# 직원 `--live` | **43검사 통과**. 실제 로그인·본인 범위·5상태 필터·행 선택/상세·실제 준비 창·인증 만료 처리 |
| 실제 C# 관리자 `--live` | **64검사 통과**. 본인/업무별 지정 관리 범위·준비 창·필터·선택/상세·접속 폐기 처리 |
| MySQL/TCP HTTP 실행기 | **301검사 통과**, exit0. raw002·버전1·정확한 권한 집합·정렬·5상태·401/403/404·읽기/준비 전후 해시·로그아웃 접속 거절 포함 |
| 권한 회수 | **11검사**, 위301에 포함. 임시 fixture에서 지정 관리자 회수·배정 변경 후 과거 권한으로 GET할 수 없는지 검사 |
| 실제 DB 생성 후 의도적 실패 | **예상 exit1**과 `CLEANUP` 완료. 생성 DB 삭제·native 서버 종료·임시 runtime 제거 확인. 기능 테스트 실패를 통과로 숨긴 것이 아닌 실패 정리 전용 실행 |
| 기존 MySQL 보존 | 실행 전 PID5028/6156과 실행 후 같은 PID 확인. 공유 프로세스 유지, 남은 소유 runtime 경로 없음 |
| 코드 기준/로컬 반영 | 구현16f76f0의 변경6파일을 로컬 `ES/2차 팀 프로젝트`에 반영. 추적 **164파일 일치**, 기존 DOCX7개 해시 보존 |
| Release 빌드 | 소스 프로젝트/로컬 작업공간 확인, **경고0·오류0**, exit0. 빌드를 수동 전체 GUI 승인으로 취급하지 않음 |

301에는 C# 실행 성공 확인과 권한 회수11개 등이 포함되므로 **301+43+64+11을 별도 총검사 수로 합산하지 않는다.** 가드16개는 실제 DB 검사와 구분한다. 이번 결과는 실제 C#→TCP HTTP→MySQL 읽기 연결의 격리 증거이며 공유 서버·운영 업무·두 PC 연결을 뜻하지 않는다.

재현 명령은 프로젝트 루트 기준이다. 서버 가상환경과 MySQL8.0.16 이상, Windows/.NET8, 빌드한 검증 DLL이 필요하다.

```powershell
dotnet build .\Tests\TaskQueries.Http.Tests\TaskQueries.Http.Tests.csproj -c Release
.\RemoteWork.Server\.venv\Scripts\python.exe -m unittest discover -s .\Tests\TaskQueries.Http.Tests -p test_mysql_http_runner.py -v
.\RemoteWork.Server\.venv\Scripts\python.exe .\Tests\TaskQueries.Http.Tests\run_mysql_http_check.py --allow-disposable-mysql --mysql-bin-dir 'C:\Program Files\MySQL\MySQL Server 8.2\bin'
# 아래는 exit1·CLEANUP이 예상되는 별도 실패 정리 검사
.\RemoteWork.Server\.venv\Scripts\python.exe .\Tests\TaskQueries.Http.Tests\run_mysql_http_check.py --allow-disposable-mysql --mysql-bin-dir 'C:\Program Files\MySQL\MySQL Server 8.2\bin' --exercise-cleanup-failure
```

실제 사용한 설치 위치가 다르면 절대 경로를 바꾸고, 검증 DLL 출력이 다르면 `--client-dll`로 지정한다. 동의 옵션 없이 기존 서비스나 사용 중인 DB를 대상으로 실행하도록 안내하지 않는다.

## 이번에 실행하지 않은 테스트

- 실제 C# 프로세스의 강제 중단·타임아웃을 일으키는 native 통합 시험. 해당 정리는 이번 가드 대역 회귀로 확인했으며 실제 정상/의도적 DB 실패 실행과 구분한다.
- 공유 DB·공식 서버·두 PC·공유 백업/복구·정식 migration/version2 적용.
- 제품 업무 등록·수락·마감 신청/판단·제출/승인 쓰기 및 쓰기 동시성/멱등·응답 유실 통합.
- 수동 전체 앱 GUI·웹·장치/지문센서 흐름. 자동 WinForms 준비 창 검사를 해당 검수 완료로 확대하지 않는다.
- 이번에 변경하지 않은 전체 서버/모든 C# 검증 모듈의 전체 회귀. 이 기록의301/16 및 빌드 결과를 이전 전체 회귀 수치로 합산하지 않는다.

## 규약 확인과 다음 구현 조건

검토 시점에는 1번 포크 [jinkyu1452-cmd/Sang1](https://github.com/jinkyu1452-cmd/Sang1)의 alpha·LOG·PR·team1/common-foundation을 GitHub branch API와 `git rev-list --left-right --count`로 대조했다. 고정 원본 기준은 alpha `c56f3c8`, LOG `64662e5`, PR `7424dfb`이며 각 비교의 **포크에만 있는 커밋은0개**였다. 해당 브랜치에서 아직 반영하지 않은 새 W1 지원 코드/추가 회신을 찾지 못한 결과이며 이후 게시 여부를 영구 보장하지 않는다. 감사 근거는 `member1-fork-audit.json`이다. W1-05는 앞선 원본 구현을 재사용하고 포크에서 중복 재구현하지 않았다.

[W1 후속 행동별 API·사용자 선택 제안](2026-10-17_W1_후속_행동별API_사용자선택_제안.md)은 LOG [a756820](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/a756820b7511986deb094f1e81b7ce15e3b96266)에 게시했다. 행동별 경로/입출력·선택 권한·최소 후보/페이지·오류·감사·버전 공급 방식을 **2번 제안·합의 필요**로 제시한 문서이며 새 공통 기능이 구현됐다는 의미가 아니다.

1번 [W1 회신](../1번/2026-10-17_업무쓰기_공통규약_회신.md)의 직원 공통 재인증 쓰기(W1-02), 버전/멱등 저장·전송·결과 확인(W1-03), 최소 후보 조회(W1-07), 실제/기대 스키마 버전 지원(W1-05)은 실제 지원 코드/계약 확인이 필요하다. 2번이 공통 인증·멱등 저장소·사용자 관리 기능을 대신 구현하지 않는다.

3번 [W3 원요청](2026-10-17_3번_수락상태_마감_연결규약_확인요청.md)은 회신 없음·합의 필요다. 초기 저장/수락 예외, 단일 상태/버전 트랜잭션, 제출 이후 수정·재배정/과거 제출 FK, 마감 시간대·연장·제출 경합·상위 검토자를 임의 결정하지 않는다. 자동 결과 제출은 제외하고 자동 검토 요청은 W3-07 질문으로 유지한다.

이후 실제 구현은 **1번 공통 지원과 W3 경계 대조 → 확인된 등록/배정 → 본인 수락 → 날짜/연장 조건이 확인된 마감 관리** 순서다. 일부만 합의되면 독립 가능한 행동만 진행한다. 이번 실행기는 그 대기 중 반복할 수 있는 읽기/MySQL 검증 기반이며 제품 쓰기 완료 보고가 아니다.
