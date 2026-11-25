using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace NetworkScanner;

public partial class MainWindow : Window
{
    readonly MainViewModel vm = new();
    string sortProperty = nameof(DeviceItem.IpSort);
    ListSortDirection sortDirection = ListSortDirection.Ascending;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = vm;
        ApplySort();
        Loaded += async (_, _) => await vm.StartAsync();
        Closing += (_, _) => vm.CancelScan();
    }

    void OnHeaderClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader { Column.Header: string header }) return;
        var property = header switch
        {
            "Device" => nameof(DeviceItem.Name),
            "Type" => nameof(DeviceItem.KindText),
            "IP" => nameof(DeviceItem.IpSort),
            "MAC" => nameof(DeviceItem.Mac),
            "Last seen" => nameof(DeviceItem.LastSeenSort),
            "Latency" => nameof(DeviceItem.LatencySort),
            _ => header,
        };
        sortDirection = property == sortProperty && sortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;
        sortProperty = property;
        ApplySort();
    }

    void ApplySort()
    {
        var view = CollectionViewSource.GetDefaultView(vm.Devices);
        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new SortDescription(sortProperty, sortDirection));
        if (sortProperty != nameof(DeviceItem.IpSort))
            view.SortDescriptions.Add(new SortDescription(nameof(DeviceItem.IpSort), ListSortDirection.Ascending));
    }
}

public sealed class NullToVisibility : IValueConverter
{
    public static readonly NullToVisibility WhenNull = new(true);
    public static readonly NullToVisibility WhenNotNull = new(false);

    readonly bool visibleWhenNull;

    NullToVisibility(bool visibleWhenNull) => this.visibleWhenNull = visibleWhenNull;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value is null or "") == visibleWhenNull ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class InvertedBoolToVisibility : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
