using System;
using GBV_Shield.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;

namespace GBV_Shield.Features.Reporting
{
    public partial class ReportPage : ContentPage
    {
        private readonly ReportsService _reports;

        // ✅ Default constructor for Shell navigation
        public ReportPage() : this(App.Services.GetRequiredService<ReportsService>()) { }

        // ✅ Main constructor with DI support
        public ReportPage(ReportsService reports)
        {
            InitializeComponent();
            _reports = reports ?? throw new ArgumentNullException(nameof(reports));
        }

        private async void OnSubmitReportClicked(object sender, EventArgs e)
        {
            string text = ReportEditor.Text ?? string.Empty;
            string type = TypePicker.SelectedItem?.ToString() ?? "general";

            if (string.IsNullOrWhiteSpace(text))
            {
                await DisplayAlertAsync("Error", "Please enter some details before submitting.", "OK");
                return;
            }

            try
            {
                var status = await _reports.SubmitReportAsync(text, type);

                ReportEditor.Text = string.Empty;
                TypePicker.SelectedIndex = -1;

                if (status == ReportSubmissionStatus.Sent)
                {
                    await DisplayAlertAsync("Thank you", "Your report has been securely submitted.", "OK");
                }
                else
                {
                    await DisplayAlertAsync(
                        "Saved for delivery",
                        "Your report is safely stored on this device and will be sent when a connection is available.",
                        "OK");
                }
            }
            catch (Exception)
            {
                await DisplayAlertAsync("Submission failed", "Please check your connection and try again.", "OK");
            }
        }
    }
}
