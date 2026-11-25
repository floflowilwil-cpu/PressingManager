using System.Windows;
using System.Windows.Media;
using PressingManager.App.Localization;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;

namespace PressingManager.App.Views;

/// <summary>
/// Ecran USSD Mobile Money : declenche la requete Campay vers le telephone du payeur (mode Live)
/// ou simule la saisie du code PIN USSD (mode Simulation).
/// </summary>
public partial class MobileMoneyPromptWindow : Window
{
    private readonly MobileMoneyChargeRequest _request;
    private readonly IMobileMoneyGateway _gateway;
    private CancellationTokenSource? _cts;

    public MobileMoneyPromptWindow(MobileMoneyChargeRequest request, IMobileMoneyGateway gateway)
    {
        InitializeComponent();

        _request = request ?? throw new ArgumentNullException(nameof(request));
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));

        ConfigureAppearance();

        Loaded += async (_, _) =>
        {
            if (_gateway.IsLiveMode)
            {
                await RunGatewayRequestAsync();
            }
            else
            {
                PinBox.Focus();
            }
        };

        Closed += (_, _) => _cts?.Cancel();
    }

    public MobileMoneyChargeResult? Result { get; private set; }

    private void ConfigureAppearance()
    {
        var isMtn = _request.Method == PaymentMethod.MtnMobileMoney;
        HeaderBorder.Background = new SolidColorBrush(isMtn ? Color.FromRgb(255, 204, 0) : Color.FromRgb(255, 121, 0));
        var headerFg = isMtn ? new SolidColorBrush(Color.FromRgb(15, 42, 74)) : Brushes.White;

        TxtOperatorTitle.Foreground = headerFg;
        TxtOrderSummary.Foreground = headerFg;
        TxtOperatorTitle.Text = isMtn ? "MTN Mobile Money (*126#)" : "Orange Money (#150#)";
        TxtOrderSummary.Text = $"{_request.OrderNumber} — {_gateway.ActiveProviderName}";
        TxtAmount.Text = MoneyFormatter.FormatFcfa(_request.Amount);

        try
        {
            var normalized = _gateway.NormalizeCameroonPhone(_request.PhoneNumber);
            TxtPhoneNumber.Text = $"+{normalized[..3]} {normalized[3..]}";
        }
        catch (BusinessRuleException)
        {
            TxtPhoneNumber.Text = _request.PhoneNumber;
        }

        if (_gateway.IsLiveMode)
        {
            LivePanel.Visibility = Visibility.Visible;
            BtnConfirm.Visibility = Visibility.Collapsed;
        }
        else
        {
            SimulationPanel.Visibility = Visibility.Visible;
        }
    }

    private async void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        var pin = PinBox.Password.Trim();
        if (pin.Length < 4 || !pin.All(char.IsAsciiDigit))
        {
            TxtStatusMessage.Text = Loc.Instance["Ussd_InvalidPin"];
            TxtStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(192, 57, 43));
            return;
        }

        await RunGatewayRequestAsync();
    }

    private async void RetryButton_Click(object sender, RoutedEventArgs e)
    {
        BtnRetry.Visibility = Visibility.Collapsed;
        await RunGatewayRequestAsync();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        DialogResult = false;
        Close();
    }

    private async Task RunGatewayRequestAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        TxtStatusMessage.Text = string.Empty;
        BtnConfirm.IsEnabled = false;
        BtnRetry.Visibility = Visibility.Collapsed;

        var progress = new Progress<string>(status =>
        {
            TxtLiveBanner.Text = status;
            TxtStatusMessage.Text = status;
            TxtStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(15, 42, 74));
        });

        try
        {
            var result = await _gateway.RequestPaymentAsync(_request, progress, _cts.Token);
            if (result.Success)
            {
                Result = result;
                DialogResult = true;
                Close();
                return;
            }

            TxtStatusMessage.Text = result.StatusMessage;
            TxtStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(192, 57, 43));
            BtnRetry.Visibility = _gateway.IsLiveMode ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (BusinessRuleException ex)
        {
            TxtStatusMessage.Text = ex.Message;
            TxtStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(192, 57, 43));
        }
        finally
        {
            BtnConfirm.IsEnabled = true;
        }
    }
}
