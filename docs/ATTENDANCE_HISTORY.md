# 실제 근태 이력과 이전 근무일 퇴근 누락

1번 서버 제공 / 5번 WinForms 소비. 기준 원본 alpha `49e3b8d84ee1689dc187f936c384fc654690eec2`.
관련 요청: [PR58 5번 대표 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/58#issuecomment-6355530334).
이 문서의 새 계약은 이번 코드 변경이며, 공유 DB 설치·실물/두 PC 검증 완료를 의미하지 않는다.

## 정책과 저장

- `REMOTEWORK_MISSED_CLOCKOUT_POLICY=absence-v1`을 명시한 지문 인증 서버에서만 새 분류를 수행한다. 빈 값은 기존 수동 확인 정책이다. 알 수 없는 값은 지문 서버 시작을 거절한다.
- 먼저 원본 DB·외부 첨부·비공개 설정을 백업하고 별도 MySQL에 복원 검증한 뒤 `migrations/018_attendance_exceptions.mysql.sql`을 명시 적용한다. `init-db`, 요청, 서버 시작이 이 DDL을 자동 실행하지 않는다. 기존 identity_v2 저장소가 선행되어야 한다. `schema_versions`를 임의 갱신하지 않는다.
- 오늘은 서버 UTC+09:00(KST) 달력 날짜다. **과거 날짜이며 `ended_at IS NULL`인 본인 실제 근무만** `absent` / `missed_clock_out`으로 별도 원장에 기록한다. 출근·정오·퇴근 원본 행, UUID, 원 결과/영수증·장치 Claim·capture fence를 삭제하거나 변경하지 않는다. 가짜 퇴근 시각도 넣지 않는다.
- 같은 guard→현재 사용자→근무 잠금/TX에서 예외 원장과 `attendance.missed_clock_out` 감사가 함께 저장된다. 근무 UUID가 원장 PK라 재시도는 중복 기록하지 않는다. 여러 과거 행을 함께 처리하며 실패하면 전부 rollback한다.
- 올바른 활성 계정의 일반/웹 비밀번호 로그인 또는 지문 대기 로그인에서 수행한다. **GET에서는 수행하지 않는다.** 일반 로그인은 분류만 commit한 후 `409 IDENTITY_FINGERPRINT_REQUIRED`를 반환할 수 있다. 이는 정상 토큰 발급/오늘 출근 성공이 아니다. 오늘 첫 출근은 새 지문 인증 후 기존 finalize로만 저장한다.
- 이전 KST 날짜에 시작한 미완료 `issued/verified` 인증이 아직 유효하면 `409 ATTENDANCE_PREVIOUS_OPERATION_PENDING`: 먼저 원 UUID 결과를 복구하거나 만료를 기다린다. 이 변경은 인증 상태/결과를 자동 폐기하거나 재촬영 허가하지 않는다. 기존 완료 결과 재조회·재전송 계약도 유지한다.
- 오늘 열린 근무는 같은 날 재로그인 가능, 오늘 퇴근 후 일반 접속도 가능하다. 퇴근을 다시 출근으로 바꾸지 않는다. 미래 날짜의 열린 근무는 계속 운영 확인 대상이다. 야간근무/근무일 경계/결근의 인사·급여 처리 확장 규칙은 별도 합의 대상이다.
- 쓰기 설정을 다시 비워도 이미 기록한 분류는 읽고 존중한다. 원장만 삭제해서 되돌리지 않는다. 사후 수동 정정은 원본과 예외의 일관성을 함께 처리할 별도 운영 절차가 필요하다. 이번에는 정정/삭제 API를 추가하지 않았다.

## 5번이 연결할 조회 계약

`GET /api/v1/attendance/records`, 기존 정상 `Authorization: Bearer ...` 사용. IdentityPending은 조회 권한이 없다.
지문 기능을 설치한 앱에 등록되며 018이 없거나 불완전하면 `503 IDENTITY_STORAGE_NOT_READY`이다.

| 인자 | 기준 |
|---|---|
| `scope` | 기본 `self`. 직원은 본인만. **현재 활성 관리자만 `all`** 가능. 부서 범위 관계는 새로 가정하지 않는다. |
| `user_id` | `self`이면 본인 ID만. 관리자의 `all`에서는 선택한 사용자 ID로 제한 가능. 생략 시 전체. 양의 정수. |
| `from_date`, `to_date` | `YYYY-MM-DD`, 양끝 포함. 기본 KST 오늘-30일~오늘. 최대 366일. 잘못된 날짜/역전/과도 범위는 400. |
| `limit` | 기본 50, 1~100. |
| `cursor` | 직전 응답 `next_cursor`의 근무 UUID. 같은 권한·필터로 다음 페이지 요청. 조회 범위에 없는 UUID는 400. |

정렬은 `work_date DESC, started_at DESC, id DESC`의 keyset이다. 필터와 권한은 **매 요청 재검사**한다.
관리자 권한이 바뀌면 다음 전체 페이지도 거절된다. 실시간 원장이므로 여러 페이지를 하나의 과거 스냅샷으로 보장하지 않는다.
알 수 없는 인자/중복 인자/잘못된 cursor도 400. 직원의 전체·다른 사람 조회는 403, 인증 만료·비활성은 401.
기존 공통 `ok/data/request_id`와 오류 envelope를 유지한다.

```json
{
  "ok": true,
  "data": {
    "records": [{
      "work_session_ref": "20000000-0000-4000-8000-000000000001",
      "user_id": 101,
      "display_name": "예시 직원",
      "department": "예시 부서",
      "work_date": "2026-10-23",
      "state": "absent",
      "reason_code": "missed_clock_out",
      "started_at": "2026-10-23T00:00:00Z",
      "noon_at": null,
      "ended_at": null,
      "recorded_at": "2026-10-26T00:30:00Z"
    }],
    "scope": "self", "user_id": 101,
    "from_date": "2026-09-26", "to_date": "2026-10-26",
    "server_time": "2026-10-26T00:30:00Z", "next_cursor": null
  },
  "request_id": "example-only"
}
```

위 값은 합성 예시다. `started_at/noon_at/ended_at/recorded_at/server_time`은 UTC `Z`, 표시할 때 KST로 변환한다.
실제 출근 행이므로 `started_at`은 필수이며 나머지 세 사건 시각은 nullable이다.

| state | 표시·의미 |
|---|---|
| `open` | 오늘 출근 중, 퇴근 미저장 |
| `closed` | 실제 또는 별도 명시 수동 정정으로 퇴근 시각이 저장됨 |
| `absent` | 분류 원장이 실제 저장됨. `missed_clock_out`은 ‘퇴근 누락’, 퇴근 시각은 null 유지 |
| `review_required` | 과거/미래 미종료지만 분류되지 않은 기록. 조회로 결근 분류를 만들지 않음 |

5번은 `AttendanceControl.InitializeSampleData()`를 실제 API 소비로 바꾸되 기존 폼·도구상자/접속 수명·취소/인증 만료 처리를 유지한다.
빈 배열은 조회 기간에 실제 기록 없음이다. nullable 퇴근을 `00:00`으로 변환하거나 모든 미종료를 결근으로 추정하지 않는다.
현재 로그인한 직원 화면은 기본 self, 관리자 화면은 all을 선택하여 연결한다. 새 C# DTO 이름은 5번 소유이며 기존 wire DTO를 강제로 바꾸지 않는다.

## 검증·배포 경계

소유 독립 MySQL에서 raw018, 여러 과거 행/오늘·미래 보호/중복·동시 요청/감사 실패 rollback/유효한 과거 작업 대기/KST 경계/이전 결과의 오늘 finalize 거절/직원·관리자 권한/페이지와 입력 검사를 실행한다.
기존 지문·당일 퇴근 후 재로그인·호스트 검사를 함께 수행한다. 실제 실행 수치는 PR과 통합 회신을 기준으로 한다.

Windows에서 별도 검사 Python에 requirements-dev.txt를 설치한 뒤 다음 기존 소유 MySQL 실행기를 사용한다.
운영 .env를 로드하지 않으며 직접 운영 DB URL을 테스트에 넣지 않는다.

```powershell
& '<검사 venv>\Scripts\python.exe' Tests/IdentityHost.Tests/run_check.py `
  --allow-disposable-mysql --mysql-bin-dir '<MySQL 8 bin 절대 경로>' --include-attendance
```

공유 환경은 백업 복원 검증→Pi의 현재 서비스 정상 정지/작업 drain→기존 5000 정지→새 정지 상태 백업 재검증→018 명시 적용→기존 기능/키 보존한 코드·설정 적용→같은 5000 시작→현재 API 검사→변경된 서버3소스 핀/관측을 5번에게 전달 순서다.
5번의 이미 설치된 **영구 자동 재시작 정책은 보존**하고 계획된 서버 소스 변경만 기존 재연결 도구로 조율한다. 일반 재부팅마다 옛 일회성 ZIP을 다시 요구하지 않는다.
공유 환경에서 과거 기록은 승인된 정책의 로그인에서 점진적으로 분류하며, 전체 계정 일괄 backfill은 하지 않는다.
실제 기존 admin 수동 정정과 다른 과거 원본을 새 결근으로 덮어쓰지 않는다.

이번 변경은 5번 UI 구현, 실제 지문/두 PC 시험, 인사 규정 확정, 공유 서버 설치 자체를 완료했다는 근거가 아니다.
실제 비밀번호·토큰·장치 키/receipt·개인 식별자료는 이 문서/PR에 넣지 않는다.
