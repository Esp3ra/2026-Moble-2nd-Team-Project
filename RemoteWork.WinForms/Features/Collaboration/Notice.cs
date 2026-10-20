namespace RemoteWork.WinForms.Features.Collaboration
{
    public sealed class Notice
    {
        public int Id { get; init; }
        public int AuthorId { get; init; }
        public string PopupKey { get; init; } = "";
        public string Title { get; init; } = "";
        public string Content { get; init; } = "";
        public string Target { get; init; } = "전 직원";
        public bool IsPinned { get; init; }
        public string Author { get; init; } = "관리자";
        public DateTime CreatedAt { get; init; } = DateTime.Now;
        public string[] AttachmentPaths { get; init; } = Array.Empty<string>();
        public string[] AttachmentNames { get; init; } = Array.Empty<string>();

        public string GetDisplayText()
        {
            var text = $"대상: {Target}\r\n작성자: {Author}    등록일: {CreatedAt:yyyy.MM.dd}\r\n\r\n{Content}";
            var names = AttachmentNames.Length > 0 ? AttachmentNames
                : AttachmentPaths.Select(path => System.IO.Path.GetFileName(path)).ToArray();
            if (names.Length > 0)
                text += "\r\n\r\n첨부파일 (파일명만 저장 · 다운로드 미연결)\r\n" + string.Join("\r\n", names);
            return text;
        }
    }
}
