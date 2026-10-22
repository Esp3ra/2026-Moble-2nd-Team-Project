# 015 공지·채팅 첨부 물리 검사

Windows/MySQL8.2와 서버 검증 Python에서 실행한다. 사용 DB/.env/운영 ACK를 사용하지 않고, 새 소유 인스턴스·DB·파일만 만들며 마지막에 정리한다.

```powershell
python Tests/ResourceAttachmentSchema.Tests/verify_mysql.py
```

SQL 문자열/괄호 회귀만 DB 없이 확인하려면 다음을 사용한다. 전체 MySQL 실행도 같은 표현식 회귀를 먼저 확인한다.

```powershell
python Tests/ResourceAttachmentSchema.Tests/verify_mysql.py --expression-only
```

3번 고정 PR76 제공부의 기존 readiness 차이를 재현하려면 `--peer-source <고정 checkout의 RemoteWork.Server/remotework/features/shared_resource_attachments.py>`를 추가한다. 고정 a6dc89c 소스를 별도 이름으로 읽어 원 코드 수정 없이, notice FK가 ON DELETE CASCADE여도 기존 검사가 True를 반환하는 상태를 재현한다. 새 공통 검사는 동일 잘못된 구조를503으로 거절한다. 기존 제공부의 실제 공유 DB가 잘못됐다는 주장이 아니다.

검사 범위: 정상015·선택적 완전부재/부분부재·PK·길이/null/default·필수 FK 누락/잘못된 UPDATE 동작·삭제 CASCADE·CHECK 누락/변조/미강제·반복검사. MySQL 자체가 CHECK 열의 CASCADE를 금지하는 경우는 잘못된 fixture를 만들지 않는다. 빈 합성 참조 테이블과 schema1 한 행의 실제 mysqldump→다른 새 소유 DB 복원 후 DDL/전행동일·readiness를 검사한다. 실제 파일 바이트/운영 데이터 복원이나4번 권한·화면 검증은 아니다. 선택한 peer의 원 SQL과015 선언 일치도 대조한다.

2026-10-22 R77-01 회귀는 SQL 구문의 대소문자·공백·허용된 quoted identifier/문자셋 표기와 문자열 리터럴의 실제 내용을 구분한다. DB 없는 **29개 표현식 검사**에서 정상015·MySQL `_utf8mb4` 표기·중복 괄호를 허용하고, notice/chat 리터럴의 대문자·내부 공백/backtick/`_utf8mb4`·escape/인접 리터럴·다른 AND/OR 묶음을 거절한다. 원본57f8c26의 parser에 같은 회귀를 실행하면 `HEX('NOTICE')` 변형을 정상으로 받아 실패하며, 수정 후 전체 통과했다.

고정 PR76 peer와 실행한 실제 소유 MySQL8.2의 **49개 물리 검사**도 전체 통과했다. 기존32개를 포함하며 표현식29개나 이전 반복 실행 수치와 합산하지 않는다. notice/chat의 대문자·내부 공백/backtick/`_utf8mb4` CHECK 변형8개를503으로 거절하고, 정상 notice/null INSERT는 성공하지만 `NOTICE`/`not ice` 변형은 실제 MySQL3819와 저장0행이 되는 차이를 확인한다. 정상015로 복원한 뒤 기존 dump/독립 복원·DDL/전행/readiness 대조를 유지한다. 사용한 MySQL·DB·native 프로세스·runtime은 소유 실행기의 정리 검사를 통과했다. 공유 DB의 실제 구조 오류나 적용/재적용을 주장하지 않는다.

기존 제품 auth/resource 콜백·파일 기능을 자동 등록/수정하지 않는다. 3번의 정상 제공부108개 HTTP 검사와 이 물리 검사를 합산하지 않는다.
