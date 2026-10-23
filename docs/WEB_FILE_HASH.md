# 내부망 HTTP 공통 파일 해시 제공 — T23

1번 제공/2·3번 소비 협의용 접점입니다. 신규 API/DTO/DDL·기능설정·권한/정책이 아니며 자동 설치하지 않습니다. 기존4번 resource_attachments.js와 같은 SHA-256 표준을 사용하되 IO·권한·자원 전송 코드를 가져오지 않습니다.

기존2번 task_attachments.js와3번 results.js 앞에 동일 출처의 static/file_hash.js를 defer로 로드한 뒤 다음처럼 연결하는 최소 변경을 제안합니다. 화면별 적용은2·3번 담당자가 기존 원 요청·접속·취소 경계와 대조해 수행합니다.

```js
const digest = await RemoteWorkFileHash.sha256(bytes, { signal: controller.signal });
```

입력은 ArrayBuffer 또는 typed view이며 정확한 byteOffset/byteLength만 처리합니다. 최대25MiB, 출력은64자 소문자hex입니다. 입력은 첫 yield 전 복사해 호출자의 변경을 격리합니다. 약256KiB마다 event loop에 양보해 취소/화면 응답을 유지하고 AbortError로 중단합니다. crypto.subtle·네트워크·외부 CDN·브라우저 영속저장·자동POST재시도·새 라이브러리는 없습니다. 이를 비밀번호/인증 서명 대용으로 쓰지 않습니다. HTTPS나 인증을 제공하는 함수도 아닙니다.

소비자는 canHash의 secure-context 전제를 명시 제공된 함수 존재 검사로 바꾸고 업로드·다운로드의 전체 bytes/size/SHA 검사를 유지합니다. 취소 뒤 전송하지 않으며 함수 반환 전후 현재 세션·원 파일을 검사합니다. 새 키/파일 재업로드·숨김 성공으로 바꾸지 않습니다. 실패 시 기존 원파일·key·body 보존과 현재 오류 복구 계약을 유지합니다. 서버해시를 그대로 믿거나 다운로드 검사를 생략하지 않습니다.

검증 명령: node Tests/FileHash.Tests/verify.mjs. Node24 crypto를 기준으로 padding경계/Unicode/이진/view/25MiB/초과·취소/호출후변경/중복설치를 검사합니다. 실제 브라우저/LAN/2·3번 화면·MySQL·물리두PC 성공은 별도입니다. 기존 화면은 이번에 수정하지 않으며 이 제공만으로 HTTP 첨부 제한 해소를 완료 처리하지 않습니다.
