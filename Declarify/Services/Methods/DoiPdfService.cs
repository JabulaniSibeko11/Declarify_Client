using Declarify.Data;
using Declarify.Models;
using Declarify.Services.PDF;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Declarify.Services.Methods
{
    public sealed class DoiPdfService : IDoiPdfService
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly DoiPdfOptions _opt;

        public DoiPdfService(ApplicationDbContext db, IWebHostEnvironment env, IOptions<DoiPdfOptions> opt)
        {
            _db = db;
            _env = env;
            _opt = opt.Value;
        }

        public async Task<(string FileName, string FullPath)> GenerateAndSaveAsync(FormSubmission submission, CancellationToken ct)
        {
            // Load everything we need
            var full = await _db.DOIFormSubmissions
                .Include(s => s.Task)!.ThenInclude(t => t.Employee)
                .Include(s => s.Task)!.ThenInclude(t => t.Template)
                .FirstAsync(s => s.SubmissionId == submission.SubmissionId, ct);

            var employee = full.Task!.Employee!;
            var template = full.Task!.Template!;

            // Parse TemplateConfig (same as CompleteForm.cshtml)
            var templateConfig = !string.IsNullOrWhiteSpace(template.TemplateConfig)
                ? JsonSerializer.Deserialize<TemplateConfig>(template.TemplateConfig, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                : new TemplateConfig();

            // Parse FormData JSON into dictionary
            var formDict = ParseFormDataToDict(full.FormData);

            // Root folder
            var safeEmployee = SafeName(employee.Full_Name ?? $"Employee_{employee.EmployeeId}");
            var root = _opt.RootPath ?? @"C:\Declarify\DOI PDFs";
            var folder = Path.Combine(root, safeEmployee);
            Directory.CreateDirectory(folder);

            var now = DateTime.UtcNow;
            var fileName = $"DOI-{now:yyyyMMdd-HHmmss}-SUB{full.SubmissionId}-{employee.EmployeeId}.pdf";
            var fullPath = Path.Combine(folder, fileName);

            // Logo path
            var logoPath = Path.Combine(_env.WebRootPath, "Images", "Declarify Logo.png");
            var hasLogo = System.IO.File.Exists(logoPath);

            // Brand tokens
            var navy = "#081B38";
            var cyan = "#00C2CB";
            var muted = "#64748B";
            var canvas = "#F8FAFC";

            var sections = (templateConfig?.Sections ?? new List<SectionConfig>())
                .OrderBy(s => s.SectionOrder)
                .ToList();

            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(28);
                    page.PageColor(canvas);

                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Inter"));

                    // ===== Header =====
                    page.Header().Column(h =>
                    {
                        h.Item().Row(row =>
                        {
                            if (hasLogo)
                                row.ConstantItem(150).Height(42).AlignMiddle().Image(logoPath);

                            row.RelativeItem().AlignRight().AlignMiddle().Column(col =>
                            {
                                col.Item().Text("Declaration of Interest (DOI)")
                                    .FontFamily("Montserrat")
                                    .FontSize(18).SemiBold()
                                    .FontColor(navy);

                                col.Item().Text("Public Sector Integrity. Clarified.")
                                    .FontSize(10)
                                    .FontColor(muted);

                                col.Item().PaddingTop(2)
                                    .Text($"Generated: {now:dd MMM yyyy HH:mm} UTC")
                                    .FontSize(9)
                                    .FontColor(muted);
                            });
                        });

                        h.Item().PaddingTop(10).Height(3).Background(cyan);
                    });

                    // ===== Content =====
                    page.Content().PaddingTop(16).Column(col =>
                    {
                        col.Spacing(14);

                        // Card style
                        IContainer Card(IContainer c) =>
                            c.Background(Colors.White)
                             .Padding(14)
                             .Border(1).BorderColor(Colors.Grey.Lighten2)
                             .CornerRadius(12);

                        // Employee details card
                        col.Item().Element(Card).Column(c =>
                        {
                            c.Spacing(8);

                            c.Item().Text("Your Details")
                                .FontFamily("Montserrat")
                                .FontSize(13).SemiBold()
                                .FontColor(navy);

                            c.Item().Table(t =>
                            {
                                t.ColumnsDefinition(cols =>
                                {
                                    cols.ConstantColumn(160);
                                    cols.RelativeColumn();
                                });

                                KV(t, "Full Name", employee.Full_Name, navy);
                                KV(t, "Employee No.", employee.EmployeeNumber, navy);
                                KV(t, "Email", employee.Email_Address, navy);
                                KV(t, "Department", employee.Department, navy);
                                KV(t, "Position", employee.Position, navy);
                                KV(t, "Template", template.TemplateName, navy);
                                KV(t, "Submitted", full.Submitted_Date.ToString("dd MMM yyyy HH:mm"), navy);
                                KV(t, "Status", full.Status, navy);
                            });
                        });

                        // Render each section like CompleteForm
                        for (int i = 0; i < sections.Count; i++)
                        {
                            var sec = sections[i];
                            var sectionNo = i + 1;

                            col.Item().Element(Card).Column(sc =>
                            {
                                sc.Spacing(10);

                                // Section header
                                sc.Item().Row(r =>
                                {
                                    r.ConstantItem(40)
                                     .Height(28)
                                     .AlignMiddle()
                                     .AlignCenter()
                                     .Background(cyan)
                                     .CornerRadius(8)
                                     .Text(sectionNo.ToString())
                                     .FontFamily("Montserrat")
                                     .SemiBold()
                                     .FontColor(navy);

                                    r.RelativeItem().PaddingLeft(10).Column(t =>
                                    {
                                        t.Item().Text(sec.SectionTitle ?? $"Section {sectionNo}")
                                            .FontFamily("Montserrat")
                                            .FontSize(13)
                                            .SemiBold()
                                            .FontColor(navy);

                                        if (!string.IsNullOrWhiteSpace(sec.Disclaimer))
                                            t.Item().Text(sec.Disclaimer).FontSize(9).FontColor(muted);
                                    });
                                });

                                var fields = (sec.Fields ?? new List<FieldConfig>())
                                    .OrderBy(f => f.Order)
                                    .ToList();

                                foreach (var field in fields)
                                {
                                    RenderField(sc, field, formDict, navy, cyan, muted, employee);
                                }
                            });
                        }
                    });

                    // ===== Footer =====
                    page.Footer().PaddingTop(10).Column(f =>
                    {
                        f.Item().Height(2).Background(Colors.Grey.Lighten2);

                        f.Item().PaddingTop(6).Row(r =>
                        {
                            r.RelativeItem().Text("Declarify • Compliance & Disclosure Hub")
                                .FontSize(9).FontColor(muted);

                            r.ConstantItem(260).AlignRight()
                                .Text("Support: valuationenquiries@joburg.org.za")
                                .FontSize(9).FontColor(muted);
                        });
                    });
                });
            }).GeneratePdf();

            await System.IO.File.WriteAllBytesAsync(fullPath, pdfBytes, ct);
            return (fileName, fullPath);
        }

        // =========================
        // Field rendering (same idea as Razor switch)
        // =========================
        private static void RenderField(ColumnDescriptor sc, FieldConfig field, Dictionary<string, JsonElement> form, string navy, string cyan, string muted, Employee employee)
        {
            var type = (field.FieldType ?? "").Trim().ToLowerInvariant();
            var label = field.FieldLabel ?? field.FieldId;

            switch (type)
            {
                case "heading":
                    sc.Item().PaddingTop(6).Text(label)
                        .FontFamily("Montserrat")
                        .FontSize(12).SemiBold()
                        .FontColor(navy);
                    return;

                case "paragraph":
                    sc.Item().Text(!string.IsNullOrWhiteSpace(field.HelpText) ? field.HelpText : label)
                        .FontSize(10)
                        .FontColor(muted);
                    return;

                case "divider":
                    sc.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    return;

                case "signature":
                    sc.Item().PaddingTop(6).Text(label).SemiBold().FontColor(navy);

                    // value is usually the signature image URL/path
                    var sig = GetString(form, field.FieldId);

                    if (string.IsNullOrWhiteSpace(sig))
                        sig = employee.Signature_Picture; // fallback if your employee model has it

                    if (!string.IsNullOrWhiteSpace(sig))
                    {
                        // If it's a web path like "/uploads/..", you can resolve it later.
                        // For now we show the text if it’s not a physical file.
                        if (Uri.TryCreate(sig, UriKind.Absolute, out _) == false && System.IO.File.Exists(sig))
                        {
                            sc.Item().PaddingTop(6)
                                .Height(90)
                                .Image(sig);
                        }
                        else
                        {
                            sc.Item().PaddingTop(4)
                                .Text("Signature captured in profile (image reference stored).")
                                .FontSize(9).FontColor(muted);
                        }
                    }
                    else
                    {
                        sc.Item().Text("-").FontColor(muted);
                    }
                    return;

                case "table":
                    RenderSimpleTable(sc, field, form, navy, cyan, muted);
                    return;

                case "advancedtable":
                    RenderAdvancedTable(sc, field, form, navy, cyan, muted);
                    return;

                // Default input-like fields: show label + value
                default:
                    sc.Item().PaddingTop(6).Text(label).SemiBold().FontColor(navy);

                    if (!string.IsNullOrWhiteSpace(field.HelpText))
                        sc.Item().Text(field.HelpText).FontSize(9).FontColor(muted);

                    var val = GetValueForField(type, form, field.FieldId);
                    sc.Item().Text(string.IsNullOrWhiteSpace(val) ? "-" : val);
                    return;
            }
        }

        private static void RenderSimpleTable(ColumnDescriptor sc, FieldConfig field, Dictionary<string, JsonElement> form, string navy, string cyan, string muted)
        {
            var label = field.FieldLabel ?? field.FieldId;

            sc.Item().PaddingTop(6).Text(label).SemiBold().FontColor(navy);

            if (!string.IsNullOrWhiteSpace(field.HelpText))
                sc.Item().Text(field.HelpText).FontSize(9).FontColor(muted);

            var columns = field.TableConfig?.Columns?.Any() == true
                ? field.TableConfig.Columns
                : new List<string> { "Description", "Details", "Value" };

            // Read rows based on your naming: {fieldId}_row{r}_col{c}
            var rows = ReadSimpleTableRows(form, field.FieldId, columns.Count);

            sc.Item().PaddingTop(6).Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    foreach (var _ in columns) c.RelativeColumn();
                });

                t.Header(h =>
                {
                    foreach (var colName in columns)
                        h.Cell().Element(x => PdfHeaderCell(x, navy, cyan)).Text(colName);
                });

                if (rows.Count == 0)
                {
                    // show 1 empty row
                    foreach (var _ in columns)
                        t.Cell().Element(PdfBodyCell).Text("-");
                    return;
                }

                foreach (var r in rows)
                {
                    for (int i = 0; i < columns.Count; i++)
                        t.Cell().Element(PdfBodyCell).Text(string.IsNullOrWhiteSpace(r[i]) ? "-" : r[i]);
                }
            });
        }

        private static void RenderAdvancedTable(ColumnDescriptor sc, FieldConfig field, Dictionary<string, JsonElement> form, string navy, string cyan, string muted)
        {
            var label = field.FieldLabel ?? field.FieldId;

            sc.Item().PaddingTop(6).Text(label).SemiBold().FontColor(navy);

            if (!string.IsNullOrWhiteSpace(field.HelpText))
                sc.Item().Text(field.HelpText).FontSize(9).FontColor(muted);

            // From your Razor logic
            var totalCols = field.GridColumns ?? 3;
            var cells = field.Cells ?? new List<CellConfig>();
            var visibleCells = cells.Where(c => !c.Hidden).OrderBy(c => c.Row).ThenBy(c => c.Col).ToList();

            // Header rows (IsHeader = true)
            var headerRows = visibleCells.Where(c => c.IsHeader)
                .GroupBy(c => c.Row)
                .OrderBy(g => g.Key)
                .ToList();

            // The repeatable data-entry row is Row == 2 in your view
            // Read repeatable rows from form keys: {fieldId}_datarow{idx}_col{col}
            var dataRows = ReadAdvancedDataRows(form, field.FieldId, totalCols);

            // Footer rows fixed: keys {fieldId}_fixedrow{row}_col{col}
            var footerRows = ReadAdvancedFixedRows(form, field.FieldId, visibleCells.Where(c => c.Row >= 3 && !c.IsHeader).ToList());

            // Render a table-like layout:
            // - First render headers as a “header strip”
            // - Then render data table rows
            // - Then render footer fixed questions as key/value blocks

            // 1) Header strip (simple representation)
            if (headerRows.Any())
            {
                sc.Item().PaddingTop(6).Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        for (int i = 0; i < totalCols; i++) c.RelativeColumn();
                    });

                    // We'll display the first header row’s titles across columns (good enough and clean)
                    var firstHeader = headerRows.First().OrderBy(x => x.Col).ToList();
                    for (int i = 0; i < totalCols; i++)
                    {
                        var cell = firstHeader.FirstOrDefault(x => x.Col == i);
                        var title = cell?.ColumnName ?? "";
                        t.Cell().Element(x => PdfHeaderCell(x, navy, cyan)).Text(string.IsNullOrWhiteSpace(title) ? "-" : title);
                    }
                });
            }

            // 2) Data rows table
            sc.Item().PaddingTop(6).Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    for (int i = 0; i < totalCols; i++) c.RelativeColumn();
                });

                if (dataRows.Count == 0)
                {
                    for (int i = 0; i < totalCols; i++)
                        t.Cell().Element(PdfBodyCell).Text("-");
                    return;
                }

                foreach (var r in dataRows)
                {
                    for (int col = 0; col < totalCols; col++)
                        t.Cell().Element(PdfBodyCell).Text(string.IsNullOrWhiteSpace(r[col]) ? "-" : r[col]);
                }
            });

            // 3) Footer fixed rows (render as question -> answer blocks)
            if (footerRows.Count > 0)
            {
                sc.Item().PaddingTop(10).Text("Additional Questions").SemiBold().FontColor(navy);

                foreach (var kv in footerRows)
                {
                    sc.Item().PaddingTop(4).Table(t =>
                    {
                        t.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(220);
                            cols.RelativeColumn();
                        });

                        t.Cell().Element(PdfBodyCell).Text(kv.Key).SemiBold().FontColor(navy);
                        t.Cell().Element(PdfBodyCell).Text(string.IsNullOrWhiteSpace(kv.Value) ? "-" : kv.Value);
                    });
                }
            }
        }

        // =========================
        // Value extraction
        // =========================
        private static string GetValueForField(string type, Dictionary<string, JsonElement> form, string fieldId)
        {
            var raw = GetString(form, fieldId);

            // checkbox stores boolean true/false
            if (type is "checkbox")
            {
                if (TryGetBool(form, fieldId, out var b))
                    return b ? "Yes" : "No";
            }

            // boolean/radio are just string values already
            return raw ?? "";
        }

        private static string? GetString(Dictionary<string, JsonElement> form, string key)
        {
            if (!form.TryGetValue(key, out var el)) return null;

            try
            {
                return el.ValueKind switch
                {
                    JsonValueKind.String => el.GetString(),
                    JsonValueKind.Number => el.ToString(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => null,
                    _ => el.ToString()
                };
            }
            catch
            {
                return el.ToString();
            }
        }

        private static bool TryGetBool(Dictionary<string, JsonElement> form, string key, out bool value)
        {
            value = false;
            if (!form.TryGetValue(key, out var el)) return false;

            try
            {
                if (el.ValueKind == JsonValueKind.True) { value = true; return true; }
                if (el.ValueKind == JsonValueKind.False) { value = false; return true; }

                if (el.ValueKind == JsonValueKind.String)
                {
                    var s = el.GetString();
                    if (bool.TryParse(s, out var b)) { value = b; return true; }
                    if (string.Equals(s, "yes", StringComparison.OrdinalIgnoreCase)) { value = true; return true; }
                    if (string.Equals(s, "no", StringComparison.OrdinalIgnoreCase)) { value = false; return true; }
                }
            }
            catch { }

            return false;
        }

        // table rows: fieldId_row{r}_col{c}
        private static List<string[]> ReadSimpleTableRows(Dictionary<string, JsonElement> form, string fieldId, int colCount)
        {
            var rows = new Dictionary<int, string[]>();

            var rx = new Regex($"^{Regex.Escape(fieldId)}_row(\\d+)_col(\\d+)$", RegexOptions.IgnoreCase);

            foreach (var kv in form)
            {
                var m = rx.Match(kv.Key);
                if (!m.Success) continue;

                var r = int.Parse(m.Groups[1].Value);
                var c = int.Parse(m.Groups[2].Value);

                if (c < 0 || c >= colCount) continue;

                if (!rows.TryGetValue(r, out var arr))
                {
                    arr = new string[colCount];
                    rows[r] = arr;
                }

                arr[c] = GetString(form, kv.Key) ?? "";
            }

            return rows.OrderBy(x => x.Key).Select(x => x.Value).ToList();
        }

        // advanced data rows: fieldId_datarow{r}_col{c}
        private static List<string[]> ReadAdvancedDataRows(Dictionary<string, JsonElement> form, string fieldId, int colCount)
        {
            var rows = new Dictionary<int, string[]>();

            var rx = new Regex($"^{Regex.Escape(fieldId)}_datarow(\\d+)_col(\\d+)$", RegexOptions.IgnoreCase);

            foreach (var kv in form)
            {
                var m = rx.Match(kv.Key);
                if (!m.Success) continue;

                var r = int.Parse(m.Groups[1].Value);
                var c = int.Parse(m.Groups[2].Value);

                if (c < 0 || c >= colCount) continue;

                if (!rows.TryGetValue(r, out var arr))
                {
                    arr = new string[colCount];
                    rows[r] = arr;
                }

                arr[c] = GetString(form, kv.Key) ?? "";
            }

            return rows.OrderBy(x => x.Key).Select(x => x.Value).ToList();
        }

        // advanced fixed rows: fieldId_fixedrow{row}_col{col}
        private static Dictionary<string, string> ReadAdvancedFixedRows(Dictionary<string, JsonElement> form, string fieldId, List<CellConfig> footerCells)
        {
            // Key = cell.ColumnName (question)
            // Value = stored answer in fixedrow{row}_col{col}
            var result = new Dictionary<string, string>();

            foreach (var cell in footerCells)
            {
                var key = $"{fieldId}_fixedrow{cell.Row}_col{cell.Col}";
                var answer = GetString(form, key) ?? "";

                var question = cell.ColumnName ?? $"Row {cell.Row} Col {cell.Col}";
                if (!result.ContainsKey(question))
                    result[question] = answer;
            }

            return result;
        }

        // =========================
        // QuestPDF cell styles
        // =========================
        private static void KV(TableDescriptor t, string label, string? value, string navy)
        {
            t.Cell().Element(PdfBodyCell).Text(label).SemiBold().FontColor(navy);
            t.Cell().Element(PdfBodyCell).Text(string.IsNullOrWhiteSpace(value) ? "-" : value);
        }

        private static IContainer PdfHeaderCell(IContainer c, string navy, string cyan)
        {
            return c
                .Background(cyan)
                .Padding(8)
                .Border(1)
                .BorderColor(Colors.Grey.Lighten2)
                .DefaultTextStyle(x => x.SemiBold().FontColor(navy));
        }

        private static IContainer PdfBodyCell(IContainer c)
        {
            return c
                .Padding(8)
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Lighten3);
        }

        private static string SafeName(string name)
        {
            foreach (var ch in Path.GetInvalidFileNameChars())
                name = name.Replace(ch, '_');

            name = name.Trim();
            return name.Length > 80 ? name[..80] : name;
        }

        private static Dictionary<string, JsonElement> ParseFormDataToDict(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new Dictionary<string, JsonElement>();

            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return dict ?? new Dictionary<string, JsonElement>();
            }
            catch
            {
                return new Dictionary<string, JsonElement>();
            }
        }

        #region TemplateConfig Models (copy from your view if not already shared)
        public class TemplateConfig
        {
            public List<SectionConfig> Sections { get; set; } = new();
        }

        public class SectionConfig
        {
            public string SectionId { get; set; } = string.Empty;
            public string SectionTitle { get; set; } = string.Empty;
            public string? Disclaimer { get; set; }
            public int SectionOrder { get; set; }
            public List<FieldConfig> Fields { get; set; } = new();
        }

        public class FieldConfig
        {
            public string FieldId { get; set; } = "";
            public string? FieldLabel { get; set; }
            public string? FieldType { get; set; }
            public bool Required { get; set; }
            public int Order { get; set; }
            public string? ConditionalOn { get; set; }
            public List<string>? Options { get; set; }
            public string? Placeholder { get; set; }
            public string? HelpText { get; set; }
            public TableConfig? TableConfig { get; set; }
            public List<ColumnConfig>? Columns { get; set; }
            public int? Rows { get; set; }
            public int? GridColumns { get; set; }
            public List<CellConfig>? Cells { get; set; }
            public ValidationConfig? Validation { get; set; }
        }

        public class TableConfig
        {
            public List<string> Columns { get; set; } = new();
            public int MinRows { get; set; }
            public bool AllowAddRows { get; set; }
        }

        public class ColumnConfig
        {
            public string? Name { get; set; }
            public string Type { get; set; } = "text";
        }

        public class CellConfig
        {
            public int Row { get; set; }
            public int Col { get; set; }
            public int Rowspan { get; set; }
            public int Colspan { get; set; }
            public string? ColumnId { get; set; }
            public string? ColumnName { get; set; }
            public bool IsHeader { get; set; }
            public bool IsMerged { get; set; }
            public bool Hidden { get; set; }
        }

        public class ValidationConfig
        {
            public string? Min { get; set; }
            public string? Max { get; set; }
            public string? FileTypes { get; set; }
            public int? MaxSize { get; set; }
            public bool? AllowTyped { get; set; }
        }
        #endregion
    }
}
