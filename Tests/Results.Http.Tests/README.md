# 3번 결과물 HTTP·MySQL·WinForms 검증

사용자 DB와 분리한 새 소유 MySQL 프로세스/임시 DB만 생성하고 종료 시 소유를 재확인하여 정리합니다. 실제 직원·업무·제출 자료를 제품 DB에 자동 입력하지 않습니다.

Windows, Python 서버 requirements, MySQL 8.0.16 이상, PowerShell 7 및 .NET 8 참조팩이 필요합니다. C# 검사는 로컬 검증 환경의 8.0.29 참조팩과 PowerShell에 포함된 Roslyn을 사용합니다. 설치 경로가 다르면 검증 스크립트의 참조팩/런타임 경로를 해당 설치와 맞춰야 합니다. 제품 csproj를 변경하지 않습니다.

저장소 루트에서:

```powershell
$env:PYTHON_DOTENV_DISABLED='1'
$env:RESULTS_MYSQLD='C:/Program Files/MySQL/MySQL Server 8.2/bin/mysqld.exe'
python Tests/Results.Http.Tests/verify_cold.py
python Tests/Results.Http.Tests/verify_mysql.py --client
dotnet build RemoteWork.sln -c Release --nologo
```

`verify_mysql.py`는 기존 2번의 소유 MySQL 실행 도구를 읽기 전용으로 재사용합니다. 자격증명은 실행 중 무작위 생성하고 출력하지 않습니다. `verify_server.py` 단독 실행은 별도 SQLite 검증이며 실제 MySQL 검증을 대신하지 않습니다.

이번 alpha 결합본 직접 실행: MySQL 185개, C# 41개 통과. 기본 off 새 프로세스의 경로/모델 미등록 확인. Release 경고 0·오류 0. 로컬 빌드는 설치된 참조팩을 선택하는 외부 검증 설정을 사용했습니다.

실제 공유 DB, 물리 두 PC, 최종 조직/연장 모델 검증은 미실행입니다. 같은 검사를 앞선 로컬 회차와 합산하지 않습니다.

## 2026-10-20 웹 후속 결합 검증

기준 alpha `43f69c99632b1a084e0bc1c441f46d7f1a8eb26d`를 보존한 후속 게시 준비본에서 다음을 직접 실행했습니다.

```powershell
$env:PYTHON_DOTENV_DISABLED='1'
python Tests/Results.Http.Tests/verify_web.py
python Tests/Results.Http.Tests/verify_cold.py
node --check RemoteWork.Server/remotework/static/results.js
```

`verify_web.py`는 같은 소유 MySQL 실행 도구를 사용하여 기존 서버185개, 실제 C# HTTP/WinForms41개, 쿠키/CSRF 웹177개를 확인했습니다. 현재 C# 소스114개 컴파일 오류0·경고0이며 전체 솔루션 Release 빌드와 구분합니다. 초기 웹149개나 이전185/41 회차를 중복 합산하지 않습니다. 사용자 DB·원본 폴더·실제 계정/업무/파일을 변경하지 않고 소유 테스트 HTTP/MySQL/임시 자료를 정리했습니다.

웹 검사는 현재 접속 범위·CSRF·같은 계정 재로그인·직원/관리자 본인/상위 검토자·실제 제출/판단/과거6차·파일 바이트와빈 파일·마감/개발용 연장gate·명시적2번 재작업·응답 유실receipt·100건 이후 이력cursor를 포함합니다. 기존 일반 API 형식/동작도 같은 실행으로 확인했습니다.

`--preview`는 격리된 TEST 자료로만 loopback 브라우저 점검 서버를 열고 종료 후 정리합니다. 앞선 웹 구현 회차에서 직접 브라우저 제출/다운로드 해시 비교·반려/빈 승인 의견·재작업/2차·과거 회차·이력 필터/상세·응답 유실 결과 확인을 실행했습니다. 그 브라우저 조작은 최신alpha 결합 후 전부 재실행한 결과로 표시하지 않습니다. 이번 후속 결합에서는 위 자동 MySQL/실제HTTP 검사를 재실행했습니다.

최신2번 취소/연장 모델의 실제3번 소비·경합/배포확인, 직급 자동 선정, 최종/공유DB·물리두PC·실제장비·운영배포는 미실행/후속입니다.2번 자체 대역 검증을3번 통합 검증으로 쓰지 않으며 실제소비 확인gate를 설치/해제하지 않습니다.

## 2026-10-21 후속 검사

`verify_client.ps1`은 dotnet 실행 위치에서 설치된 .NET8 참조팩과 실행 PowerShell의 같은 major WindowsDesktop runtime을 자동 탐색합니다. 특정 패치 버전8.0.29/10.0.9를 강제하지 않습니다. 필요한 계열이 없으면 설치 필요를 명시하며 제품 csproj를 바꾸지 않습니다.

```powershell
node Tests/Results.Http.Tests/verify_ui.js
python Tests/Results.Http.Tests/verify_lifecycle.py
python Tests/Results.Http.Tests/verify_web.py
python Tests/Results.Http.Tests/verify_cold.py
```

