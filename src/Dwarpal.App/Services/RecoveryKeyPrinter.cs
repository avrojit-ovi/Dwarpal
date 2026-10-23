using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Dwarpal.Core;
using Serilog;

namespace Dwarpal.App.Services;

/// <summary>
/// Recovery key ekbar print korar helper. Print dialog cancel korle false return kore.
/// </summary>
public static class RecoveryKeyPrinter
{
    public static bool Print(string recoveryKey, Window? owner = null)
    {
        try
        {
            var dialog = new PrintDialog();
            if (dialog.ShowDialog() != true)
            {
                return false;
            }

            var document = BuildDocument(recoveryKey, dialog);
            dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, "Dwarpal Recovery Key");

            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Recovery key print failed");

            MessageBox.Show(owner,
                "Print korte parlam na. Recovery key ta copy kore nite paren.",
                AppPaths.ProductName, MessageBoxButton.OK, MessageBoxImage.Warning);

            return false;
        }
    }

    private static FlowDocument BuildDocument(string recoveryKey, PrintDialog dialog)
    {
        var document = new FlowDocument
        {
            PageWidth = dialog.PrintableAreaWidth,
            PageHeight = dialog.PrintableAreaHeight,
            PagePadding = new Thickness(60),
            ColumnWidth = double.PositiveInfinity,
            FontFamily = new FontFamily("Segoe UI"),
        };

        document.Blocks.Add(new Paragraph(new Run("DWARPAL"))
        {
            FontSize = 28,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.Black,
        });

        document.Blocks.Add(new Paragraph(new Run("Account Recovery Key"))
        {
            FontSize = 18,
            Margin = new Thickness(0, 0, 0, 18),
        });

        document.Blocks.Add(new Paragraph(new Run(recoveryKey))
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 10, 0, 10),
        });

        document.Blocks.Add(new Paragraph(new Run(
            "Ei key ta offline rakho. PIN/password bhule gele ei key diye credential reset korte parbe. " +
            "Key harale protected app gulo uninstall kora chara recovery er ar upay thakbe na."))
        {
            FontSize = 12,
            Margin = new Thickness(0, 16, 0, 0),
        });

        document.Blocks.Add(new Paragraph(new Run($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}"))
        {
            FontSize = 11,
            Margin = new Thickness(0, 24, 0, 0),
        });

        return document;
    }
}
