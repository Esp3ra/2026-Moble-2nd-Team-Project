> **2026-10-19 · 1번 공통규약 사전 협의 요청** — [요청서](../1번/2026-10-19_공통규약_사전협의요청.md)
> 2번 확인: C01 공유 적용, C02 상태별 수정/재배정, C03 검토자, C04·C06 제출/이력 접점, C07 마감/연장. 최신 등록 화면 연결을 반영했습니다. **회신 대기이며 새 규약 확정·구현 완료가 아닙니다.**

# 2번 업무·상태·마감 — 현재 안내

최신화: 2026-10-19 · 담당/팀장: 사용자 본인

## 먼저 볼 문서

| 문서 | 내용 |
|---|---|
| [공통규약·협의 회신](공통규약.md) | 확정 기준, 2번 개발용 등록 계약, 3·4·5번 최신 수신·회신 요청, 1번 복귀 후 기술 검토 |
| [업무 구현·검증](업무_구현검증.md) | 실제 조회·신규 등록·수락/재작업 대상 확인, 회차별 테스트와 미실행, 다음 구현 |
| [PR 검토 기록](PR_검토기록.md) | 병합/미병합·수정 요청·작성자 보고/독립 검증 구분 |
| [2번 영역 협업 기준](2026-10-17_2번_영역수정_협업규약.md) | 다른 담당이 2번 파일·조회/화면 접점을 변경할 때 확인할 경계 |
| [지문 용도 기준](2026-10-17_지문센서_원격근무_본인인증_용도변경.md) | 로그인 후 본인 확인·재석 보조·업무 인증 조건의 경계 |
| [3번 재작업 확정 회신](2026-10-19_3번_재작업상태_확정_및_연결회신.md) | 반려완료→재작업 시작→진행중→재제출 성공→제출완료와 담당 분리 |
| [PR19·20 원 검토 기록](2026-10-19_PR19_PR20_장치시연_검토_병합.md) | 외부 전달 링크를 유지하는 당시 독립 검토 원문 |

## 현재 상태

- 기본 인증·HTTP·DB·오류·권한/버전·요청 결과 지원은 원본에 있습니다. 1번 부재 동안 기존 지원을 재사용하며 공통 기능을 대신 바꾸지 않습니다.
- 2번은 실제 목록·상세 조회와 입력/후보 준비, 등록자 필수 관리자를 포함한 개발용 신규 등록 화면/API를 연결했습니다. 입력 확인 후 명시적 등록, 불명확 결과 확인·같은 키 재전송, 성공 후 범위/필터 유지 재조회를 검증했습니다. 서버 기본 off·공유 미적용이며 1번 공동 기술 검토는 남아 있습니다.
- 수락/재작업 대상은 기존 GET으로 현재 본인 배정·상태·버전을 확인하고 명시적 CheckedTarget을 보관합니다. 진행전/반려완료의 화면·대상을 구분하며 다른 상태는 대상 버튼을 막습니다. 실제 상태 저장·마감·3번 제출/판단/파일은 별도이며 확인값을 실행 허용이나 저장 완료로 표시하지 않습니다.
- 다음 합의는 W3-01/06의 시작 전·마감 후/시간대, W1-03/04의 행동별 API·버전 증가·오류/응답·재작업 별도 action/감사/receipt입니다. 최초 수락·배정 번호·제출 회차/과거 결과 보존 방향은 최신3번 회신 수신 기준을 유지합니다.
- 3번 재작업 흐름 수신 확인을 유지합니다. 4번의 새 수정 회신을 받아 기존3건을 확인했고 추가 R21-04를 요청했습니다. 5번은 문서24만 병합했으며 코드23의 지연 프레임 문제와 D5 연결 합의가 남아 있습니다.
- 5번 최신 포크 문서는 카메라 기본 인식 정상 보고와 ‘활동 중·자리 비움’의 표시 변경을 추가했습니다. 실제 근태 저장·신원/정확도 검증과 구분해 공통규약에 기록했습니다.
- 공통규약의 현재 기준과 제안·담당자 합의·기술 검토·실제 적용을 구분합니다. 공통 버전 검사 도구가 있다는 사실이 정식 DDL 적용을 뜻하지 않습니다.

## 문서 정리 기준

