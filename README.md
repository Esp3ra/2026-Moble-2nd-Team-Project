# 원격 근무용 업무 처리 확인 시스템 문서

`DOCX`는 2차 팀 프로젝트의 기획·설계·참고자료를 관리하는 문서 브랜치입니다. 로컬 `2차 팀 프로젝트/DOCX` 폴더의 파일 10개를 같은 이름과 폴더 구조로 게시했습니다. 반영일은 **2026-10-16**입니다.

현재 화면 초안과 실행 방법은 [alpha README](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/alpha/README.md), 도구상자 화면 버전은 [0.0.2V](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/tree/0.0.2V), 수정 기록은 [LOG](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/tree/LOG), 전체 프로젝트 소개는 [main README](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/main/README.md)에서 확인합니다. `alpha`와 `0.0.2V`에서는 `DOCX` 폴더를 분리했습니다.

## 현재 문서와 참고자료

| 파일 | 용도 |
|---|---|
| [10-16~10-28.txt](DOCX/10-16~10-28.txt) | 일정 이름으로 만든 빈 파일; 원본 상태 보존 |
| [2번_업무_상태_마감_구현계획.md](DOCX/2%EB%B2%88_%EC%97%85%EB%AC%B4_%EC%83%81%ED%83%9C_%EB%A7%88%EA%B0%90_%EA%B5%AC%ED%98%84%EA%B3%84%ED%9A%8D.md) | 2번 담당의 구현 순서·화면·API·DB 제안과 합의 필요 항목 |
| [4팀 원격 근무용 업무처리 확인 시스템.pdf](DOCX/4%ED%8C%80%20%EC%9B%90%EA%B2%A9%20%EA%B7%BC%EB%AC%B4%EC%9A%A9%20%EC%97%85%EB%AC%B4%EC%B2%98%EB%A6%AC%20%ED%99%95%EC%9D%B8%20%EC%8B%9C%EC%8A%A4%ED%85%9C.pdf) | 프로젝트 참고 PDF |
| [4팀_원격근무용업무처리확인시스템_2기.pdf](DOCX/4%ED%8C%80_%EC%9B%90%EA%B2%A9%EA%B7%BC%EB%AC%B4%EC%9A%A9%EC%97%85%EB%AC%B4%EC%B2%98%EB%A6%AC%ED%99%95%EC%9D%B8%EC%8B%9C%EC%8A%A4%ED%85%9C_2%EA%B8%B0.pdf) | 프로젝트 참고 PDF |
| [원격 근무용 업무 처리 확인 시스템.docx](DOCX/%EC%9B%90%EA%B2%A9%20%EA%B7%BC%EB%AC%B4%EC%9A%A9%20%EC%97%85%EB%AC%B4%20%EC%B2%98%EB%A6%AC%20%ED%99%95%EC%9D%B8%20%EC%8B%9C%EC%8A%A4%ED%85%9C.docx) | 기획·업무 분담 기준 문서 |
| [폼 수정사항.md](DOCX/%ED%8F%BC%20%EC%88%98%EC%A0%95%EC%82%AC%ED%95%AD.md) | 폼 수정 검토안 |
| [프로젝트 방향.txt](DOCX/%ED%94%84%EB%A1%9C%EC%A0%9D%ED%8A%B8%20%EB%B0%A9%ED%96%A5.txt) | 프로젝트 방향 메모 |

## 이전 단계 사본

아래 문서는 파일명에 표시된 수정 전 상태를 보관하는 사본입니다. 현재 기준 문서와 구분해 참고합니다.

- [원격 근무용 업무 처리 확인 시스템_WinForms확정전_20261013.docx](DOCX/%EC%9B%90%EA%B2%A9%20%EA%B7%BC%EB%AC%B4%EC%9A%A9%20%EC%97%85%EB%AC%B4%20%EC%B2%98%EB%A6%AC%20%ED%99%95%EC%9D%B8%20%EC%8B%9C%EC%8A%A4%ED%85%9C_WinForms%ED%99%95%EC%A0%95%EC%A0%84_20261013.docx)
- [원격 근무용 업무 처리 확인 시스템_역할분담추가전_20261013.docx](DOCX/%EC%9B%90%EA%B2%A9%20%EA%B7%BC%EB%AC%B4%EC%9A%A9%20%EC%97%85%EB%AC%B4%20%EC%B2%98%EB%A6%AC%20%ED%99%95%EC%9D%B8%20%EC%8B%9C%EC%8A%A4%ED%85%9C_%EC%97%AD%ED%95%A0%EB%B6%84%EB%8B%B4%EC%B6%94%EA%B0%80%EC%A0%84_20261013.docx)
- [원격 근무용 업무 처리 확인 시스템_일정추가전_20261013.docx](DOCX/%EC%9B%90%EA%B2%A9%20%EA%B7%BC%EB%AC%B4%EC%9A%A9%20%EC%97%85%EB%AC%B4%20%EC%B2%98%EB%A6%AC%20%ED%99%95%EC%9D%B8%20%EC%8B%9C%EC%8A%A4%ED%85%9C_%EC%9D%BC%EC%A0%95%EC%B6%94%EA%B0%80%EC%A0%84_20261013.docx)

## 문서 기준과 게시 범위

업무 상태는 **진행전 / 진행중 / 제출완료 / 승인완료 / 반려완료** 5개입니다. 관리자도 본인 업무를 수락·제출하며 모든 정식 제출·재제출은 권한 있는 검토자의 승인·반려를 거칩니다. 승인완료만 최종 완료이고, 마감 경과에 따른 자동 제출은 제외합니다. 미확정 연결 규격은 구현계획의 **합의 필요** 항목으로 관리합니다.

폼 수정 검토안과 이전 단계 문서에는 과거 제안이 남아 있을 수 있습니다. 이후 확정된 규칙과 현재 구현계획을 함께 확인합니다. 현재 프로그램은 실제 업무 기능을 연결하지 않은 화면 뼈대입니다.

이 브랜치에는 문서 폴더와 이 README만 포함합니다. 문서 10개를 모두 보존했고, 이번에는 구현계획서의 게시 위치 안내와 링크만 새 브랜치 기준으로 수정했습니다. Word·PDF·이전 사본의 내용은 변경하지 않았습니다. 문서를 푸시할 때마다 README의 목록·현재 기준·게시 범위를 최신화합니다.
