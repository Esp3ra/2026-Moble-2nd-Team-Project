# 010 지문 등록의 MySQL 물리 작업 증거

1번 공통 DB/서버,2026-10-20. [SQL](010_enrollment_durability.mysql.sql) · [API·5번 연결/이관·복구 경계](../../docs/ENROLLMENT_MYSQL_DURABILITY.md).

007의 기존4테이블을 보존하고 독립metadata의 `enrollment_physical_receipts`1개만 추가한다. 기존테이블 ALTER/DROP·전역version상승·회원/지문seed·예약해제를 하지 않는다. 공통 실제/요구schema는1이며010이라는파일번호가version10승인이아니다. MySQL8.0.16+의InnoDB·utf8mb4_unicode_ci를 사용한다.

- PK enrollment_id,007의 `(enrollment_id,device_id,reserved_slot)`에 복합FK로 같은 작업·장치·예약칸을 결합한다. 사용자/장치·nonce/bearer/키·지문원본을 중복 저장하지 않는다. 삭제CASCADE없음.
- operation/claim/key revision은signed BIGINT,slot은signedINTEGER. intentnonce는SHA256만 저장한다. intent시각/hash와 결과BLOB/hash/ID/상태/시각의 짝을CHECK로 제한한다.
- signed result원문은BLOB 최대16384바이트,CHECK/서버파서양쪽검사. result_id는UNIQUE다. sensor_stored의failure_code는NULL,실패/확인필요는명시코드가필요하다. 결과조회는hash/MAC/필드바인딩을재검증한다.
- 새기능은독립명시flag 기본off. 기존007init/일반init-db와HTTP는이테이블을자동생성하지않는다. `require_storage`는경로의제한된readiness이며전체DDL·FK/인덱스/CHECK·백업복원의검토를대신하지않는다.

적용 전 정확한개발/대상DB·007의물리컬럼/타입/복합UNIQUE·기존자료/미해결작업과백업을확인한다. 이미테이블이있다면열이름만보고CREATE/IF NOT EXISTS로덮어맞추지않는다. MySQL DDL은암묵commit이므로실패시상태확인후중단한다. 기존복구/센서기록을지워서맞추지않는다.

새소유MySQL에서공통/007후010을적용하고,첫store intent commit/응답유실·동일probe재전송·결과원문보관·늦은complete차단·FK/CHECK/UNIQUE를대조한다. 비공개옵션파일/hex-blob dump로백업하고빈새DB에복원해동일원문SHA256·PK/FK/DDL/schema1·기존매핑보존을확인한다. 실제사용자DB를pytest에지정하지않는다.

기존SQLite이관/기존작업의예약재사용은단순DDL적용으로끝나지않는다.5번코어/PC중계연결과새inventory·기존writer차단·현재관리자/접속/claim정책의운영확인후별도로수행하며원본병합·실제배포·장치전환/실물검증을구분한다.
