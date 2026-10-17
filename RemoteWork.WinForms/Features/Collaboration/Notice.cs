namespace RemoteWork.WinForms.Features.Collaboration
{
    public sealed class Notice
    {
        public string Title { get; init; } = "";
        public string Content { get; init; } = "";
        public string Target { get; init; } = "전 직원";
        public bool IsPinned { get; init; }
        public string Author { get; init; } = "관리자";
        public DateTime CreatedAt { get; init; } = DateTime.Now;
        public string[] AttachmentPaths { get; init; } = Array.Empty<string>();

        public string GetDisplayText()
        {
            var text = $"대상: {Target}\r\n작성자: {Author}    등록일: {CreatedAt:yyyy.MM.dd}\r\n\r\n{Content}";
            if (AttachmentPaths.Length > 0)
                text += "\r\n\r\n첨부파일\r\n" + string.Join("\r\n",
                    AttachmentPaths.Select(path => System.IO.Path.GetFileName(path)));
            return text;
        }
    }
}