기존 42개 Markdown을 **8개**로 정리했습니다. 같은 목적의 요청·회신과 구현/검증·PR 기록은 주제별 문서에 통합했습니다. 외부에서 사용 중인 협업·지문·재작업·장치 검토 대표 경로 4개는 유지합니다. 기존 날짜형 문서를 새로 계속 늘리지 않고 주제 문서에 현재 상태와 회차를 추가합니다.

과거 원문은 통합 전 Git 커밋의 고정 링크와 SHA256으로 보존했습니다. 아래 이동표로 옛 파일을 찾을 수 있습니다. 과거의 미연결/대기·PR 상태·검증 숫자를 현재 완료 상태로 읽거나 다른 실행과 합산하지 않습니다.

## 문서 이동표

<details>
<summary>기존 42개 파일의 새 위치·작성 당시 원문 보기</summary>

| 기존 파일 | 현재 위치 | 보존 원문 |
|---|---|---|
| `2026-10-16_1번_공통규약_회신정리.md` | [이동](공통규약.md#r001) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-16_1%EB%B2%88_%EA%B3%B5%ED%86%B5%EA%B7%9C%EC%95%BD_%ED%9A%8C%EC%8B%A0%EC%A0%95%EB%A6%AC.md) |
| `2026-10-16_1번_담당_공통규약_확인요청.md` | [이동](공통규약.md#r002) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-16_1%EB%B2%88_%EB%8B%B4%EB%8B%B9_%EA%B3%B5%ED%86%B5%EA%B7%9C%EC%95%BD_%ED%99%95%EC%9D%B8%EC%9A%94%EC%B2%AD.md) |
| `2026-10-16_PR3_병합_및_수정요청_분리.md` | [이동](PR_검토기록.md#r003) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-16_PR3_%EB%B3%91%ED%95%A9_%EB%B0%8F_%EC%88%98%EC%A0%95%EC%9A%94%EC%B2%AD_%EB%B6%84%EB%A6%AC.md) |
| `2026-10-16_PR4_문서병합_상태갱신.md` | [이동](PR_검토기록.md#r004) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-16_PR4_%EB%AC%B8%EC%84%9C%EB%B3%91%ED%95%A9_%EC%83%81%ED%83%9C%EA%B0%B1%EC%8B%A0.md) |
| `2026-10-16_PR검토_병합후_1번전달사항.md` | [이동](PR_검토기록.md#r005) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-16_PR%EA%B2%80%ED%86%A0_%EB%B3%91%ED%95%A9%ED%9B%84_1%EB%B2%88%EC%A0%84%EB%8B%AC%EC%82%AC%ED%95%AD.md) |
| `2026-10-16_업무목록_상세_조회연결_준비.md` | [이동](업무_구현검증.md#r006) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-16_%EC%97%85%EB%AC%B4%EB%AA%A9%EB%A1%9D_%EC%83%81%EC%84%B8_%EC%A1%B0%ED%9A%8C%EC%97%B0%EA%B2%B0_%EC%A4%80%EB%B9%84.md) |
| `2026-10-16_업무조회_인터페이스_DTO_전달.md` | [이동](업무_구현검증.md#r007) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-16_%EC%97%85%EB%AC%B4%EC%A1%B0%ED%9A%8C_%EC%9D%B8%ED%84%B0%ED%8E%98%EC%9D%B4%EC%8A%A4_DTO_%EC%A0%84%EB%8B%AC.md) |
| `2026-10-17_002업무DDL_초안_검증.md` | [이동](업무_구현검증.md#r008) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_002%EC%97%85%EB%AC%B4DDL_%EC%B4%88%EC%95%88_%EA%B2%80%EC%A6%9D.md) |
| `2026-10-17_1번_W1_N4_회신수신_PR12_병합.md` | [이동](PR_검토기록.md#r009) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_1%EB%B2%88_W1_N4_%ED%9A%8C%EC%8B%A0%EC%88%98%EC%8B%A0_PR12_%EB%B3%91%ED%95%A9.md) |
| `2026-10-17_1번_쓰기공통지원_구현전달.md` | [이동](공통규약.md#r010) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_1%EB%B2%88_%EC%93%B0%EA%B8%B0%EA%B3%B5%ED%86%B5%EC%A7%80%EC%9B%90_%EA%B5%AC%ED%98%84%EC%A0%84%EB%8B%AC.md) |
| `2026-10-17_1번_업무쓰기_공통규약_확인요청.md` | [이동](공통규약.md#r011) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_1%EB%B2%88_%EC%97%85%EB%AC%B4%EC%93%B0%EA%B8%B0_%EA%B3%B5%ED%86%B5%EA%B7%9C%EC%95%BD_%ED%99%95%EC%9D%B8%EC%9A%94%EC%B2%AD.md) |
| `2026-10-17_1번_업무조회_API_DB_합의요청.md` | [이동](공통규약.md#r012) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_1%EB%B2%88_%EC%97%85%EB%AC%B4%EC%A1%B0%ED%9A%8C_API_DB_%ED%95%A9%EC%9D%98%EC%9A%94%EC%B2%AD.md) |
| `2026-10-17_1번회신_수신확인_및_조회규약반영.md` | [이동](공통규약.md#r013) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_1%EB%B2%88%ED%9A%8C%EC%8B%A0_%EC%88%98%EC%8B%A0%ED%99%95%EC%9D%B8_%EB%B0%8F_%EC%A1%B0%ED%9A%8C%EA%B7%9C%EC%95%BD%EB%B0%98%EC%98%81.md) |
| `2026-10-17_2번_영역수정_협업규약.md` | [이동](2026-10-17_2번_영역수정_협업규약.md) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_2%EB%B2%88_%EC%98%81%EC%97%AD%EC%88%98%EC%A0%95_%ED%98%91%EC%97%85%EA%B7%9C%EC%95%BD.md) |
| `2026-10-17_3번_W3_회신수신_및_조회연결안내.md` | [이동](공통규약.md#r015) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_3%EB%B2%88_W3_%ED%9A%8C%EC%8B%A0%EC%88%98%EC%8B%A0_%EB%B0%8F_%EC%A1%B0%ED%9A%8C%EC%97%B0%EA%B2%B0%EC%95%88%EB%82%B4.md) |
| `2026-10-17_3번_수락상태_마감_연결규약_확인요청.md` | [이동](공통규약.md#r016) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_3%EB%B2%88_%EC%88%98%EB%9D%BD%EC%83%81%ED%83%9C_%EB%A7%88%EA%B0%90_%EC%97%B0%EA%B2%B0%EA%B7%9C%EC%95%BD_%ED%99%95%EC%9D%B8%EC%9A%94%EC%B2%AD.md) |
| `2026-10-17_3번_업무상태_제출검토_연결합의요청.md` | [이동](공통규약.md#r017) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_3%EB%B2%88_%EC%97%85%EB%AC%B4%EC%83%81%ED%83%9C_%EC%A0%9C%EC%B6%9C%EA%B2%80%ED%86%A0_%EC%97%B0%EA%B2%B0%ED%95%A9%EC%9D%98%EC%9A%94%EC%B2%AD.md) |
| `2026-10-17_4번_PR7_수정회신.md` | [이동](PR_검토기록.md#r018) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_4%EB%B2%88_PR7_%EC%88%98%EC%A0%95%ED%9A%8C%EC%8B%A0.md) |
| `2026-10-17_PR15_PR16_공통지원_검토_병합.md` | [이동](PR_검토기록.md#r019) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_PR15_PR16_%EA%B3%B5%ED%86%B5%EC%A7%80%EC%9B%90_%EA%B2%80%ED%86%A0_%EB%B3%91%ED%95%A9.md) |
| `2026-10-17_PR1_PR2_재검토_및_PR5_PR6_병합.md` | [이동](PR_검토기록.md#r020) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_PR1_PR2_%EC%9E%AC%EA%B2%80%ED%86%A0_%EB%B0%8F_PR5_PR6_%EB%B3%91%ED%95%A9.md) |
| `2026-10-17_PR7_Ready_재검토_PR13_PR14_병합.md` | [이동](PR_검토기록.md#r021) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_PR7_Ready_%EC%9E%AC%EA%B2%80%ED%86%A0_PR13_PR14_%EB%B3%91%ED%95%A9.md) |
| `2026-10-17_PR7_검토_및_PR8_PR9_문서병합.md` | [이동](PR_검토기록.md#r022) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_PR7_%EA%B2%80%ED%86%A0_%EB%B0%8F_PR8_PR9_%EB%AC%B8%EC%84%9C%EB%B3%91%ED%95%A9.md) |
| `2026-10-17_PR7_수정재검토_PR10_PR11_문서병합.md` | [이동](PR_검토기록.md#r023) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_PR7_%EC%88%98%EC%A0%95%EC%9E%AC%EA%B2%80%ED%86%A0_PR10_PR11_%EB%AC%B8%EC%84%9C%EB%B3%91%ED%95%A9.md) |
| `2026-10-17_W1-05_상태CHECK_수정_MySQL_검증.md` | [이동](업무_구현검증.md#r024) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_W1-05_%EC%83%81%ED%83%9CCHECK_%EC%88%98%EC%A0%95_MySQL_%EA%B2%80%EC%A6%9D.md) |
| `2026-10-17_W1_후보_쓰기준비_화면연결.md` | [이동](업무_구현검증.md#r025) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_W1_%ED%9B%84%EB%B3%B4_%EC%93%B0%EA%B8%B0%EC%A4%80%EB%B9%84_%ED%99%94%EB%A9%B4%EC%97%B0%EA%B2%B0.md) |
| `2026-10-17_W1_후속_행동별API_사용자선택_제안.md` | [이동](공통규약.md#r026) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_W1_%ED%9B%84%EC%86%8D_%ED%96%89%EB%8F%99%EB%B3%84API_%EC%82%AC%EC%9A%A9%EC%9E%90%EC%84%A0%ED%83%9D_%EC%A0%9C%EC%95%88.md) |
| `2026-10-17_업무등록_수락_마감_입력준비.md` | [이동](업무_구현검증.md#r027) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_%EC%97%85%EB%AC%B4%EB%93%B1%EB%A1%9D_%EC%88%98%EB%9D%BD_%EB%A7%88%EA%B0%90_%EC%9E%85%EB%A0%A5%EC%A4%80%EB%B9%84.md) |
| `2026-10-17_업무목록_상세_실제조회_개발초안.md` | [이동](업무_구현검증.md#r028) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_%EC%97%85%EB%AC%B4%EB%AA%A9%EB%A1%9D_%EC%83%81%EC%84%B8_%EC%8B%A4%EC%A0%9C%EC%A1%B0%ED%9A%8C_%EA%B0%9C%EB%B0%9C%EC%B4%88%EC%95%88.md) |
| `2026-10-17_업무조회_API_DB_합의초안.md` | [이동](업무_구현검증.md#r029) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_%EC%97%85%EB%AC%B4%EC%A1%B0%ED%9A%8C_API_DB_%ED%95%A9%EC%9D%98%EC%B4%88%EC%95%88.md) |
| `2026-10-17_업무조회_MySQL_실제HTTP_실행기.md` | [이동](업무_구현검증.md#r030) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_%EC%97%85%EB%AC%B4%EC%A1%B0%ED%9A%8C_MySQL_%EC%8B%A4%EC%A0%9CHTTP_%EC%8B%A4%ED%96%89%EA%B8%B0.md) |
| `2026-10-17_업무조회_격리HTTP_통합검증_실행기.md` | [이동](업무_구현검증.md#r031) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_%EC%97%85%EB%AC%B4%EC%A1%B0%ED%9A%8C_%EA%B2%A9%EB%A6%ACHTTP_%ED%86%B5%ED%95%A9%EA%B2%80%EC%A6%9D_%EC%8B%A4%ED%96%89%EA%B8%B0.md) |
| `2026-10-17_입력확인_스냅샷_실제HTTP_준비창검증.md` | [이동](업무_구현검증.md#r032) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_%EC%9E%85%EB%A0%A5%ED%99%95%EC%9D%B8_%EC%8A%A4%EB%83%85%EC%83%B7_%EC%8B%A4%EC%A0%9CHTTP_%EC%A4%80%EB%B9%84%EC%B0%BD%EA%B2%80%EC%A6%9D.md) |
| `2026-10-17_조회_상태필터_응답검증_보강.md` | [이동](업무_구현검증.md#r033) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_%EC%A1%B0%ED%9A%8C_%EC%83%81%ED%83%9C%ED%95%84%ED%84%B0_%EC%9D%91%EB%8B%B5%EA%B2%80%EC%A6%9D_%EB%B3%B4%EA%B0%95.md) |
| `2026-10-17_조회_선택유지_상세재시도_취소처리.md` | [이동](업무_구현검증.md#r034) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_%EC%A1%B0%ED%9A%8C_%EC%84%A0%ED%83%9D%EC%9C%A0%EC%A7%80_%EC%83%81%EC%84%B8%EC%9E%AC%EC%8B%9C%EB%8F%84_%EC%B7%A8%EC%86%8C%EC%B2%98%EB%A6%AC.md) |
| `2026-10-17_지문센서_원격근무_본인인증_용도변경.md` | [이동](2026-10-17_지문센서_원격근무_본인인증_용도변경.md) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_%EC%A7%80%EB%AC%B8%EC%84%BC%EC%84%9C_%EC%9B%90%EA%B2%A9%EA%B7%BC%EB%AC%B4_%EB%B3%B8%EC%9D%B8%EC%9D%B8%EC%A6%9D_%EC%9A%A9%EB%8F%84%EB%B3%80%EA%B2%BD.md) |
| `2026-10-17_합의전_진행가능범위_점검.md` | [이동](업무_구현검증.md#r036) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_%ED%95%A9%EC%9D%98%EC%A0%84_%EC%A7%84%ED%96%89%EA%B0%80%EB%8A%A5%EB%B2%94%EC%9C%84_%EC%A0%90%EA%B2%80.md) |
| `2026-10-17_회신재확인_목록상세_변경안내.md` | [이동](업무_구현검증.md#r037) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_%ED%9A%8C%EC%8B%A0%EC%9E%AC%ED%99%95%EC%9D%B8_%EB%AA%A9%EB%A1%9D%EC%83%81%EC%84%B8_%EB%B3%80%EA%B2%BD%EC%95%88%EB%82%B4.md) |
| `2026-10-19_3번_재작업상태_확정_및_연결회신.md` | [이동](2026-10-19_3번_재작업상태_확정_및_연결회신.md) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-19_3%EB%B2%88_%EC%9E%AC%EC%9E%91%EC%97%85%EC%83%81%ED%83%9C_%ED%99%95%EC%A0%95_%EB%B0%8F_%EC%97%B0%EA%B2%B0%ED%9A%8C%EC%8B%A0.md) |
| `2026-10-19_PR19_PR20_장치시연_검토_병합.md` | [이동](2026-10-19_PR19_PR20_장치시연_검토_병합.md) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-19_PR19_PR20_%EC%9E%A5%EC%B9%98%EC%8B%9C%EC%97%B0_%EA%B2%80%ED%86%A0_%EB%B3%91%ED%95%A9.md) |
| `2026-10-19_PR21_채팅검토_PR22_문서병합.md` | [이동](PR_검토기록.md#r040) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-19_PR21_%EC%B1%84%ED%8C%85%EA%B2%80%ED%86%A0_PR22_%EB%AC%B8%EC%84%9C%EB%B3%91%ED%95%A9.md) |
| `README.md` | [이동](README.md) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/README.md) |
| `기록목록.md` | [이동](README.md#문서-이동표) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/%EA%B8%B0%EB%A1%9D%EB%AA%A9%EB%A1%9D.md) |

</details>

[전체 LOG](../README.md) · [alpha 코드](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/tree/alpha)

## 1번 기술 회신 도착 — 2026-10-19

[공통규약 후속](공통규약.md#1번-복귀-기술-회신--2026-10-19) · [1번 판정·검증](../1번/2026-10-19_협업요청_기술검토_회신.md). 신규업무등록초기값/DB·채팅API/DDL·3번제출/파일/이력·5번회원장치접점에답변했습니다. 1번기술수용과공동회신/제품연결·공유적용은구분합니다.

게시 확인: [DB SQL·검증 PR #25](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/25) · [협업 LOG PR #26](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/26) 생성 완료. 포크 alpha/LOG → 원본 alpha/LOG 검토 대기. 이번 보완의 원본 병합·각 담당 수신/구현·공유 적용은 아직 완료로 표시하지 않는다.
