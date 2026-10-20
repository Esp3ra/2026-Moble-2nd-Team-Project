# 업무 수정·재배정·관리자 변경의 실제 HTTP/WinForms 검사

실제 HTTP/WinForms 통합 흐름은 로그인, 업무 등록, 관리 변경 대화상자, 수락, 결과물 제출·반려·승인 API를 사용하며 업무·배정·제출 상태를 직접 삽입하거나 바꾸지 않습니다. 별도 서버 경계 pytest는 오류·손상·동시성 재현을 위해 명시적 상태 fixture와 대역을 포함합니다. 두 모드 모두 새로 만든 전용 MySQL 인스턴스와 임의 계정만 사용하며 기존 서버·DB·`.env`·SQLite는 사용하지 않습니다.

```powershell
dotnet build Tests/TaskManagement.Http.Tests/TaskManagement.Http.Tests.csproj -c Release
<Flask/SQLAlchemy/PyMySQL이 설치된 Python> Tests/TaskManagement.Http.Tests/run_mysql_check.py --allow-disposable-mysql --mysql-bin-dir '<MySQL bin 절대 경로>'
# 서버의 경계·동시성·원자성 pytest는 별도의 새 전용 DB에서 실행
<Flask/SQLAlchemy/PyMySQL/pytest가 설치된 Python> Tests/TaskManagement.Http.Tests/run_mysql_check.py --allow-disposable-mysql --mysql-bin-dir '<MySQL bin 절대 경로>' --server-pytest-only
```

공통 원시 DDL과 `002`·`004`·`006`을 새 빈 데이터베이스에 적용합니다. 전용 fixture의 검토자 개발 CLI는 명시적으로 설정합니다. 개발 플래그는 그 자식 서버에서만 켭니다. 비밀번호와 토큰은 임시 런타임·환경 및 자식 표준입력에만 보관하고 출력하지 않습니다. 자식 프로세스와 MySQL은 소유권을 확인한 뒤 종료하고 임시 런타임을 정리합니다.

범위: 지정 관리자 권한과 이전 관계 권한 회수, 진행전 일반 수정·재배정, 배정 차수와 이전 배정 보존, 관리자 인계·등록자 제외·관리자 최소 1명, 진행중·반려완료 인계, 제출완료·승인완료 차단, 버전 충돌·무변경 차단, 실제 과거 제출·첨부·검토자·최초 수락 시각·감사·요청 결과 보존. C# 대화상자 검사는 화면 밖의 실제 WinForms 컨트롤을 자동 조작합니다. 사람이 화면을 확인한 수동 검수·공유 계정·두 PC 검증을 뜻하지 않습니다.
