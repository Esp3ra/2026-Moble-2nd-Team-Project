# 연차 입력·목록·검토 UI 회귀

```powershell
dotnet run --project Tests/Leave.Ui.Tests -c Release
```

실제 WinForms 메시지 루프와 합성 로그인/내부 ILeaveWorkflow 대역을 사용한다. 5개 신청 유형과 병가·공가 차감 제외 안내, 미연결 시 저장 완료 금지, 기간 검사, 중복 클릭/응답 유실의 요청 고정, 다른 신청의 영수증 거부, 지정 직원의 관리자 검토 제한, 접속 종료 뒤 늦은 결과를 확인한다. 실제 HTTP 휴가 계약·DB 정책을 만든 시험이 아니며 실제 회원/서버/DB/장치를 사용하지 않는다.

출력 폴더를 인자로 넘기면 신청·검토 폼 PNG를 생성한다. 폼 컨트롤은 제품의 Designer.cs/resx에서 관리한다. 서버 adapter의 담당 범위 권한·실제 차감·동시성과 API 협의는 별도다.

2026-10-22 화면 보완 후 **76검사**(기존57 포함)다. 신청 유형/기간·사유 글자수의 즉시 요약, 검토 차감 안내, 본인/지정 목록 안의 상태 필터, 필터가 새 조회를 하지 않는 것, 선택 상태 초기화, 로그아웃 뒤 필터의 이전 자료 복구 금지, 조회 실패를 빈 정상 응답으로 바꾸지 않는 것을 추가 확인한다. 독립 [화면 체험 도구](../../Tools/LeavePreview/README.md)의9검사와 구분한다.

후속 반려 사유 보완은 **89검사**(이전76 포함)다. 직원 목록의 별도 검토 사유·실제 상세 버튼·긴 여러 줄 사유·읽기 전용/직접 검토 호출 거절·본인/지정 범위 밖 내용 차단·누락 사유 안내·접속 종료 후 상세 닫기를 추가 검증한다. 체험 도구는 같은 신청에 실제 검토 폼에서 입력한 반려 사유가 직원 상세에 전달되는 것을 포함해 **11검사**다. 합성 workflow/신원 검사이며 실제 서버/DB 검증이 아니다.

신청/검토 종료 보완 후 **115검사**(이전89 포함)다. 실제 `Form.Close()`로 처리 중 닫기 차단·진행 요청 취소 방지, 응답 유실 뒤 첫 닫기의 결과 미확인 안내, 같은 UUID/본문/검토 버전 재확인, 재시도 뒤 새 닫기 안내, 확인된 성공 후 정상 종료, 두 번째 명시 닫기가 저장 성공을 만들지 않는 것, 로그인 종료 시 닫기 차단 해제와 늦은 결과 무시를 검사한다. 창을 명시 닫은 뒤 RAM 작업의 복구/서버 조회가 자동 제공된다는 뜻은 아니다. 실제 API/DB 미연결은 유지한다.

연결 소비 보완 후 **144검사**(이전115 포함)다. 준비 상태·서버 연도·0.5일 단위 잔여/검토 대기/사용 일수, 오류 뒤 이전 숫자 제거, 설정과 목록의 독립 표시, 관리자 자신의 연차 미배정과 배정 직원 검토 분리, 늦은 준비 응답/오류·로그아웃, 메뉴 페이지 종료 뒤 동일 adapter 유지, 다른 Session/workflow 연결 거절을 검사한다. 이 기본 검사는 HTTP 대역이다.

## PR79 실제 adapter + 서버 + 소유 MySQL 결합 검사

Draft PR79는 제품 alpha에 합치지 않는다. `Tests/WorkdayActivity.Tests/prepare_peer.py`로 자신의 커밋과 PR79 head를 **새 외부 폴더**에 export한 뒤 그 소스의 `Tests/Leave.Ui.Tests`를 빌드한다. `HttpLeaveWorkflow.cs`가 있는 결합 소스에서만 `REAL_LEAVE_ADAPTER` 검사가 컴파일된다. `.env`·운영 DB·실제 계정·장비를 사용하지 않는다.

```powershell
python Tests/WorkdayActivity.Tests/prepare_peer.py --alpha-ref <5번-커밋> --peer-ref <PR79-head> --output <새-외부-폴더>
dotnet build <외부-폴더>/Tests/Leave.Ui.Tests -c Release --artifacts-path <외부-빌드-폴더>
python Tests/Leave.Ui.Tests/run_mysql.py --allow-disposable-mysql --mysql-bin-dir <검증된-MySQL-bin> --source-root <외부-폴더> --client <고정-Leave.Ui.Tests.dll> --report-directory <외부-결과-폴더>
```

Flask/SQLAlchemy/PyMySQL/pytest가 있는 Python과 Windows MySQL 8.0.16 이상이 필요하다. 실행기는 기존 MySQL 서비스/DB를 건드리지 않고 소유 Job·임시 DB/runtime을 검증·정리하며, DLL SHA를 전후 대조한다. 실패 출력은 외부 private 보고서에만 보관한다.

실제 MainForm 메뉴→동일 adapter→신청/검토 폼→API/W1→MySQL을 연결해 정상 승인, 신청·승인 응답 유실, 신청·반려 응답 유실, 정책 미설정 4개 시나리오를 실행한다. 응답 유실은 테스트 서버가 commit 뒤 첫 응답만 버린다. 두 명시 재확인은 원 intent로 복구하며 POST는 각각 한 번이어야 한다. SQL의 단일 신청·최종 상태·예약 해제/사용량도 확인한다. 테스트 정책·휴일·8일·합성 직원/관리자는 소유 DB 안의 fixture일 뿐 실제 운영 승인/배정이 아니다.