직접 결과: JS handler 대역52, 실제 MySQL/loopback 취소·연장·첨부258, 서버185, C#HTTP41/115소스 컴파일0오류0경고, 최종 웹183통과. 세부 범위와 이전177/183·반복185 중복 합산 금지는 docs/RESULTS.md의2026-10-21절을 따릅니다. JS대역52를 실제브라우저/DB 검사로 표시하지 않습니다. 실패fixture 보완 뒤 최종 결과이며 초기SQL주석/응답envelope/ACK선행조건을 잘못 읽은 테스트 실행은 통과 수치에 포함하지 않습니다. 제품/공유 환경의 lifecycle ACK는 설치하지 않았습니다.


## 2026-10-21 공용 첨부 제공부

`python -B Tests/Results.Http.Tests/verify_shared.py`: 실제 소유 MySQL과 실제 loopback HTTP134검사 통과. 테스트용 업무 소비 route는 2번 제품 코드가 아닌 명시 fixture이며, 실제 업무 등록/교체/취소 화면 연결 완료로 표시하지 않습니다. 실제 SQL 초안3테이블을 전용 DB에만 적용했고 사용자 DB/계정/파일은 변경하지 않았습니다.

범위: 현재 역할·다른 업무 공유 권한, A 삭제 후 B/과거 회차 보존, 신규 업무+첨부 같은TX와 rollback, 중복 제출receipt, 첫/두번째 회차·최신순, 등록 취소·마지막참조·만료정리·삭제실패재시도, 파일크기/개수/이름/해시, 취소 중 업로드/완료 차단·과거 제출 보존. 공유 DB 스키마/2번 소비 연결/두PC/실제 장비는 미검증입니다.

이번 전송함수 재사용 변경 뒤 기존 서버191·웹183·C#41/115소스0오류0경고·취소연장258검사도 통과했습니다. 마지막 권한 조건 보완 뒤 서버191·웹183을 재실행했고 C# 소스는 바꾸지 않았습니다.134 이전의91/112/126·반복191 등을 합산하지 않습니다. 초기 소비fixture 경로/내부 정수ID 보완 과정의 실패 실행은 통과 수에 포함하지 않습니다.

### R52-03 현재 참조 회귀

같은 `python -B Tests/Results.Http.Tests/verify_shared.py`에 실제 인증 snapshot 이후/guard 이전에 요청을 멈추는 테스트 전용 스케줄러를 추가했다. 별도 원본 HTTP 요청이 B 연결 또는 같은 업무의 첨부 교체를 commit한 뒤 앞 요청을 재개한다. 일반 조회가 옛 snapshot을 유지함과 실제 helper의 현재 읽기·다운로드·rollback·최종 정리를 검사한다. 미연결 snapshot 이후 다른 요청에서 연결된 업로드를 삭제하지 못하는 검사도 포함한다.

