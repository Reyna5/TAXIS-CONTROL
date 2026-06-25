using System.IO;
using System.Printing;
using System.Text;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ControlTaxiWeb.Models.PosTriton;
using Microsoft.Win32;

namespace ControlTaxi.Desktop.Services;

public sealed class TicketPrintService
{
    public string BuildDejadaReceiptContent(PosDejadaTicketViewModel ticket)
    {
        const int width = 32;
        static string Line(char value = '-') => new(value, width);
        static string Clean(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        static string First(params string?[] values) =>
            values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? "-";
        static string Center(string value)
        {
            value = Clean(value);
            if (value.Length >= width)
                return value[..width];
            var left = (width - value.Length) / 2;
            return new string(' ', left) + value;
        }

        static string Pair(string label, string value)
        {
            label = Clean(label).ToUpperInvariant();
            value = Clean(value);
            var prefix = $"{label}: ";
            var maxValue = Math.Max(1, width - prefix.Length);
            return prefix + (value.Length > maxValue ? value[..maxValue] : value);
        }

        var folio = Clean(First(ticket.FolioOperacion, ticket.FolioControl, ticket.FolioApp, ticket.FolioPos));
        var folioApp = Clean(ticket.FolioApp);
        var operacion = Clean(ticket.FolioOperacion);
        return string.Join(Environment.NewLine, new[]
        {
            Center("CONTROL TAXI"),
            Center("PAGO DE DEJADA"),
            Line(),
            Pair("Ticket", ticket.Ticket),
            Pair("Folio", folio),
            folioApp != folio ? Pair("Folio app", ticket.FolioApp) : string.Empty,
            operacion != folio && operacion != folioApp ? Pair("Operacion", ticket.FolioOperacion) : string.Empty,
            Pair("Fecha viaje", ticket.FechaViaje),
            Pair("Fecha pago", ticket.FechaPago),
            Line(),
            Pair("Taxista", ticket.Taxista),
            Pair("Vendedor", ticket.Vendedor),
            Pair("Gafete", ticket.Gafete),
            Pair("Unidad", ticket.Unidad),
            Pair("Placas", ticket.Placas),
            Pair("Telefono", ticket.Telefono),
            Pair("Nacionalidad", ticket.Nacionalidad),
            Pair("Transporte", ticket.Transporte),
            Pair("Hotel", ticket.Hotel),
            Pair("Destino", ticket.Destino),
            Pair("Pax", ticket.Pax.ToString()),
            Line(),
            Pair("Importe", ticket.Importe.ToString("C2")),
            Pair("Usuario", ticket.Usuario),
            Pair("Estatus", ticket.Estatus),
            Line(),
            Center("CONSERVE ESTE COMPROBANTE"),
            "________________________",
            Center("FIRMA DEL TAXISTA")
        }.Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    public void PrintText(string ticketText)
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true)
            return;

        var document = new FlowDocument
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10,
            PagePadding = new System.Windows.Thickness(4),
            ColumnWidth = double.PositiveInfinity
        };
        document.Blocks.Add(new Paragraph(new Run(ticketText))
        {
            Margin = new System.Windows.Thickness(0)
        });

        var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
        dialog.PrintDocument(paginator, "Ticket de dejada");
    }

    public async Task<string?> SaveTicketAsync(PosDejadaTicketViewModel ticket)
    {
        var folio = string.Join("_", new[] { ticket.Ticket, ticket.FolioControl, ticket.FolioApp }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim()));
        var dialog = new SaveFileDialog
        {
            Title = "Guardar ticket de dejada",
            FileName = $"ticket_dejada_{folio}_{DateTime.Now:yyyyMMddHHmmss}.txt",
            Filter = "Texto (*.txt)|*.txt"
        };

        if (dialog.ShowDialog() != true)
            return null;

        await File.WriteAllTextAsync(dialog.FileName, BuildDejadaReceiptContent(ticket), Encoding.UTF8);
        return dialog.FileName;
    }
}
