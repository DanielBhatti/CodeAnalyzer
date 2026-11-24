using Avalonia.Media;

namespace CodeAnalyzer.UI;

public sealed record MethodRow(MethodMetric Metric, int SelectedMetric = 0)
{
    public string Name => Metric.Name;
    public int Complexity => Metric.Complexity;
    public double Value => SelectedMetric switch
    {
        1 => Metric.CognitiveComplexity,
        2 => Metric.MaintainabilityIndex,
        3 => Metric.LinesOfCode,
        _ => Complexity
    };
    public string ValueLabel => Value.ToString("0.#", System.Globalization.CultureInfo.CurrentCulture);
    public string MetricsLabel => $"Cyclomatic {Complexity} · Cognitive {Metric.CognitiveComplexity} · MI {Metric.MaintainabilityIndex:0.#}";
    public string Location => $"{Metric.FilePath}:{Metric.Line}";
    public string SizeLabel => $"{Metric.LinesOfCode} lines of code";
    public double BarWidth => Math.Min(400, Value * (SelectedMetric == 2 ? 4 : 8));
    public IBrush BarBrush => SelectedMetric == 2
        ? Value < 10 ? Brushes.Salmon : Value < 20 ? Brushes.Gold : Brushes.MediumTurquoise
        : SelectedMetric == 3 ? Brushes.MediumTurquoise
        : Value > 20 ? Brushes.Salmon : Value > 10 ? Brushes.Gold : Brushes.MediumTurquoise;
}