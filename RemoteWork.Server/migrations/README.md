# DB 변경 기록

현재 공통 스키마 버전은 1입니다. 최초 테이블 생성은 `flask --app remotework init-db` 또는 `run.py --init`으로 수행합니다. 공통 모델 정의는 `remotework/models.py`입니다.

추가 기능의 모델은 등록한 Blueprint에서 import하고 최초 `init-db` 전에 로드합니다. 이미 배포한 테이블을 변경할 때는 이 폴더에 `002_설명.mysql.sql`처럼 번호를 붙인 변경 SQL을 추가하고 테스트 DB 검증·공유 DB 백업 후 적용하세요. 적용 결과와 버전 증가를 기록합니다. 공통 `init-db`가 기존 열을 자동 이관한다고 가정하지 마세요.
