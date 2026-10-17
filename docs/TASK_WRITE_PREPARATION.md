# 2번 W1 후보·쓰기 준비 연결

2026-10-17. **팀장과 2번 담당자는 사용자 본인**입니다. 정책 확인이 필요하면 사용자에게 확인합니다.

## 완료한 순서

1. `Preparation/TaskPreparationModels.cs`에 후보·페이지·쓰기 문맥·키/결과 모델과 별도 서비스 계약을 추가했습니다. 기존 업무 GET DTO와 CheckedInput은 유지합니다.
2. `HttpTaskPreparationService`는 공통 ApiClient.Session에 묶여 후보·write-context·키 발급/결과 조회만 호출합니다. 계정 전환·같은 사용자 재로그인·서버 변경 후 옛 창이 새 접속으로 요청하지 않습니다.
3. HttpTaskQueryService의 선택적 ITaskPreparationSource를 TasksControl이 사용하므로 MainForm·공통 파일·기존 ITaskQueryService는 변경하지 않았습니다. 미리보기/기본 조회 대역에는 자동 주입하지 않습니다.
4. 업무 화면 ‘업무 연결 정보’에서 성공한 현재 상세의 ID로 준비 정보를 조회합니다. 등록/수정 창의 ‘담당자 · 업무 관리자 선택’으로 검색·페이지·담당자 단일/관리자 복수 선택·해제를 제공합니다. 이름 대신 서버 ID로 보관합니다.
5. 페이지/검색 중 선택 유지, 로딩/실패 중 선택 확정 차단, 빈 결과 구분, 다시 조회 시 초기화, 권한 회수/로그인 변경 시 정보 삭제와 늦은 응답 차단을 구현했습니다.

## 저장과의 경계

후보/문맥 조회나 선택값 가져오기는 최종 저장 권한·상태별 허용이 아닙니다. 업무 관리자는 제출 검토자와 별개입니다. 폼의 별도 PreparedSelection은 선택값이며 기존 CheckedInput은 로컬 제목/내용/날짜 확인값입니다. 후보 선택 변경은 이전 CheckedInput을 무효화합니다. 실제 등록·재배정·수락·연장 접수/상태 변경은 실행하지 않습니다.

기존 상세 GET와 별도 write-context가 동일 트랜잭션의 스냅샷임을 보증하지 않습니다. PreparedSelection과 CheckedInput을 곧바로 완성된 쓰기 DTO로 조합하지 않습니다. 실제 행동 연결 시 입력 원본과 버전의 일관된 읽기·상태별 검사·현재 권한/배정/버전 재검사를 서버 계약으로 맞춰야 합니다.

| 서비스 | 용도 |
|---|---|
| GetCandidatesAsync | assignment/manager-candidates · 검색·페이지·필수 필드·중복 ID 검사 |
| GetContextAsync | 업무/버전 BIGINT 문자열·담당자/관리자 INTEGER·배정 차수·5상태·UTC 검사 |
| GetRequestKeyAsync | 확정된 W1 행동/대상의 서버 키 발급. 원문은 메모리 보관, ToString에 노출하지 않음 |
| GetResultAsync | 같은 키로 completed/not_recorded 조회. 자동 재전송·새 키 생성 없음 |

401·403·업무 전용404/일반404·버전/배정 충돌·키 재사용/만료·미준비503을 구분합니다. 준비 창은 키 발급/결과 조회도 자동 호출하지 않습니다. 제품 행동 API를 호출하는 메서드는 추가하지 않았습니다.

## 검증과 개발 환경

[TaskPreparation.Tests](../Tests/TaskPreparation.Tests/README.md)는 제어 HTTP·실제 WinForms와 새 소유 MySQL의 실제 HTTP/UI 검증을 분리합니다. 최소 크기 렌더링도 확인했으며 수동 전체 GUI 검수를 대신하지 않습니다.

명시적 개발 서버와 삭제 가능한 전용 MySQL에 검토용002/004·공통 테이블을 준비하고 `REMOTEWORK_ENABLE_COMMON_WRITE_DRAFT=1`, `REMOTEWORK_ENABLE_TASK_QUERY_DRAFT=1`을 사용합니다. 조회 중 DDL이나 공유 DB 변경을 하지 않으며 공통 version1·기본off를 유지합니다. [W1 계약](WRITE_SUPPORT.md)을 따릅니다.

## 다음 합의와 구현

- W3-01의 초기 저장 단계/값·수락 예외를 확인한 행동부터 등록/수락을 연결합니다.
- W3-02/03/04의 상태별 수정·재배정·취소, 제출/판단 원자성·버전 증가·배정/제출 참조·이력 계약을 맞춥니다.
- W3-05/06/07의 검토자·마감/연장/제출 경합·수동/자동 검토 요청은 사용자(팀장)·관련 담당과 확인합니다. 자동 결과 제출은 제외합니다.

실제 업무 쓰기 API/UI 저장·3번 결과/파일/회차/승인·반려·공유DDL/버전/복구·두PC·웹 업무·장치 인증은 이번 범위 밖입니다. 3번 업로드나 문서 게시를 공동 정책 확정으로 간주하지 않습니다.
