namespace CostTracker.Application.Pdf;

public interface IPdfRenderer
{
    byte[] Render(string referenceMonth, string analysisMarkdown);
}
