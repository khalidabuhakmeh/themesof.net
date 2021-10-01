using Markdig;

using Microsoft.AspNetCore.Components;

namespace ThemesOfDotNet;

public static class MarkdownExtensions
{
    private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseEmojiAndSmiley()
        .Build();

    public static MarkupString RenderMarkdown(this string text)
    {
        var html = Markdown.ToHtml(text, _pipeline);
        return new MarkupString(html);
    }
}
