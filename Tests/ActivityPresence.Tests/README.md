# WinForms 활동 상태 확인 회귀

Windows, .NET SDK 8 이상/Windows Desktop runtime 8, `Tools/HandPresence/.venv`의 Python 3.13 환경에서 저장소 루트:

```powershell
dotnet run --project Tests/ActivityPresence.Tests
```

실제 WinForms 메시지 루프/컨트롤에 대역 카메라와 합성 JPEG를 사용합니다. 명시적 시작, 영역 오류, 상태 표시/지연/복구, 중지 후 결과 무시, 소유 모달과 종료, 직원/관리자의 기존 근태 화면을 검사합니다. 실제 Python 자식 프로세스의 stdio·stderr 동시 처리/최신 프레임·QPC 시각·즉시 중지·비정상 종료·응답 없는 프로세스 종료도 검사합니다. 물리 카메라·모델·사용자 영상·네트워크·DB는 사용하지 않습니다.

선택 `-- --render <임시 PNG 경로>`는 합성 단색 영상만 포함한 폼 렌더링을 임시 위치에 생성합니다. 실제 카메라 촬영 기능이 아니며 결과물을 Git에 넣지 않습니다. 상용 배포와 실제 하드웨어 인식 정확도는 이 테스트 범위가 아닙니다.

시작 지연 후속 회귀: 사전에 NumPy를 import하지 않은 새 Python 프로세스에서 실제 bridge CLI를 실행합니다. 카메라/모델은 명시적 대역으로 주입하고 실제 OpenCV/NumPy 초기화와 stdin 대기가 교착 없이 진행되어 장치 오류를 즉시 전달하는지 확인합니다. 최신 C#5시나리오49검증이며 모델 정확도 시험은 아닙니다.
