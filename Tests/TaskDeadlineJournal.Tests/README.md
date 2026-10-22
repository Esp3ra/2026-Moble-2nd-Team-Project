# 2번 실제 업무 변경·기한 사건 연결 검증

```powershell
& <검증용 Python> Tests/TaskDeadlineJournal.Tests/run_mysql_check.py --allow-disposable-mysql --mysql-bin-dir 'C:/Program Files/MySQL/MySQL Server 8.2/bin' --report-dir <소스 밖 새 보고 폴더>
```

Windows의 명시적으로 소유한 새 MySQL8.2를 만들고 원본 raw002/004/006/009/013과 PR70의 번호 없는 journal/confirmation SQL을 한 번 준비합니다. 제품 startup의 자동 DDL·공유 DB/.env·SQLite·운영 ACK를 사용하지 않습니다. 실제 공통 인증·W1·2번 등록/수정/연장 승인과 공개 첨부 API를 거쳐 원래 mutation 안에서 호출된 `record_change`를 검사합니다. 예전 `test_deadline_event_writers.py`의 audit-boundary record 주입을 사용하지 않습니다.

- 새 등록의 최초 실제 사건, 일반수정 A→B→A, 승인 연장의 실제 변경과 retained replay 한 효과.
- 제목/본문/시작일·동일 기한과 실제 업로드한 첨부만 변경, 수락/신청/반려/철회/취소 등 비기한 동작의 사건 보존.
- 웹 등록·수정은 확인 화면 준비만으로 쓰지 않고 명시 실행에서 같은 사건을 기록하며, 재실행·결과 확인은 효과를 반복하지 않음.
- 재배정 A→B→A와 관리자 변경에서 원 사건 보존, 현재 담당자의 배정별 target·권한·원 버전/마감일 확인.
- 실제 제출→반려→재작업→재제출→승인에 따른 원 상태·버전·수락 시각/제출 차수 보존 및 제출 중 신규 마감 확인 차단.
- 실제 사건 INSERT 뒤 기록 오류, 후속 감사 오류, 실제 receipt INSERT 뒤 오류가 원 업무·사건/head·감사/receipt를 모두 되돌리는지 확인.
- 미연결 기존 업무의 기한 변경503·baseline 자동 생성 없음, 기능OFF/독립 결과읽기의 기존 쓰기 보존(번호 없는 draft 테이블 3개가 물리적으로 없어도 확인), 설정과 설치 불일치503.
- 실제 제품 생성 사건을 새 마감 HTTP 목록/확인/W1 결과 읽기로 소비하며 업무 버전과 배정/사건을 보존하는지 확인.

검사는 합성 회원·검토자와 소유 TESTING 앱에만 통합 ACK를 명시합니다. Flask test client→실제 제품 처리부→실제 MySQL/파일 저장소 검사이며 TCP/브라우저/WinForms·공유 서버/두 PC·4번 source 연결 완료 증거는 아닙니다. 실패 주입은 원 함수의 실제 저장 뒤 예외만 추가하며 제공부 성공을 대신하는 대역이 아닙니다. 원문/body/키·로그인 수명·정책을 변경하지 않습니다.

보고서는 `verification.json`에 안전한 pytest 요약·물리DDL/schema1 불변·소유 MySQL/DB/runtime 정리 결과를 남깁니다. 진단용 `--select <pytest -k 식>`은 선택 검사만 실행하므로 전체 검사 통과로 기록하지 않습니다. `private-failure.txt`는 합성 시험의 로컬 진단 자료이며 공개 Git에 올리지 않습니다. 기존 자료의 baseline·최종 확인 정책·번호 예약/공유 적용은 별도 협의사항입니다.

2026-10-22 전체 `actual` 검사 32건을 실제 소유 MySQL 8.2에서 통과했습니다. 물리 DDL과 `schema_versions=[1]`은 실행 전후 동일하며 소유 인스턴스·DB·런타임 삭제를 확인했습니다.

기존 PR70 회귀는 `--suite legacy-writers`(7건), `--suite legacy-http`(21건)로 각각 별도의 새 보고 폴더와 소유 DB에서 실행합니다. 2026-10-22 두 모드 모두 전체 통과했고 schema1·소유 자원 정리를 확인했습니다. 이 모드는 기존 시험의 module fixture가 DDL을 직접 준비하므로 runner는 raw SQL을 중복 적용하지 않습니다. `legacy-writers`는 원래의 audit 경계 record 주입을 보존한 호환 검사이며 실제 제품 연결의 근거는 위 `actual` 32건입니다. legacy 모드는 실행 전후 DDL 불변을 주장하지 않고 fixture 준비 사실과 최종 schema1·정리 여부를 기록합니다.
