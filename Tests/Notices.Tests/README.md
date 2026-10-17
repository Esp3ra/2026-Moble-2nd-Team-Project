# ���� ���塤��ȸ ����

Windows/.NET 8���� �����մϴ�. �⺻ ������ ������ HTTP/���� �뿪�� ���� WinForms �޽��� ������ �����ϸ� ���� DB�� �������� �ʽ��ϴ�.

```powershell
dotnet run --project .\Tests\Notices.Tests\Notices.Tests.csproj -c Release
```

���ϸ� ��Ÿ������ ���ۡ��ۼ���/��� �����ۡ����� Bearer�������� ��ü �б⡤�� ID ����, ���� �Ϸ� �� OK �������ߺ� Ŭ�� ���������� ���� �Է� ��������õ�, ��� ����/����/������ȭ�� ���� �� ���� ������ Ȯ���մϴ�. ����� ���콺/Ű���� �Է��̳� ���� �˼��� �ƴմϴ�.

���� C# �ۼ� ȭ���HTTP�漭����SQLite�� ���� ��ȸ������ ������ �����Ϸ��� ���� Release ���� �� ������Ʈ ��Ʈ���� ������ �����մϴ�. ���� ���� �������� ��ġ�� Python�� ����մϴ�.

```powershell
python .\Tests\Notices.Tests\run_http_check.py --client-dll .\Tests\Notices.Tests\bin\Release\net8.0-windows\Notices.Tests.dll
```

������ �ӽ� ���͸��� ���� SQLite���ӽ� ��Ʈ���ռ� ������ ����մϴ�. ����� .env �ε带 �����ϰ� ���� ������DB ������ � �����Ϳ� �������� �ʽ��ϴ�. ���� �� �ڱ� ������ �ݰ� �ӽ� DB�� �����մϴ�. ���� �ռ� ��ȣ�� �׽�Ʈ �����̸� ���� �������� �������� �ʽ��ϴ�.

���� ���塤����� ���� �� pytest ������ `RemoteWork.Server`���� �Ʒ�ó�� �����մϴ�. `MYSQL_TEST_URL`�� ������ ������ ���� ���� fixture�� �ش� �׽�Ʈ DB�� ���Ƿ� �⺻ SQLite ���������� �����մϴ�.

```powershell
$env:PYTHON_DOTENV_DISABLED = '1'
Remove-Item Env:MYSQL_TEST_URL -ErrorAction SilentlyContinue
python -m pytest -q
```

���� MySQL������ �������� PC�����ε�/�ٿ�ε塤���� �ڵ� �˾���Ȯ�� ����� �� ������� ���� ������ �ƴմϴ�.


�ֽ� alpha ��� ���: ���� ������ `REMOTEWORK_ENABLE_NOTICE_DRAFT=1`������ Ȱ��ȭ�˴ϴ�. Python HTTP ������ �ڽ��� �׽�Ʈ ���μ����� �� �÷��׸� �����մϴ�. ���� pytest ����� autouse fixture�� �׽�Ʈ �߿��� �Ѱ� �����մϴ�. �ֽ� ���պ��� �׽�Ʈ�� PR �غ� �� ��������� �ʾҽ��ϴ�.
