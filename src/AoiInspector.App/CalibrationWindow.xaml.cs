using System.Globalization;
using System.Windows;
using AoiInspector.Workflows;

namespace AoiInspector.App;

public partial class CalibrationWindow : Window
{
    public CalibrationWindow(double currentMicrometersPerPixel)
    {
        InitializeComponent();
        CurrentScaleText.Text = $"目前比例：{currentMicrometersPerPixel:0.####} µm/px";
    }

    public double CalculatedMicrometersPerPixel { get; private set; }
    public string CalibrationReference { get; private set; } = string.Empty;

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var knownLength = ParsePositive(KnownLengthTextBox.Text, "標定物已知長度");
            var measuredPixels = ParsePositive(MeasuredPixelsTextBox.Text, "影像量測長度");
            CalculatedMicrometersPerPixel = ScaleCalibration.CalculateMicrometersPerPixel(
                knownLength, measuredPixels);
            CalibrationReference = CalibrationReferenceTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(CalibrationReference))
                throw new FormatException("標定板／量具識別不可空白。");
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "校正輸入錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private static double ParsePositive(string value, string name)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ||
            parsed <= 0 || !double.IsFinite(parsed))
            throw new FormatException($"{name}必須是大於零的數字，請使用小數點。");
        return parsed;
    }
}