이번 직접 결과는239개(추가105, 기존134)이며 수정 전 원본은 새 B 참조의 ready 상태 검사에서 실패했다. 실행 명령·범위는 위와 같으며 상세 수정·다른 직접 검사 결과 및 미검증 항목은 [공용 첨부 계약의 R52-03 기록](../../docs/SHARED_ATTACHMENTS.md#r52-03-수정검증--2026-10-21)에 있다. 대역 UI/소유 DB 검증을 실제2번 제품 화면·공유 DB 검증으로 표시하지 않는다.


## 2026-10-22 T3-1~5 코드 PR 검증

실행 기준 alpha `5dcd66e291a0b12470c3347909653a64458ef70e`, LOG `75e6fe5002ad91547de919ab79acfd5a00bbf82b` 위의3번 변경본이다. 새 테스트와 계약은 기존 [PR52 통합 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/52#issuecomment-6308742417)의 후속이며 별도 회신 문서 PR을 만들지 않는다.

```powershell
python -B Tests/Results.Http.Tests/verify_connections.py
python -B Tests/Results.Http.Tests/verify_shared.py
python -B Tests/Results.Http.Tests/verify_lifecycle.py
python -B Tests/Results.Http.Tests/verify_web.py
python -B Tests/Results.Http.Tests/verify_cold.py
node --check RemoteWork.Server/remotework/static/results.js
node Tests/Results.Http.Tests/verify_ui.js
dotnet build RemoteWork.sln -c Release --nologo
```

이번 직접 결과: 새 연결 **150**, 공용 첨부 **251**(포함된105 회귀를 중복 합산하지 않음), 실제 취소/연장 **258**, 서버 **191**, cookie 웹 **183**, 실제 C# HTTP/WinForms **43**, JS 명시 DOM/HTTP 대역 **61** 통과. cold OFF는 결과물 모델·경로에 더해 `results_task_web` 제공부/모듈 미등록을 검사한다. JS 구문·변경 Python 구문·`git diff --check` 통과. Release는 설치된 참조팩을 선택하는 **외부** 오프라인 설정과 외부 출력 폴더를 사용해 오류0/경고0, 제품 csproj/설정은 수정하지 않았다.

C# 추가 회귀는 정상16KiB `FB EF BE` 반복 파일이다. 수정 전 기존 `ResultsFileTransport`와 실제 공통 JSON/서버에서 요청 크기 오류를 재현했고, 전용 전송 DTO의 내부 base64만 pre-encode한 뒤 같은 파일 완료와 원래 size/hash를 확인했다. 기존 일반 파일·제출/반려/재제출/승인/다운로드43검사에 이2검사가 포함된다. 서버 한도/청크 크기를 늘리거나 공통 JSON 인코더·2번 코드를 변경하지 않았다. 재현 전 테스트 대상 업무를 잘못 선택해 상태 검사로 차단된 실행은 결함 재현/통과 수치에 포함하지 않는다.

`verify_connections.py`는 새 소유 MySQL에서 실제 Flask cookie/CSRF handler와 현재 권한·원래 두 버전·W1 receipt 재생·rollback·IO 도중 담당자/참조 변경·서명 복귀/다중 탭/구로그인·읽기 전용 DOM 구조를 검사한다. `/_connections_test` 업무 소비는 명시 **테스트 fixture**이며2번 제품 writer/UI 소비 완료가 아니다. IO 도중 변경은 테스트 전용 패치로 순서를 고정하고 실제 DB commit 전후를 검증한다. `verify_ui.js`는 원본JS를 대역으로 실행하므로 실제 브라우저 검사로 표시하지 않는다.

사용자/공유 DB, 실행 중 서버, 실제 계정/장비·두PC·실제 브라우저 전체 조작·운영 배포/ACK는 미실행·미변경이다.1번의006/009/013 적용 보고 수신과 이번 직접 검증을 구분한다.2번 실제업무 웹 링크/첨부 UI·일반수정 감사/버전/receipt 소비, 공유 저장소/권한·검토자 관계·같은 배포ACK는 후속이다. 별도 자동 제출·재전송·파일 삭제 정책을 추가하지 않았다.

최신 결합본의 최초 병렬 실행은258검사/종료코드0 뒤 Waitress 정리 스레드에서 WinError10038 경고가 출력되어 원본 로그를 보존하고 해당 검사를 단독 재실행했다. 정리 경고를 제품 권한/저장 실패나 추가 통과 검사로 계산하지 않는다.


### PR68 R68-01 성공 안내의 업무·처리 구분 — 2026-10-22

기준 alpha `5dcd66e291a0b12470c3347909653a64458ef70e`, 수정 전 PR head `fec2364b8caf9ba8c4ac43a0052f02f3af479223`이다. [수정 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/68#issuecomment-6331304609)에 따라 기존 성공 안내에 원본 요청의 업무번호·제출/승인/반려/재작업 동작과 서버가 확인한 회차(제공된 경우)를 표시한다. 현재 선택 업무나 입력 중 회차에서 성공 정보를 추측하지 않는다. 즉시 성공 응답과 완료 receipt 조회가 같은 표시 경로를 사용한다.

`node Tests/Results.Http.Tests/verify_ui.js` 직접 **83검사 통과**. 원본 JS를 명시 DOM/HTTP 대역으로 실행한다. A 성공 → 실제 B 선택·B 제출 → B 응답502·원본키 조회, 같은 업무 2차 승인 → 3차 반려 결과불명, 완료 receipt의 회차 표시, 성공 뒤 조회403 보존을 포함한다. 자동 재전송·새 키 발급·첨부 폐기는 추가하지 않았다. 수정 전 JS는 새 성공 식별 검사에서 실패했고 수정 후 통과했다. 기존61에 추가22를 포함한 총83이며 중복 합산하지 않는다. `node --check RemoteWork.Server/remotework/static/results.js`도 통과했다.

제품 변경은 기존 `results.js`의 성공 표시와 서버 응답 전달에 한정한다. 실제 브라우저 조작·공유 DB·운영 서버·두PC는 이번 수정에서 미실행이다. 위 T3-1~5의 기존 전체 검증 기록을 이번에 재실행한 결과로 대체하지 않는다. 협업 회신은 기존 PR52 통합 댓글에 갱신한다.

같은 수정본에서 `python -B -u Tests/Results.Http.Tests/verify_web.py`를 별도 소유 MySQL/임시 테스트 계정으로 실행해 **웹183·서버191·C# HTTP/WinForms43** 통과와 테스트 DB 정리를 확인했다. 이 검사는 실제 cookie/CSRF/HTTP·파일·receipt 경로이며 브라우저 JS 조작 검사가 아니다. 첫 sandbox 실행은 진행이 멈춰 해당 테스트 프로세스만 종료했으며 통과 수치에 포함하지 않았다. 재실행은 별도 격리 환경에서 완료했다. 사용자 DB·실행 중 서버는 변경하지 않았다. 이번 제품 수정은 JS만이므로 전체 솔루션 빌드와 연결150·공용251·lifecycle258은 재실행하지 않았고 위 최초 PR의 결과를 유지한다.
