using Avalonia.Media;

namespace CodeAnalyzer.UI;

public sealed record MethodRow(MethodMetric Metric)
{
    public string Name => Metric.Name;
    public int Complexity => Metric.Complexity;
    public string Location => $"{Metric.FilePath}:{Metric.Line}";
    public string SizeLabel => $"{Metric.Lines} source lines";
    public double BarWidth => Math.Min(400, Metric.Complexity * 8);
    public IBrush BarBrush => Complexity > 20 ? Brushes.Salmon : Complexity > 10 ? Brushes.Gold : Brushes.MediumTurquoise;
}