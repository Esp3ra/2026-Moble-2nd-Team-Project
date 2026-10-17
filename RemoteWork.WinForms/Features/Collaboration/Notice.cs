namespace RemoteWork.WinForms.Features.Collaboration
{
    public sealed class Notice
    {
        public int Id { get; init; }
        public int AuthorId { get; init; }
        public string Title { get; init; } = "";
        public string Content { get; init; } = "";
        public string Target { get; init; } = "�� ����";
        public bool IsPinned { get; init; }
        public string Author { get; init; } = "������";
        public DateTime CreatedAt { get; init; } = DateTime.Now;
        public string[] AttachmentPaths { get; init; } = Array.Empty<string>();
        public string[] AttachmentNames { get; init; } = Array.Empty<string>();

        public string GetDisplayText()
        {
            var text = $"���: {Target}\r\n�ۼ���: {Author}    �����: {CreatedAt:yyyy.MM.dd}\r\n\r\n{Content}";
            var names = AttachmentNames.Length > 0 ? AttachmentNames
                : AttachmentPaths.Select(path => System.IO.Path.GetFileName(path)).ToArray();
            if (names.Length > 0)
                text += "\r\n\r\n÷������ (���ϸ��� ���� �� �ٿ�ε� �̿���)\r\n" + string.Join("\r\n", names);
            return text;
        }
    }
}
