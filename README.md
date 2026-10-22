# 원격 근무용 업무 처리 확인 시스템

업무 웹의 [등록·일반수정 파일 선택 화면](docs/TASK_WEB.md#task-web-file-preparation)은 파일명·크기 확인과 선택 목록 정리를 지원합니다. **현재는 화면 준비 단계로 업로드·저장·서버 삭제를 수행하지 않습니다.** 선택이 남아 있으면 등록 확인·후보 검색을 막아 첨부가 저장된 것으로 오해하지 않게 안내합니다. 좁은 화면 배치와 입력·권한·충돌·서버 오류/처리 결과 확인 안내도 보완했습니다. 원본 JS 86검사·템플릿 16검사와 합성 자료의 실제 브라우저 선택/차단·320/390/1100픽셀 배치를 확인했습니다. 3번 [PR68](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/68)은 alpha에 병합됐으며 실제 웹 전송은 후속으로 연결·검증합니다.

2번은 [현재 본인 업무 전체 사실 읽기](docs/TASK_QUERIES.md#own-deadline-current-facts)를 제공합니다. 같은 인증 TX에서 관리자도 본인 배정만 조회하고 현재 기한·5상태·취소·배정/업무 버전을 반환합니다. 전체 조회 상한 초과/불완전 자료는503이며, 기한 사건·확인 저장·서버UTC·HTTP/실제 알림 source는1·4번과 대조할 후속입니다. 새 DDL·경로·자동 활성화는 없습니다.

2번의 [WinForms 업무 등록 첨부 화면](docs/TASK_CREATION.md#task-creation-attachment-ui)은 파일 선택·명시 업로드·미연결 임시 파일의 확인 삭제를 지원합니다. PR64의 로그인에 고정된 공통 DELETE를 사용하며, 업로드가 완료된 파일만 업무 등록에 연결합니다. 등록 결과가 불명확하면 원래 요청과 파일 목록을 고정해 결과를 먼저 확인합니다. [전송 제어440·실제 HTTP33/DB9검사](Tests/TaskCreationUploads.Tests/README.md), [실제 WinForms와 HTTP 대역95검사](Tests/TaskCreationAttachmentUi.Tests/README.md)를 통과했습니다. 일반수정 첨부 교체·웹 첨부와 공유 운영 서버 검증은 후속입니다.

2번 업무 화면은 [로그인에 고정된 내 업무 탐색 콜백](docs/TASK_QUERIES.md#captured-own-task-navigation)을 제공합니다. 요청 업무를 새 내 업무 목록과 상세에서 확인하며, 대상이 없으면 다른 업무를 대신 열지 않습니다. PR64가 MainForm의 명시적 마감 탐색 연결을 제공합니다. 실제 알림 source·확인 저장과 공유 환경 검증은 후속입니다.

업무 상세를 읽은 뒤에는 첨부 목록의 응답을 기다리지 않고 기존 업무 행동과 새로고침을 사용할 수 있습니다. 인증 만료·로그아웃·행 변경 때는 이전 상세와 행동을 무효화합니다. 업무 웹의 긴 제목·관리자명도 좁은 화면 안에서 줄바꿈합니다. [화면 보완과 검증 범위](docs/TASK_MANUAL_CHECK.md#task-independent-ui-check-20261021)를 확인하세요.

<a id="member2-remainder-implementation"></a>
2번은 취소·마감 연장 웹의 확인/명시 실행/동일 요청 결과 복구, WinForms 업무 첨부 목록·검증 후 다운로드, 취소 시 기존 공용 첨부의 업무 참조 해제를 연결했습니다. 기존 협업 bridge에는 **완전한 2번 이력이 있는 구간만 판정하는 reader**를 제공합니다. 누락·3번 제출 구간은 `None`을 유지합니다. 다른 담당 제품·공유 서버 설정·DB·운영 ACK는 변경하지 않았습니다.

2026-10-21 팀장이 [3번의 첨부 최종 목록·읽기 전용 진입·명시 복귀·웹 첨부 제안 4개를 수용](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member3-proposals-accepted-20261021)했습니다. [신규 등록 API와 C# 요청 DTO](docs/TASK_CREATION.md#task-creation-attachment-selection)는 선택 `attachment_ids`를 지원하며, 값이 있으면 등록된 3번 제공 함수를 같은 트랜잭션에서 호출합니다. 생략·빈 배열은 첨부 없이 등록하며 첨부 제공부가 없어도 동작합니다. WinForms 등록의 파일 선택·업로드는 연결했습니다. 업무 웹은 로컬 선택 화면을 제공하며 실제 전송·저장 연결은 후속입니다.

[일반 수정](docs/TASK_MANAGEMENT.md#task-management-attachment-selection)은 첨부 필드를 생략하면 기존 첨부를 유지합니다. 빈 배열을 포함해 첨부 목록을 명시하면, 같은 트랜잭션의 변경 전 목록·차이 조회와 감사 제공부를 2번에 연결·검증하기 전까지 `503 TASK_ATTACHMENT_REPLACEMENT_NOT_READY`로 차단합니다. 관리 업무의 `view=read`, 서명된 `back_context`, 웹 첨부 cookie/CSRF 경로는 아직 연결하지 않았습니다. 3번은 [PR52 기존 댓글](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/52#issuecomment-6308742417)에 T3-1~5 제품 패치를 제공했습니다. 계약은 기존 수용 기준에 부합하며 새 코드 PR68은 alpha `ff32ba2`에 병합됐습니다. 2번 연결·검증이 남았으며 수정 차단을 미리 해제하지 않습니다. PR64의 captured DELETE는 이번 WinForms 임시 파일 삭제에 소비합니다.

2번의 [업무 웹](docs/TASK_WEB.md)은 공통 로그인 후 `/tasks`에서 내 업무·지정 관리 업무의 목록/상세·전체/5상태·취소 포함을 조회합니다. 기존 공통 쓰기와 기능별 개발 옵션에서는 **등록·수락·재작업 시작·수정·재배정·관리자 변경·취소·마감 연장**을 확인 화면에서 실행하고 저장 결과를 확인할 수 있습니다. 웹과 API는 같은 실행 함수를 사용하며 현재 로그인·관계·버전·상태·날짜를 서버에서 검사합니다. 기존 lifecycle 신청함과 취소·연장 대기 정보도 유지합니다. 1번 PR59의 공통 업무 메뉴는 조회 기능 on일 때 `/tasks`로 연결합니다. PR52는 alpha `cbda025`에 병합됐으며, 현재 본인 업무에서는 기존 3번 결과물 웹으로 이동합니다. 취소·연장 쓰기의 통합 ACK와 실제 변경503은 유지합니다.

업무 웹/기존 API의 [검사 명령·실행 기록·미실행 범위](Tests/TaskWeb.Tests/README.md)를 확인합니다. 3번의 취소·연장 대기 차단 코드는 병합됐지만, 같은 배포의 ACK·공용 DB/013 적용·두 PC 검증은 별도입니다. 신규 등록의 첨부 연결과 수정 차단 검사는 별도 `--suite attachments`로 구분하며, 웹 등록·수정의 서버 전송 본문은 첨부 필드 없이 유지하고, 로컬 파일 선택 목록은 전송에 포함하지 않습니다. 자동 새 키 발급·재전송은 없으며 응답이 불확실하면 기존 확인 화면에서 같은 요청의 처리 결과를 조회합니다.

업무 관리의 [확정 정책과 실행 안내](docs/TASK_MANAGEMENT.md)를 WinForms와 웹에 적용했습니다. 현재 지정 관리자는 진행전 업무의 일반 수정·담당자 재배정, 진행전/진행중/반려완료 업무의 관리자 변경을 별도 확인 후 실행할 수 있습니다. `REMOTEWORK_ENABLE_TASK_MANAGEMENT_DRAFT=1`은 기본 off입니다. 웹의 후보 검색은 처음 연 업무 버전을 유지하며, 입력 확인·페이지 열기만으로 저장하지 않습니다. [취소·마감 연장](docs/TASK_LIFECYCLE.md)은2번 모델/저장·조회·요청함/폼과 PR51 공통 등록·성공 결과 읽기/009를 제공합니다. PR52의 취소/pending 차단 제공과 같은 배포의 적용·통합 검증을 구분하며, 연결 확인 전 변경503을 유지합니다.

2번 화면 조작의 준비·확인·명시 실행, 작은 창/오류 안내와 검증 범위는 [화면 실사용 점검 안내](docs/TASK_MANUAL_CHECK.md)를 확인합니다. 자동화·테스트 대역과 공유 실계정의 수동 검증 결과를 구분합니다.

4번 회신에 따른 2번 제공 접점으로 [로그인 접속에 고정된 읽기 어댑터](docs/TASK_QUERIES.md#captured-read-session)를 추가했습니다. `CaptureReadSession()`은 기존 목록·상세 GET과 오류 분류만 재사용하며 로그인 변경 뒤 새 계정으로 자동 이동하지 않습니다. [전용 92검사](Tests/TaskReadSession.Tests/README.md)와 기존 조회304검사를 통과했습니다. 명시적 업무 대역의 취소 제외/포함 필터도 수정했습니다. 2026-10-21 [알림 A1~A6·협업 B1~B7 진행 기준](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member4-policy-adopted-20261021)은 채택됐고 PR61의 독립 알림 UI·정책·source port가 병합됐습니다. 실제 알림용 DTO/endpoint·현재 관계/기한 사건·서버 시각·확인 저장 계약과 실제 source 주입·공유 통합은 후속입니다. 명시적 MainForm/업무 탐색 조립은 PR64에서 제공됐습니다.

5번 Pi 실행 조립은 `EnrollmentRuntime`과 `main(enrollment_factory=...)`의 명시 주입으로 기존 지문 센서·잠금·OLED를 등록과 공유합니다. 기본 실행은 등록 비활성이며 키/정책 자동 생성·서비스 설치는 하지 않습니다. [실행 구성·사용자 Pi123개 대역 검사·실물 적용 전 조건](Devices/RaspberryPi/FINGERPRINT_ENROLLMENT.md)을 확인합니다. 관리자 화면/장치 주소 연결과 실제 신규 지문 저장·회원 매핑/운영 배포는 후속입니다.

5번 새 회원 지문 등록의 [Pi 등록 코어·서명/온라인 승인·C# 중계와 전용 등록창](Devices/RaspberryPi/FINGERPRINT_ENROLLMENT.md)을 제공합니다. 기본 장치 실행에는 등록 경로를 켜지 않습니다. 관리자 사용자 관리의 생성한 회원 지문 등록 버튼과 장치별 주소 조립은 [1번 연결 안내](docs/DEVICE_ENROLLMENT_API.md)에 구현·검증 범위를 기록했습니다. 실제 키/주소 설치와 실물 통합 검증은 별도입니다. 실물 신규 저장·회원 매핑/운영 서비스 배포는 아직 검증하지 않았습니다. 서버 계약 [PR44](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/44)와 등록 코어/중계 [PR47](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/47)은 원본 alpha에 병합됐으며, 이번 후속은 기존 자원을 공유하는 Pi 실행 조립입니다.

팀원 병행 개발용 [공통 점검·지문 메시지 초안/가상 fixture·화면 요청 취소 도구](docs/TEAM_INTEGRATION_SUPPORT.md)를 제공합니다. 실행 중 서버/DB를 변경하거나 실제 장치·출퇴근·MainForm을 연결하는 기능은 아닙니다.

원격 근무 중 업무 배정·상태·마감·결과 검토와 공지·채팅·근태를 관리하는 팀 프로젝트입니다. `alpha`는 화면 초안과 담당별 구현을 통합하는 개발 브랜치입니다. C# WinForms 클라이언트와 Python 공통 서버를 포함합니다.

현재 공통 로그인·사용자 관리·HTTP·DB 기반과 공지·채팅의 별도 개발 활성화에 따른 DB 저장과 채팅 화면 자동 갱신이 구현돼 있습니다. 업무 목록·상세는 공통 인증을 사용하는 GET API·HTTP 서비스와 기존 화면을 연결한 2번 개발 초안입니다. 업무 API는 명시적으로 활성화한 개발 서버에서 사용하며, 기본 실행에는 미확정 업무 스키마를 적용하지 않습니다. 설계·합의 사항은 [DOCX](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/tree/DOCX/DOCX), 구현·검토 이력은 [LOG](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/tree/LOG), 담당별 수정 요청과 재검토 결과는 [PR](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/tree/PR)에서 관리합니다.

## 현재 구현 범위

| 담당 | 영역 | 현재 동작 |
|---|---|---|
| 1번 | 공통 서버·DB·인증·사용자·통신 | 실제 로그인·역할 진입, 사용자 관리·비밀번호 변경, 접속 만료·폐기, 공통 응답·오류·트랜잭션·감사 이력, 웹 로그인·사용자 목록, 개발용 W1 직원 쓰기 guard·후보/문맥·버전·중복 요청 공통 지원 |
| 2번 | 업무·상태·마감 | GET 목록/상세·전체/5상태·취소 포함과 본인/지정 관리자 권한. WinForms와 업무 웹의 등록·수락/재작업·수정/재배정/관리자 변경, 명시 확인·실행·결과 복구. 본인 업무는 기존3번 제출 폼/결과물 웹으로 연결. 취소/연장 모델·저장·요청함/WinForms·웹5행동, 업무 첨부 조회/다운로드·취소참조해제, 신규 등록 API/C# DTO의 선택 첨부 목록. WinForms 등록 첨부 업로드·삭제는 연결, 웹은 로컬 선택 준비 단계이며 첨부 수정·웹 전송은 후속. 취소/연장은 같은 배포의 연결 확인 전 실제 변경503 유지 |
| 3번 | 결과물·제출·검토·이력 | 별도 opt-in 개발 서버의 실제 제출·파일·승인/반려·이력 및 기존 4개 WinForms 연결. 직원/관리자 웹 결과물·제출/검토·관리자 이력도 동일 저장 처리 연결(로컬 검증). 최종 조직/연장·공용 첨부·성공 구독·공유 환경은 후속 |
| 4번 | 공지·채팅 | 개발 opt-in 시 웹/WinForms 공지 목록·상세·관리자 작성/삭제, 대상·고정·정렬. 웹20개 페이지/선택 후 조회. 채팅은 독립 활성화 시DB 저장·개인 숨김·약1초 자동 갱신 |
| 5번 | 출퇴근·연차·장치 | 독립 라파 지문·조도 API, 메가 A0 송신, Windows 장치 점검 도구와 자동 실행 예제. 기존 근태 폼·공통 회원/인증·DB 저장 연결은 별도 |

업무 조회는 현재 본인 배정과 업무별 지정 관리자 조건을 서버에서 각각 검사합니다. 2026-10-17 [1번 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/1번/2026-10-17_업무조회_API_DB_규약확정_회신.md)으로 조회 API·DTO·권한·오류와 상태/날짜 표현·공통 FK 기준을 수용했습니다. 이후 수정·관계 변경·취소·연장은 [팀장 확정 기준](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-management-policy-approved-20261020)을 따릅니다. 공통 등록과3번 취소·연장 대기 helper 소비는 제공됐으며, 정식 migration·전역 스키마 버전 변경·공유 DB 적용·두 PC·같은 배포의 통합 ACK는 별도입니다.

W1 공통 쓰기 지원은 [WRITE_SUPPORT](docs/WRITE_SUPPORT.md)를 따릅니다. 명시적 개발 활성화에서 직원 쓰기 세션 재검사·사용자 후보/업무 문맥·서명 요청 키와 성공 결과 보관·C# 명시적 키 전송을 제공합니다. 2번의 [신규 등록 API·WinForms 연결](docs/TASK_CREATION.md)은 이 공통 지원을 재사용합니다. 수락·재작업도1번의 [기술 수용 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/1번/2026-10-19_수락_재작업_API_기술수용회신.md)을 받아 별도 시작 옵션으로 연결했습니다. [일반 수정·재배정·관리자 변경](docs/TASK_MANAGEMENT.md)도 기존 공통 action/최소 결과를 재사용합니다. 공통 schema version1·기본 비활성화를 유지하며 공유 DB 적용과 구분합니다.

2번은 W1에 [후보·쓰기 준비 연결](docs/TASK_WRITE_PREPARATION.md)을 추가했습니다. 현재 업무의 ID·버전·배정 정보와 담당자/업무 관리자 검색·페이지·선택을 실제 HTTP로 조회합니다. 선택과 입력 확인만으로 저장하지 않으며, 신규 업무는 확인 후 별도 등록 버튼을 눌러 서버에 전송합니다.

업무 화면은 1번의 `FeatureUiScope`와 공통 캡처 세션을 재사용해 목록·상세·입력 준비·등록 workflow를 같은 로그인에 묶습니다. 로그아웃·인증 만료·계정 교체 때 이전 목록/상세/선택을 지우고, 늦게 도착한 응답은 반영하지 않습니다. 새 로그인은 사용자가 목록을 다시 조회할 때 연결하며 준비 정보도 함께 갱신합니다. 같은 접속의 필터·선택 복원과 명시적 결과 복구는 유지합니다. 4번 알림/협업의 새 권한·기한 revision과 5번 장치/근태 조건은 업무에 추가하지 않았습니다.

내 업무의 진행중 상세에서 **결과 제출**을 누르면 같은 로그인·선택 업무로 3번의 기존 제출 폼을 엽니다. 실제 제출/결과 확인 성공 후 현재 상태 필터로 업무를 다시 조회하므로 진행중 필터에서는 제출된 행이 사라질 수 있습니다. 진행전/반려완료는 먼저 수락/재작업 시작이 필요하고, 관리 업무의 타인 결과는 제출하지 않습니다. 제출 자격·기한·검토자는 결과물 서버에서 최종 검사합니다. 제출 결과가 불명확하면 **결과물 메뉴 → 제출 결과 확인**의 기존 복구 경로를 사용합니다. 별도 검토 요청은 협의사항이며 자동 제출·2번의 추가 submitted 저장은 없습니다. [2·3번 통합 검사](Tests/TaskResults.Integration.Tests/README.md)는 소유 MySQL/합성 계정의 실제 HTTP·WinForms 검사이며 공유 실계정·두 PC 완료와 구분합니다.

[수락·재작업 대상 확인](docs/TASK_START_PREPARATION.md)은 기존 `write-context` GET으로 현재 본인 배정·상태·버전을 읽습니다. 진행전은 업무 수락, 반려완료는 재작업 시작 대상을 확인하며 관리자 본인도 같은 기준을 사용합니다. 상세와 현재 조회의 상태가 다르면 확인을 막고 목록·상세를 다시 조회하도록 안내합니다. `대상 확인`은 별도 불변 `CheckedTarget`만 보관하며 업무 POST·키 발급·결과 조회나 상태 변경은 하지 않습니다. 날짜는 팀장 확정으로 **한국 시간 시작일00:00부터 마감일이 끝날 때까지** 허용합니다. 서버 내부에는 이 날짜·대상 상태·버전 충돌과 증가·최초 수락 기록 보존을 묶는 불변 전이 계획도 준비했습니다. 순수 계획 자체는 저장이 아니며, 실제 시작 API가 현재 권한·잠금·버전·수락 기록·KST 기간을 검사한 뒤 같은 트랜잭션에 상태·감사·성공 결과를 저장합니다. 별도 REMOTEWORK_ENABLE_TASK_START_DRAFT=1이 필요하고 기본off입니다. 응답 유실 시 기존 키/본문을 보관하며, 필터 밖/빈 목록에서도 명시적으로 결과를 확인할 수 있습니다. 일반 수정·재배정·관리자 변경은 별도 관리 옵션으로 연결했습니다. 취소·연장은2번 저장·조회/폼과 공통 등록·3번 차단 코드를 제공하며 같은 배포의 검증·통합 ACK 전에는 실제 변경을 막습니다. 별도 검토 요청 등 이번 결정 밖의 항목은 협의사항입니다.

기존 등록/수정·마감 연장 입력 준비창은 로그인 변경·취소·종료 뒤 이전 확인값을 즉시 폐기하고 입력/확인을 막습니다. 기존 로컬 준비창 DeadlineExtensionForm은 캡처 접속의 수명만 빌리고 새 조회·신청 요청을 보내지 않습니다. 실제 HTTP 업무 화면의 별도 TaskLifecycleForm은 최신 문맥을 GET 조회하고 사용자의 확인·명시 실행을 제공합니다. 기본·미리보기 생성자는 유지하며 취소·연장 변경은1번 공통·3번 제출/첨부 차단의 같은 배포 검증·통합 ACK 확인 전503입니다.

신규 업무의 관리자 기준은 **최소 1명·등록자 자동 포함·최초 등록 시 등록자 제외 불가**입니다(2026-10-19 사용자 확정). 선택 준비 화면에 등록자를 필수 관리자로 표시하고 해제를 막습니다. 추가 관리자는 기존 후보에서 선택하며 제출 검토자는 별도입니다. 명시적 등록 시 서버가 현재 후보·등록자를 다시 검사하고 DB에 저장합니다. 응답이 불확실하면 같은 공통 클라이언트·로그인 세션의 요청을 메모리에 보관해 등록 창과 업무 메뉴를 다시 열어도 이어갑니다. 사용자가 `처리 결과 확인`으로 `not_recorded`를 확인했을 때만 같은 키·본문으로 재전송할 수 있습니다. POST의 키 만료 응답도 보관된 결과를 먼저 확인하며, 앱 종료·로그인 변경 후의 복구 저장소는 제공하지 않습니다. 자동 재등록은 하지 않습니다. 기존 업무의 관리자 변경은 진행전/진행중/반려완료에서 현재 지정 관리자가 실행하며 활성 관리자 한 명 이상을 유지합니다. 반려 후 흐름은 **반려완료 → 재작업 시작 → 진행중 → 재제출 성공 → 제출완료**로 확정했습니다. 재작업 시작에서는 제출 회차를 올리지 않습니다. [3번 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/2026-10-19_3번_재작업상태_확정_및_연결회신.md)의 담당 경계를 따릅니다. 재작업 시작 API와 기존3번 제출 폼/API 연결은 별도 개발 옵션·소유 환경 검증으로 제공하며, 연장 기준은 확정 A~E를 적용합니다. 새 취소 guard·대기 연장 facts 소비 코드는 PR52로 제공됐으며 같은 배포의 통합·공유 검증은 남아 있고 검토자·별도 검토 요청의 미확정 정책은 협의사항입니다.

팀장 공통 기준과 담당별 회신은 [LOG/2번 공통규약](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md), 현재 구현·실행/미실행은 [통합 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/업무_구현검증.md)에서 확인합니다. 1번 부재 중 공통 코드를 대신 변경하지 않았으며, 초기 배정/업무 버전 1/1·신규 등록 트랜잭션·최소 결과와 새 개발 DB의 DDL은 [1번 기술 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/1번/2026-10-19_협업요청_기술검토_회신.md)으로 수용됐습니다. 정식 migration·공유 적용 승인은 별도입니다.

## 5번 장치 독립 시연

새 지문 본인인증의 [RW-ID-v2 연결 계약](docs/DEVICE_IDENTITY_V2.md)은 API DTO·방향별 메시지·공개 벡터와 승인/실패/재시도 기준을 제공합니다. 현재 **계약 구현 단계**이며 실제 인증 endpoint·제한 로그인·MySQL 소비·출퇴근 전환은 아직 설치되지 않았습니다. 등록 API나 일반 `/scan`을 이 인증의 성공으로 사용하지 않습니다.

[장치 설치·실행·API·검증 범위](Devices/RaspberryPi/README.md)에 지문 센서·SH1106 OLED·메가 조도 송신과 systemd 자동 실행 예제를 정리했습니다. [Windows 점검 도구](Tools/DeviceCheck/README.md)는 공통 WinForms와 별도로 실행하며 조도 조회와 수동 지문 요청을 제공합니다. 실제 표시 이름 설정·센서 이벤트 CSV·장치 원본 데이터는 게시하지 않습니다.

[Windows USB 웹캠 손 검출](Tools/HandPresence/README.md)은 [C# 활동 상태 확인 창](RemoteWork.WinForms/Features/Attendance/README.md)에서도 실행할 수 있습니다. 손·화면 움직임·라파 조도 변화가 모두 없는 정상 관찰이30초 이어지면 자리 비움을 표시하며, 하나라도 감지되면 다시30초부터 확인합니다. 연결 문제는 확인 필요로 구분합니다. 같은 PC의 Python이 선택 영역 미리보기·신호를 C#에 전달하며 Pi는 읽기 전용 조회만 합니다. 영상 저장·외부 전송·얼굴 식별·자동 근태 기록은 없으며 몸통 검출과 근태/회원 연결은 남아 있습니다.

가림 감지와 최근 변화 이력을 구분하며 손가락 그림자로 움직임을 모의실험합니다. 재석/이석·출퇴근을 자동 판정하지 않습니다. 센서 슬롯과 로그인 회원의 연결·장치 신뢰·인증 시점·공통 API/DB는 1번·5번 및 팀장과 합의 후 통합합니다. 이 장치 API의 시각/응답 형식은 독립 시연용이고 기존 공통 인증·UTC·업무 계약을 대체하지 않습니다.

## 기술과 실행 환경

- 데스크톱: Windows, .NET 8 SDK, C# WinForms.
- 서버: Python 3.11 이상, Flask/Waitress, SQLAlchemy/PyMySQL.
- DB: 실제 구현·DB 규약·지속 관리와 주요 검증은 MySQL 기준입니다. SQLite는 DEMO용 임시 구성이며 DEMO 종료 후 삭제할 예정입니다.
- UI 구조: `LoginForm → MainForm → 기능별 UserControl`. 기존 화면의 Designer·도구상자 편집 구조를 유지합니다.

## 실행

1. 처음 화면과 공통 로그인을 확인하려면 프로젝트 루트의 `Start-Demo.cmd`를 실행합니다. 별도 SQLite 데모 DB가 준비되며 최초 생성 계정의 무작위 비밀번호는 서버 창에 표시됩니다.
2. 서버를 켠 상태에서 `Start-Desktop.cmd`를 실행하거나 `RemoteWork.sln`을 Visual Studio로 열어 `RemoteWork.WinForms`를 시작합니다.
3. 웹 공통 화면은 `http://127.0.0.1:5000/login`에서 확인합니다.

```powershell
dotnet run --project .\RemoteWork.WinForms\RemoteWork.WinForms.csproj
```

공동 MySQL 설정·초기화·테스트 계정 준비는 [SETUP](docs/SETUP.md)을 따른 뒤 `Start-MySQL.cmd`로 실행합니다. 서버 패키지 설치에는 인터넷 연결이 필요합니다. 실제 `.env`는 `.env.example`을 참고해 로컬에서 준비합니다.

클라이언트에는 `http://서버IP:5000/`처럼 **서버 루트**를 설정합니다. 공통 `AppServices.Api`가 요청 경로에 `/api/v1/`을 붙입니다. 다른 PC의 `127.0.0.1`은 서버 PC를 가리키지 않으므로 서버 주소·수신 설정·방화벽과 두 PC 연결을 별도로 확인해야 합니다.

## 로그인과 공통 연결

아이디·비밀번호로 로그인한 서버 사용자의 역할에 따라 직원·관리자 화면으로 진입합니다. 로그인 화면의 기존 미리보기 버튼 위치는 서버 연결 설정과 웹 로그인 열기에 사용합니다. 회사명은 확정 전까지 `회사명`으로 표시합니다.

2026-10-17 사용자 결정에 따라 지문센서는 **로그인 이후 원격 근무 중 본인 인증 수단**으로 사용합니다. 사용자가 설명한 취지는 인증 시점에 본인이 근무 자리에 있는지 확인하는 **재석 확인**입니다. 로그인 화면의 지문 버튼과 로그인 대기 안내는 제거했습니다. 실제 센서 통신·인증 화면·API·DB는 5번 담당의 연결 전이며, 인증 요청 시점·주기·응답 시간·미응답/실패 처리·출퇴근 연계는 1번·5번 등 관련 담당과 합의가 필요합니다. 기존 업무 조회나 업무 상태에 장치 인증 조건을 임의로 추가하지 않습니다.

공통 API는 Bearer 인증, snake_case 필드와 UTC ISO 8601 기록 시각을 사용합니다. `ApiClient`는 접속 세대·서버·토큰을 요청에 연결하여 이전 접속의 늦은 응답이 새 로그인 상태를 바꾸지 않게 합니다. 현재 접속의 401은 본문 JSON 형식과 관계없이 인증 만료로 처리합니다. 공통 관리자 변경은 잠금 뒤 수행자·접속·관리자 상태를 다시 확인합니다.

실제 API·오류·DB·기능 등록 방법은 [INTEGRATION](docs/INTEGRATION.md)을 따릅니다. 문서의 이전 업무 코드 블록과 새 2번 GET 개발 API를 구분합니다. 현재 조회 API와 활성화 조건은 [업무 조회 개발 계약](docs/TASK_QUERIES.md)을 따릅니다.

3번과 함께 사용할 공유 검증 서버와 직원·관리자 계정은 아직 준비 전입니다(2026-10-19 팀장 확인). 2번의 [읽기 전용 준비 점검](docs/TASK_QUERIES.md#환경-선행-조건-점검)은 기존 서버 설정으로 업무 조회 flag·필수 테이블/열·공통 버전·배정/관리자 참조를 확인합니다. 테이블·샘플·계정을 생성하지 않으며, 성공도 실제 로그인/권한·공유 서버·3번 화면 연결 완료를 뜻하지 않습니다. [담당별 후속 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#3번-실행-환경-요청-회신)을 확인합니다.

2번 쓰기 연결용 [W1 공통 지원](docs/WRITE_SUPPORT.md)은 기본 비활성 개발 초안이며, 실제 업무 저장 API와 구분합니다.

## 공지 DB 개발 초안과 채팅 저장 범위

**공지 삭제 정책(2026-10-20 팀장 확정):** 관리자가 공지를 삭제하면 해당 공지의 직원별 확인 여부·확인 시각 기록도 함께 삭제하고, 삭제 감사 로그는 별도로 남깁니다. 독립 중요공지 팝업/확인 기능은 [POPUP](RemoteWork.Server/remotework/features/notices/POPUP.md)·[008](RemoteWork.Server/migrations/008_notice_popups.md)의 기본off 개발 옵션이며 실제 앱/공유DB 적용·팝업 활성화는 별도입니다. [검토·병합 근거](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr45-merged-delete-policy-20261020).

[웹 공지 화면](RemoteWork.Server/remotework/features/notices/WEB.md)도 기존 notice opt-in에서 연결됩니다. 웹 로그인 후 공지 메뉴에서20개 페이지 목록·선택 후 조회·대상/정렬·관리자 작성/삭제를 사용합니다. 같은 notices와 기존 API를 공유하고, 단순 조회는 현재 페이지를 유지하며 필터/정렬 변경은1페이지로 초기화합니다. 저장 commit 후303으로 이동하고 기본off·전체32KiB·쿠키/CSRF를 유지합니다. 웹 팝업·확인 저장·실제 파일 전송·마감 알림은 별도입니다.

공지 작성·목록·상세·관리자 삭제를 공통 인증 서버와 notices 테이블에 연결합니다. 전 직원·기획팀·개발팀·디자인팀을 target으로 구분하고 사용자 결정에 따라 직원도 모든 부서를 조회합니다. 작성자·UTC 시각은 서버에서 결정하며 저장 성공 때만 작성 창을 닫고 오류 시 입력을 유지합니다. 목록/대상 필터·고정 우선·열 정렬·조회 버튼 상세를 유지합니다. 정적 샘플 공지는 제거했고 종료 후 유지 여부는 연결한 DB가 담당합니다.

공지 API/ID/권한은 PR #12의 1번 수용 기준이며 **기본 off**를 유지합니다. 프로젝트 루트의 PowerShell에서 `$env:REMOTEWORK_ENABLE_NOTICE_DRAFT = "1"`을 지정하고 `& .\Start-Demo.cmd`를 실행하면 별도 SQLite 데모에 공지 테이블을 준비합니다. 기존 업무 플래그와 독립적으로 등록하며 공통 인증·스키마 버전1·업무 코드는 보존합니다. 실제 격리 MySQL 검증을 수행했고 migration003은 번호 예약에 따른 SQL 검토안입니다. 정식 migration·전역 스키마 버전 변경·공유 적용·정식 활성화는 대기입니다. [공지 준비·API·제한](RemoteWork.Server/remotework/features/notices/README.md)을 따릅니다.

첨부는 파일명 메타데이터만 저장하고 원본 절대경로·파일 본문을 서버에 보내지 않습니다. 실제 업로드/다운로드·중요 공지 자동 팝업·직원별 확인 기록과 정렬 추가 조정은 이번 DB 변경에 포함하지 않습니다.

실제 로그인 채팅은 공통 세션과 서버의 현재 참여자 검사에 연결합니다. 독립 개발 활성화 시 방·메시지·개인 숨김을 MySQL에 저장하며, 채팅 화면을 표시하는 동안 약 1초마다 방과 선택 대화를 조회합니다. 숨김은 본인 목록에만 적용하며 상대방의 메시지·자료를 삭제하지 않습니다. 기본 off이며 [채팅 실행·DDL 안내](RemoteWork.Server/remotework/features/chat/README.md)와 [자동 갱신 범위](RemoteWork.WinForms/Features/Collaboration/CHAT_AUTO_RECEIVE.md)를 따릅니다. 명시적 미리보기는 기존 메모리 대역을 사용합니다. 실제 두 PC·공유 DB·장시간 부하는 아직 검증하지 않았습니다.

## 업무 조회와 상태 기준

`ITaskQueryService`·`TaskSummary`·`TaskDetail`을 유지하고 실제 로그인 MainForm이 공통 `AppServices.Api`를 사용하는 HTTP 조회 서비스를 주입합니다. 기본 TasksControl·미리보기 생성자는 미구성 서비스를 유지하고 테스트 대역은 명시적으로 주입합니다. 서버 오류를 샘플 목록으로 대체하지 않습니다.

`GET /api/v1/tasks?scope=own|managed&status=...`와 `GET /api/v1/tasks/{id}`를 제공합니다. 내 업무는 직원·관리자의 현재 본인 배정만, 관리 업무는 해당 업무에 지정된 관리자만 조회합니다. 직접 상세 접근도 서버에서 검사하며 관리자 역할만으로 전체 업무를 열람하지 않습니다. 첫 연결은 전체 목록 반환이며 페이지·검색 조건은 아직 지원하지 않습니다.

같은 조회 조건으로 다시 조회하면 선택 업무를 유지합니다. 상세 실패 후에는 해당 상세만 재시도하고 F5로는 전체 목록을 다시 받습니다. 범위·상태 변경 시 선택을 초기화하며, 접속 변경 취소는 이전 데이터를 지우고 취소 안내로 구분합니다. 요청한 상태와 다른 목록 응답은 정상 결과로 표시하지 않습니다. 이 개선은 API·DB·업무 상태 전환 규칙을 변경하지 않습니다.

목록 조회 이후 같은 업무의 제목·담당자 표시명·상태·시작일·마감일이 달라진 상세를 받으면 변경 감지 안내와 `목록 새로고침`을 표시합니다. 최신 상세를 보여주고 목록은 조회 당시 결과로 유지합니다. 버튼이나 F5로 현재 조회 조건의 목록을 다시 받으며, 같은 조건의 선택 복원과 새 결과의 상태 필터를 따릅니다.

등록·수정은 기본 입력을 확인하고, 수락·재작업은 대상 확인 후 별도 실행으로 시작하며, 마감 연장은 실제 선택 업무의 현재 마감일과 희망일·사유 입력을 준비합니다. 수정·새 수락/재작업·연장 준비는 현재 상세 조회 성공 후에 열립니다. 이미 전송한 수락/재작업의 결과 확인은 선택 행이 없거나 필터 밖인 경우에도 열 수 있습니다. 입력 확인은 로컬 형식 검사이며 창에 값이 유지됩니다. 확인된 제목·내용·날짜와 원본 업무 ID는 불변 로컬 입력값으로 보관하고, 입력이 바뀌면 확인값을 무효화합니다. 날짜 체크의 변경 이벤트가 없더라도 읽는 시점에 현재 입력과 다시 대조합니다. 신규 등록은 이 확인값과 별도 후보 선택을 다시 검사해 등록 전용 DTO를 만들며, 별도 등록 버튼에서만 POST합니다. 성공 응답 또는 저장 결과 확인 후 현재 범위·상태 필터로 재조회하고, 새 업무가 결과에 포함되면 해당 행·상세를 선택합니다. 필터 밖이면 기존 조회 조건을 유지합니다. 401·403이나 접속 변경은 이전 요청과 창의 등록 동작을 차단합니다. 일반 수정·재배정·관리자 변경은 별도 관리 폼/옵션으로 연결했고, 취소·연장은 [별도 lifecycle 연결](docs/TASK_LIFECYCLE.md)에서2번 모델/저장·조회/폼을 제공하며 같은 배포의 공통/3번 통합·ACK 확인 전에는503으로 막습니다. [입력 준비 안내](docs/TASK_INPUT_PREPARATION.md)의 경계를 따릅니다.

업무 조회는 서버 `REMOTEWORK_ENABLE_TASK_QUERY_DRAFT=1`로 명시적으로 활성화합니다. **삭제 가능한 개발·테스트 DB에서만** 활성화·초기화합니다. 1번이 예약한 `002_task_queries.mysql.sql`은 [검토용 DDL·검증/복구안](RemoteWork.Server/migrations/002_task_queries.md)으로 준비하며, 공유 적용 승인이나 버전2 구현을 뜻하지 않습니다. 현재 공통 버전1과 기본 비활성화를 유지합니다. 기본 서버의 미등록 업무 경로는 연결 필요로 표시합니다. 일반 실행에 업무 데이터를 자동 생성하지 않습니다. [신규 등록 API](docs/TASK_CREATION.md)는 조회 옵션과 함께 `REMOTEWORK_ENABLE_COMMON_WRITE_DRAFT=1`, `REMOTEWORK_ENABLE_TASK_CREATE_DRAFT=1` 및 준비된 002/004 개발 테이블을 요구합니다. WinForms 저장 연결은 이 개발 범위에서 검증했으며 1번의 등록 초기값·서버/DB 기술 수용을 받았습니다. 정식 migration·공유 배포는 미완료입니다.

W1-05 요청에 따라 002 SQL과 업무 ORM의 상태 CHECK는 기존 IN과 HEX 비교를 함께 사용해 정확한 다섯 소문자 코드만 허용합니다. DB collation이 대소문자·뒤 공백을 같게 비교해도 대문자·혼합·공백 변형을 거절하고, SQLite의 TEXT와 BLOB 구분도 유지합니다. 기존 공유 DB 제약의 자동 수정이나 상태 전환 API는 추가하지 않습니다.

업무 상태는 **진행전 / 진행중 / 제출완료 / 승인완료 / 반려완료**입니다. 배정→진행전, 수락→진행중, 정식 제출·재제출→제출완료, 검토 판단→승인완료/반려완료를 기준으로 합니다. 모든 정식 제출은 승인·반려를 거쳐야 하며 승인완료만 최종 완료입니다. 관리자 본인 업무도 같은 흐름을 사용하고 상위 검토자가 판단합니다.

조회 계약은 [TASK_QUERIES](docs/TASK_QUERIES.md)에 정리했습니다. 5개 문자열 코드·DATE/DateOnly 형식·현재 상세403/업무 전용404 구분은 1번 수용 기준입니다. 반려 후 재작업 시작 흐름과 수락/재작업의 KST 시작일~마감일 당일 내 허용은 팀장 확정입니다. 시작 API/action·오류 접점은1번 기술 수용에 맞춰 개발 연결했고 제출/판단은3번 구현을 재사용합니다. 취소·연장의 공통 등록/3번 소비 코드는 제공됐으며 같은 배포의 검증·통합 ACK와 최종 조직 검토자·자동 검토 요청은 별도입니다. **마감 경과에 따른 자동 결과 제출은 제외**합니다.

## 검증

Windows에서 소스 루트 기준으로 실행합니다.

```powershell
dotnet build .\RemoteWork.sln -c Release
dotnet run --project .\Tests\ApiClient.Tests\ApiClient.Tests.csproj -c Release
dotnet run --project .\Tests\Collaboration.Tests\Collaboration.Tests.csproj -c Release
dotnet run --project .\Tests\Notices.Tests\Notices.Tests.csproj -c Release
dotnet run --project .\Tests\TaskQueries.Tests\TaskQueries.Tests.csproj -c Release
dotnet run --project .\Tests\TaskQueries.Http.Tests\TaskQueries.Http.Tests.csproj -c Release
dotnet run --project .\Tests\TaskCreation.Tests\TaskCreation.Tests.csproj -c Release
dotnet run --project .\Tests\TaskStartPreparation.Tests\TaskStartPreparation.Tests.csproj -c Release
```

서버 테스트는 `RemoteWork.Server`에서 개발 의존성을 설치한 뒤 실행합니다. MySQL 동시성 테스트는 삭제 가능한 별도 `remote_work_test_` DB를 지정해야 합니다.

```powershell
python -m pip install -r requirements-dev.txt
python -m pytest -q
```

| 테스트 | 범위 |
|---|---|
| [TaskCreation.Tests](Tests/TaskCreation.Tests/README.md) | 등록 HTTP·요청 복구·실제 WinForms 제어 검사, 별도 owned MySQL에서 실제 UI 등록/GET·응답 유실/명시 재전송·저장 행 검증 |
| [TaskPreparation.Tests](Tests/TaskPreparation.Tests/README.md) | W1 후보·쓰기 문맥·키/결과 연결, 접속 변경·응답 순서·실제 후보 선택 화면. 별도 명시적 새 소유 MySQL 실행기 제공 |
| [TaskStart.Tests](Tests/TaskStart.Tests/README.md) | 수락·재작업 HTTP/세션·키/결과 복구의 제어 검사와 별도 소유 서버의 실제 HTTP 모드 |
| [TaskStart.Ui.Tests](Tests/TaskStart.Ui.Tests/README.md) | 실제 WinForms·명시 대역의 별도 실행·응답 유실·필터 밖 복구·접속 변경·성공 재조회 |
| [TaskStartPreparation.Tests](Tests/TaskStartPreparation.Tests/) | 수락·재작업 대상의 본인/상태 확인, 실제 WinForms·통제 HTTP와 별도 MySQL/HTTP 읽기 검사. 업무 쓰기·키 발급·결과 조회를 하지 않는 경계 |
| [TaskInputSession.Tests](Tests/TaskInputSession.Tests/) | 등록/수정·연장 입력 확인값의 접속 변경/종료·UI 콜백 전 무효화, 실제 부모 창의 캡처 세션 전달. 명시적 로그인 HTTP 대역·업무 요청0회 검사 |
| [ApiClient.Tests](Tests/ApiClient.Tests/README.md) | 접속 세대·취소·이전 응답·본문 형식별 401 등 C# 인증 회귀 |
| [Collaboration.Tests](Tests/Collaboration.Tests/README.md) | 실제 WinForms 작성·대상 필터·공지·로컬 채팅 회귀 |
| [Notices.Tests](Tests/Notices.Tests/README.md) | 공지 HTTP 대역·비동기 저장·WinForms, 선택적 격리 HTTP/SQLite 실행기 |
| [TaskQueries.Tests](Tests/TaskQueries.Tests/README.md) | 명시적 조회 대역과 실제 업무 화면의 목록·상세·필터·오류·응답 순서 |
| [TaskQueries.Http.Tests](Tests/TaskQueries.Http.Tests/README.md) | 공통 HTTP 어댑터 응답·오류·취소, opt-in 실제 HTTP와 WinForms 조회 |
| `RemoteWork.Server/tests` | 공통 인증·사용자·권한·웹·감사 이력·MySQL 관리자 동시 변경 및 업무 조회 권한. 예약 DDL의 전용 MySQL 검증은 [별도 opt-in 절차](RemoteWork.Server/migrations/002_task_queries.md)를 사용 |
| [RemoteWork.ClientChecks](Tests/README.md) | 별도 테스트 계정으로 실행하는 실제 C#→공통 서버→DB HTTP 점검 |

실행 결과와 사용한 커밋은 [LOG](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/tree/LOG)에 기록합니다. 테스트 대역·로컬 메모리·공통 HTTP 통과를 실제 업무 통합 완료로 표시하지 않습니다. 서버·실제 업무 권한·두 PC·운영 DB의 검증 범위를 각각 구분합니다. 기존 Preview는 이전 화면 검토 자료이며 현재 인증 화면과 같다고 해석하지 않습니다.

2번 조회의 MySQL 검증은 [전용 HTTP 실행기 안내](Tests/TaskQueries.Http.Tests/README.md)를 따릅니다. `run_mysql_http_check.py`는 명시적 실행 옵션과 MySQL 설치 경로를 받아 새 임시 MySQL 프로세스·datadir·DB·합성 계정·loopback API를 준비합니다. 공통7테이블과 검토용002 SQL로 만든 업무 테이블에서 기존 C# 직원·관리자 WinForms의 정확한 조회 집합/정렬·전체/5상태·행 상세·준비 창·접속 폐기를 검사합니다. 지정 관리자 회수·재배정은 fixture 안에서만 수행하며 GET 전후 업무/공통 행의 보존을 대조합니다.

```powershell
dotnet build .\Tests\TaskQueries.Http.Tests\TaskQueries.Http.Tests.csproj -c Release
.\RemoteWork.Server\.venv\Scripts\python.exe .\Tests\TaskQueries.Http.Tests\run_mysql_http_check.py --allow-disposable-mysql --mysql-bin-dir "C:\Program Files\MySQL\MySQL Server 8.2\bin"
```

실제 `.env`·기존 DB 주소·서버 설정을 사용하지 않으며 자신이 생성한 서버·DB·임시 경로만 정리합니다. 이 조회 실행기의 통과는 신규 등록·쓰기 검증이나 정식 migration·전역 스키마 버전 변경·공유 DB·두 PC 완료를 뜻하지 않습니다. 신규 등록 검증은 별도 TaskCreation.Tests로 구분합니다. 이전 SQLite 실행기는 DEMO 참고용으로 남기며 별도 제품 DB 관리 대상으로 확대하지 않습니다.

다음 쓰기 연결에 필요한 [2번 행동별 API·후보 사용자 선택 제안](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#r026)은 당시 제안 원문으로 보존합니다. 현재 개발용 신규 등록의 실제 경로·입력·결과는 [TASK_CREATION](docs/TASK_CREATION.md), 수락·재작업의 실제 개발 계약은 [TASK_START_PREPARATION](docs/TASK_START_PREPARATION.md)을 따릅니다. 일반 수정·재배정·관리자 변경은 [TASK_MANAGEMENT](docs/TASK_MANAGEMENT.md)의 실제 개발 계약을 따릅니다. 취소·연장 [lifecycle 구현](docs/TASK_LIFECYCLE.md)은 별도 기본off/연결 확인을 사용합니다.

[3번 W3 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/3번/README.md)의 날짜별 제안·기술 회신은 수신 기록으로 보존합니다. 현재 수정·재배정·관리자 변경·취소·연장 정책은 팀장 확정 A~E를 따릅니다. 기존3번 제출/검토·파일·이력 코드와 업무 화면의 제출 진입은 존재하며, PR52로 새 lifecycle 취소/대기 연장 차단 소비 코드를 제공하며 같은 배포의 검증·통합 ACK·정식 공유 적용은 별도입니다. 공용 첨부·이벤트·검토자 등 남은 접점은 [최신 공통규약](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md)을 따르고 초기 로컬 시연 조건을 새 서버 규약으로 사용하지 않습니다.

## 2026-10-19 PR 검토 상태

[PR40](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/40) 공통 읽기 점검/공개 바이트 초안/FeatureUiScope와 [PR41](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/41) 독립 감지 서비스/공유 상태 view를 검토 후 alpha7073817/763f11e에 병합했습니다. Python107·C#31, 감지C#294/12·Python30, 결합 Release 경고0/오류0과 검증/병합 tree 일치를 확인했습니다. 기존 기본 진입은 수동 점검이며 실제 MainForm 소유/출퇴근 저장·본인 확인 API/DB·실물/공유/두PC는 후속입니다. [PR42](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/42)는4번24개 통합 회신으로 LOG2e6a439에 수신했고 제안 정책은 협의사항입니다. [상세 검토](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr40-pr41-pr42-review)를 따릅니다.

[웹 공지PR38](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/38)은4번 수정본8b57ed1의 페이지 선택 오류R38-01 해결을 확인해 alpha `8cfdfea`에 병합했습니다. 소유MySQL 원본75검사·이전 실패 재현1검사 통과, 검증/병합 tree 일치를 확인했습니다. [최종 검토](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/PR/4번/2026-10-19_PR38_웹공지_페이지선택_수정요청.md)와 [LOG 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md)을 따릅니다. 아래PR38미병합/실패 설명은 이전 검토 회차의 기록입니다.

[웹 공지 코드PR38](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/38)은2페이지 조회 form의 page 누락으로 다른 공지 상세를 표시하는 R38-01 P2를 재현해 미병합입니다. 소유 MySQL 기존65통과와 독립 회귀1실패를 구분합니다. [4번 수정 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/PR/4번/2026-10-19_PR38_웹공지_페이지선택_수정요청.md)을 따르며 현재alpha의 웹 공지 연결 완료로 표시하지 않습니다. 문서PR39는 LOG `754a6ea` 병합 상태/동일트리를 확인했습니다. 회원/장치 서버·DB/MainForm은1번,Pi/PC 중계·감지/근태는5번의 역할 회신이고 상대 확인·제품 구현은 별도입니다.

잘못 올라온 **PR35는 사용자 지시로 CLOSED·미병합** 처리했습니다. 문서 [PR37](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/37)은 1번의 기술 의견/협의 자료로 LOG `c2e8d95`에 수신·병합했습니다. 닫힌 PR35 요청을 웹 작업·공통 파일 수정 승인으로 사용하지 않으며 후속 유효 범위는 별도 확인합니다. 공개 벡터2개와 추가 상대링크18개를 검증했고 제품 코드/DB 검증을 새로 실행하지 않았습니다. 상세는 [검토 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md)을 따릅니다.

문서 [PR34](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/34)와 [PR36](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/36)은 수정 필수 사항 없이 검토해 LOG에 병합했습니다.1번의 시작 API 기술 수용과5번 장치·근태 협의안 수신을 실제 구현/공동 정책/공유 적용과 구분합니다. 담당별 남은 [협의사항·회신 형식](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#담당별-협의사항과-회신-방법)을 확인합니다.

후속 문서 [PR39](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/39)도 검토 후 LOG에 병합했습니다. 본인 확인 서버·DB·MainForm 연결은1번, Pi/PC 중계·감지·근태 기능은5번의 작성 범위로 정정했으며5번 확인·구체 계약/정책·제품 구현은 별도입니다. UI 콜백 실행 때의 접속/근무 세션 재검사도 협의사항에 보완했습니다.

코드 #21(채팅)·#25(DB 검증)에 이어 [#23(활동 상태 확인)](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/23)도 재검토 후 alpha `5d9602d`로 병합했습니다. 새 head7af259d에서 R23-01~03 해결을 확인했고, 수정된 원본 C#264검증/11시나리오·Python30개·독립 지연 검사·Release 경고/오류0을 직접 확인했습니다. 검증본과 실제 병합 tree도 일치합니다. 관련 문서 #32·#33은 LOG·PR에 수신·병합했습니다. [검토 결과·실제 테스트·미실행](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md)과 [5번 해결 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/PR/5번/2026-10-19_PR23_과거프레임_지연검사_수정요청.md)을 따릅니다. 활동 확인은 사용자가 직접 여는 모달 기능이며, 실제 장비·회원/근태 저장·출근~퇴근 백그라운드 감지와 공유 DB·다른PC는 별도 검증/구현입니다. 등록된 CI 결과 대신 독립 로컬 검증을 기록했습니다. #29의 C09 공지 확장 정책도 계속 제안 수신 단계입니다.

## 브랜치와 게시 기준

| 브랜치·태그 | 목적 |
|---|---|
| `alpha` | 현재 통합 개발 코드·화면 |
| `LOG` | 담당별 구현·검토·병합 이력 |
| `PR` | 담당별 수정 요청·회신·재검토 결과 |
| `DOCX` | 기획·설계·합의 문서 |
| `0.0.1V` / `0.0.2V` | 각각 초기 코드 배치·기존 Designer 화면/조회 준비 버전 보존 |

코드 브랜치의 루트 `DOCX/`는 `.gitignore`의 `/DOCX/`로 제외합니다. 로컬 문서는 유지하고 DOCX 브랜치는 별도 요청 시 갱신합니다. 실제 `.env`·비밀번호·토큰·DB·가상환경·bin·obj·임시 검증 결과는 게시하지 않습니다. 푸시할 때마다 해당 브랜치 README를 실제 구현·파일 위치·검증 결과에 맞춰 최신화합니다.

다른 담당이 업무·상태·마감 영역을 수정하거나 기능을 연결할 때는 [2번 영역 수정 협업 규약](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/2026-10-17_2번_영역수정_협업규약.md)을 따릅니다. 담당별 파일·공유 접점, 유지할 조회 계약, 공동 협의가 필요한 변경, 검증 범위와 PR 전달 양식을 정리했습니다. 기존 계약을 유지하는 수정과 API·권한·상태·DB 변경을 구분하며, 미확정 제출/검토·협업·장치 규칙은 관련 담당과 먼저 맞춥니다.

## 4번 채팅 저장·자동 갱신 — PR #21

로그인 사용자는 서버 DB에 대화방·참여자·메시지를 저장하고 재로그인 후 조회합니다. 삭제는 본인 목록 숨김이며 상대 기록·메시지·감사를 자동 삭제하지 않습니다. 채팅 화면이 보이는 동안 기존 API를 약 1초마다 조회하여 새 방과 선택한 방의 메시지를 자동 표시합니다. 초안·선택·스크롤을 보존하며 화면 숨김·로그아웃 시 조회를 중지합니다. WebSocket/SSE 방식은 아닙니다.

1번의 [기술 회신](https://github.com/jinkyu1452-cmd/Sang1/blob/550872d3bf973090b17a4801456fedacf30ada87/1번/2026-10-19_협업요청_기술검토_회신.md)에 따라 API·권한·UUID 재시도·개인 숨김·기본 off를 유지합니다. 검토된 [005 개발 DDL](RemoteWork.Server/migrations/005_chat_storage.md)을 동일 내용으로 포함하고 기존 번호 없는 SQL을 대체했습니다. ORM 생성도 InnoDB/utf8mb4/utf8mb4_unicode_ci를 명시합니다. 공통 schema version은 1을 유지합니다. 회신과 PR #25/#26의 원본 병합·공유 DB 적용 승인은 별개입니다.

공통 DB 초기화는 chat off의 새 프로세스에서 진행합니다. 새 개발 DB의 채팅 테이블 준비는 검토된 SQL 또는 Init-Chat.cmd 중 한 방법을 사용합니다. 기존 테이블에 자동 변환·삭제를 하지 않습니다. CLI의 열 이름 검사는 물리 스키마 검증을 대신하지 않습니다. 서버 활성화는 REMOTEWORK_ENABLE_CHAT_DRAFT=1로 별도 지정합니다. Init-Chat의 환경 값은 호출자에게 남지 않습니다.

[채팅 실행·정책](RemoteWork.Server/remotework/features/chat/README.md), [자동 갱신 코드·제한](RemoteWork.WinForms/Features/Collaboration/CHAT_AUTO_RECEIVE.md), [재현 테스트](Tests/ChatAutoReceive.Tests/README.md)를 확인합니다. 물리 두 PC·공유 DB·운영 배포·장시간 부하와 전역 알림은 미검증/범위 밖이며, 운영 적용자·백업/복구·보존 정책은 팀장과 별도로 확정해야 합니다.


## 3번 결과물 개발 구현 — PR #46

직원·관리자 본인 제출과 관리자 검토·업무 이력을 기존 4개 폼과 공통 인증/쓰기 지원에 연결합니다. 첫 성공 제출 1차, 성공 재제출만 +1차이며 업무상 5차 제한은 없습니다. 실제 파일 바이트와 이전 제출물을 보존합니다. 기본 off이며 006은 개발 검토안입니다. 실행/권한/범위와 미연결은 [결과물 안내](docs/RESULTS.md), 직접 검사와 미실행은 [검증 안내](Tests/Results.Http.Tests/README.md)를 확인합니다. 후속 웹 구현은 같은 저장 기능을 사용하는 직원/관리자 결과물·제출/검토·관리자 업무 이력 화면을 제공합니다. 최종 조직/취소·연장 소비 연결과 공유 환경 검증은 구분합니다.

<details>
<summary>과거 PR 검토 요약</summary>

아래는 각 검토 당시 기록입니다. PR45는 이후 팀장 삭제 정책 확정과 재검증을 거쳐 alpha에 병합됐습니다. 현재 상태는 [LOG 검토 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr45-merged-delete-policy-20261020)을 따릅니다.

## PR45 최신 추가분 — 삭제 정책 문서 정정·Draft 유지, 2026-10-20

[PR45](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/45) 검토 마무리 중 head `c5dad3b`이 추가됐습니다. Markdown3개·SQL/시험 주석만 바뀌었고 실행 로직·DDL은 이전 검증본 `0dfa517`과 동일합니다. **4번 문서는 로컬 선택을 “공지 삭제 시 확인 기록도 함께 삭제”로 정정**했으므로, 아래 “보존 요구와 CASCADE 불일치”는 이전 요구 시점 기록입니다. 이 보고를 팀장의 공통 삭제 정책·최종008/공유 적용 승인으로 확대하지 않습니다. 최신 head는 충돌 없이 결합되며 **Draft·미병합을 유지**합니다.

[최신 검토 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr45-policy-followup-20261020). PR45 제품은 미병합이며 이번 alpha 갱신은 상태 README만 추가합니다.

## PR45 후속 수정 확인 — 충돌·PID 경합 해결 / Draft 유지, 2026-10-20

[PR45](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/45) head `0dfa517`에서 최신 alpha `14c1c80` 반영과 병합 충돌 해소, 공식 Activity 회귀의 PID 파일 읽기 보완을 확인했습니다. 공식 원본 전체 **297검사/12시나리오를2회 통과**, 공지35검사·Release 경고0/오류0입니다. R45-01·02 해결도 유지됩니다. **공지 삭제 후 확인 기록 보존 구조는 여전히 미구현이므로 Draft·미병합·008공유 적용 보류**입니다. 아래 과거 “alpha 충돌/공식 Activity PID 경합 실패”는 해당 head 시점 이력이며 최신 해결 상태와 구분합니다.

[검토 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr45-pid-recheck-20261020). PR45 제품은 미병합이며 이번 alpha 갱신은 이 상태 기록만 추가합니다.

## 최신 재검토 — PR44·46·47 병합 / PR45 Draft 유지, 2026-10-20

[PR44](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/44) R44-01과 [PR45](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/45) R45-01·02의 수정 업로드를 확인했습니다. **44번은 수정 재검증 후 병합, 45번은 기존 지적 해결·보존 구현 미완료로 Draft/미병합**입니다. 팀장의 이번 “46번은 수정됐으니 검토” 지시로 이전 보류를 해제하여 [PR46](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/46) 실제 제출·검토 코드를 검토했고, 신규 [PR47](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/47) 등록 중계도 검토했습니다. 44→47→46 순서로 alpha에 병합했으며 최종 코드 merge `1d2cb6b`/tree `0608974b29eb5996f67d34c14b63ae8fbf4fa51f`는 검증본과 일치합니다. 아래 이전 미해결·46보류·미병합은 작성 시점의 이력입니다.

[검토 결과·남은 연결·실행/미실행 범위](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr44-47-recheck-20261020). 코드 검증 이후 이 README 상태 기록만 추가했으며 제품 트리는 위 최종 코드 merge와 같습니다.

</details>
