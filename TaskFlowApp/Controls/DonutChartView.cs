using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace TaskFlowApp.Controls;

public sealed class DonutChartView : GraphicsView, IDrawable
{
    public static readonly BindableProperty FirstValueProperty =
        BindableProperty.Create(
            nameof(FirstValue),
            typeof(double),
            typeof(DonutChartView),
            0d,
            propertyChanged: OnChartPropertyChanged);

    public static readonly BindableProperty SecondValueProperty =
        BindableProperty.Create(
            nameof(SecondValue),
            typeof(double),
            typeof(DonutChartView),
            0d,
            propertyChanged: OnChartPropertyChanged);

    public static readonly BindableProperty FirstSegmentColorProperty =
        BindableProperty.Create(
            nameof(FirstSegmentColor),
            typeof(Color),
            typeof(DonutChartView),
            Color.FromArgb("#22c55e"),
            propertyChanged: OnChartPropertyChanged);

    public static readonly BindableProperty SecondSegmentColorProperty =
        BindableProperty.Create(
            nameof(SecondSegmentColor),
            typeof(Color),
            typeof(DonutChartView),
            Color.FromArgb("#f97316"),
            propertyChanged: OnChartPropertyChanged);

    public static readonly BindableProperty BackgroundRingColorProperty =
        BindableProperty.Create(
            nameof(BackgroundRingColor),
            typeof(Color),
            typeof(DonutChartView),
            Color.FromArgb("#2B3A4E"),
            propertyChanged: OnChartPropertyChanged);

    public double FirstValue
    {
        get => (double)GetValue(FirstValueProperty);
        set => SetValue(FirstValueProperty, value);
    }

    public double SecondValue
    {
        get => (double)GetValue(SecondValueProperty);
        set => SetValue(SecondValueProperty, value);
    }

    public Color FirstSegmentColor
    {
        get => (Color)GetValue(FirstSegmentColorProperty);
        set => SetValue(FirstSegmentColorProperty, value);
    }

    public Color SecondSegmentColor
    {
        get => (Color)GetValue(SecondSegmentColorProperty);
        set => SetValue(SecondSegmentColorProperty, value);
    }

    public Color BackgroundRingColor
    {
        get => (Color)GetValue(BackgroundRingColorProperty);
        set => SetValue(BackgroundRingColorProperty, value);
    }

    public DonutChartView()
    {
        Drawable = this;
        HeightRequest = 130;
        WidthRequest = 130;
    }

    void IDrawable.Draw(ICanvas canvas, RectF dirtyRect)
    {
        var chartSize = MathF.Min(dirtyRect.Width, dirtyRect.Height);
        if (chartSize <= 0f)
        {
            return;
        }

        const float chartPadding = 8f;
        chartSize -= chartPadding * 2f;
        if (chartSize <= 0f)
        {
            return;
        }

        var centerX = dirtyRect.Center.X;
        var centerY = dirtyRect.Center.Y;
        var strokeSize = MathF.Max(10f, chartSize * 0.2f);
        var drawableDiameter = chartSize - strokeSize;
        if (drawableDiameter <= 0f)
        {
            return;
        }

        var radius = drawableDiameter / 2f;
        var x = centerX - radius;
        var y = centerY - radius;

        canvas.Antialias = true;
        canvas.StrokeLineCap = LineCap.Butt;
        canvas.StrokeSize = strokeSize;
        canvas.StrokeColor = BackgroundRingColor;
        canvas.DrawCircle(centerX, centerY, radius);

        var first = Math.Max(0d, FirstValue);
        var second = Math.Max(0d, SecondValue);
        var total = first + second;
        if (total <= 0d)
        {
            return;
        }

        const float startAngle = -90f;
        var firstSweep = (float)(360d * (first / total));
        var secondSweep = 360f - firstSweep;

        DrawSegment(canvas, x, y, drawableDiameter, startAngle, firstSweep, FirstSegmentColor);
        DrawSegment(canvas, x, y, drawableDiameter, startAngle + firstSweep, secondSweep, SecondSegmentColor);
    }

    private static void DrawSegment(
        ICanvas canvas,
        float x,
        float y,
        float size,
        float startAngle,
        float sweepAngle,
        Color color)
    {
        if (sweepAngle <= 0.01f)
        {
            return;
        }

        canvas.StrokeColor = color;
        canvas.DrawArc(
            x,
            y,
            size,
            size,
            startAngle,
            startAngle + sweepAngle,
            false,
            false);
    }

    private static void OnChartPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is DonutChartView chartView)
        {
            chartView.Invalidate();
        }
    }
}
