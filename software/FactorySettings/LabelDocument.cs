using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace FactorySettings {
    internal static class LabelDocument {
        private const string GettingStartedUrl = "https://sigcoreuc.com/getting-started.html";

        private const int WdAlignParagraphLeft = 0;
        private const int WdAlignParagraphCenter = 1;
        private const int WdAlignParagraphRight = 2;
        private const int WdCellAlignVerticalCenter = 1;
        private const int WdRowHeightExactly = 2;
        private const int WdPageBreak = 7;

        public static string CreateAndOpen(IReadOnlyList<FactoryLabelRecord> labels) {
            if (labels == null || labels.Count == 0)
                throw new InvalidOperationException("There are no saved labels to create.");

            Type? wordType = Type.GetTypeFromProgID("Word.Application");
            if (wordType == null)
                throw new InvalidOperationException("Microsoft Word is not installed on this computer.");

            string qrPath = Path.Combine(AppContext.BaseDirectory, "getting-started-qr.png");
            if (!File.Exists(qrPath))
                throw new FileNotFoundException("The Getting Started QR image is missing.", qrPath);

            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "SigCore Labels");
            Directory.CreateDirectory(folder);

            string outputPath = Path.Combine(
                folder,
                $"SigCore-Labels-{DateTime.Now:yyyyMMdd-HHmmss}.docx");

            dynamic? word = null;
            dynamic? document = null;

            try {
                word = Activator.CreateInstance(wordType);
                if (word == null)
                    throw new InvalidOperationException("Microsoft Word could not be started.");

                word.Visible = false;
                document = word.Documents.Add();

                // Letter page, compact margins. The 2 x 5 label grid fits cleanly
                // inside an 8.0 x 10.0 inch printable area.
                document.PageSetup.TopMargin = InchesToPoints(0.50);
                document.PageSetup.BottomMargin = InchesToPoints(0.50);
                document.PageSetup.LeftMargin = InchesToPoints(0.25);
                document.PageSetup.RightMargin = InchesToPoints(0.25);

                int pageCount = (labels.Count + 9) / 10;

                for (int page = 0; page < pageCount; page++) {
                    if (page > 0) {
                        dynamic breakRange = document.Content;
                        breakRange.Collapse(0); // wdCollapseEnd
                        breakRange.InsertBreak(WdPageBreak);
                    }

                    dynamic pageRange = document.Content;
                    pageRange.Collapse(0); // wdCollapseEnd
                    dynamic outer = document.Tables.Add(pageRange, 5, 2);
                    outer.AllowAutoFit = false;
                    // Light gray cut guides around every label and between labels.
                    // Set the six actual table borders individually. Word COM can throw
                    // "Value out of range" when OutsideColor/InsideColor are assigned
                    // directly to the Borders collection.
                    SetCutGuideBorders(outer);
                    outer.TopPadding = 0;
                    outer.BottomPadding = 0;
                    outer.LeftPadding = 0;
                    outer.RightPadding = 0;

                    for (int row = 1; row <= 5; row++) {
                        outer.Rows.Item(row).HeightRule = WdRowHeightExactly;
                        outer.Rows.Item(row).Height = InchesToPoints(2.0);
                    }

                    for (int col = 1; col <= 2; col++)
                        outer.Columns.Item(col).Width = InchesToPoints(4.0);

                    for (int slot = 0; slot < 10; slot++) {
                        int labelIndex = page * 10 + slot;
                        if (labelIndex >= labels.Count)
                            break;

                        int row = slot / 2 + 1;
                        int col = slot % 2 + 1;
                        FillLabel(document, outer.Cell(row, col), labels[labelIndex], qrPath);
                    }
                }

                document.SaveAs2(outputPath);
                word.Visible = true;
                word.Activate();
                document.Activate();

                // Ownership transfers to the visible Word instance.
                document = null;
                word = null;

                return outputPath;

            } catch {
                try { document?.Close(false); } catch { }
                try { word?.Quit(false); } catch { }
                throw;
            } finally {
                if (document != null && Marshal.IsComObject(document))
                    Marshal.FinalReleaseComObject(document);
                if (word != null && Marshal.IsComObject(word))
                    Marshal.FinalReleaseComObject(word);
            }
        }


        private static void SetCutGuideBorders(dynamic table) {
            const int WdLineStyleSingle = 1;
            const int WdGray25 = 16;

            // Use the Borders collection's supported table-level properties.
            // ColorIndex is much more reliable through Word COM than assigning
            // raw RGB values or addressing individual border types.
            table.Borders.Enable = 1;
            table.Borders.OutsideLineStyle = WdLineStyleSingle;
            table.Borders.InsideLineStyle = WdLineStyleSingle;
            table.Borders.OutsideColorIndex = WdGray25;
            table.Borders.InsideColorIndex = WdGray25;
        }

        private static void FillLabel(dynamic document, dynamic outerCell, FactoryLabelRecord label, string qrPath) {
            string stage = "start";
            try {
                stage = "outer cell";
                outerCell.VerticalAlignment = WdCellAlignVerticalCenter;

            stage = "clear label cell";
            dynamic labelRange = outerCell.Range.Duplicate;
            labelRange.End -= 1; // exclude end-of-cell marker
            labelRange.Text = "";

            stage = "create label layout table";
            dynamic layout = document.Tables.Add(labelRange, 1, 2);
            layout.AllowAutoFit = false;
            layout.Borders.Enable = 0;
            layout.TopPadding = 0;
            layout.BottomPadding = 0;
            layout.LeftPadding = 0;
            layout.RightPadding = 0;
            stage = "size label layout columns";
            layout.Columns.Item(1).Width = InchesToPoints(3.00);
            layout.Columns.Item(2).Width = InchesToPoints(0.80);

            dynamic left = layout.Cell(1, 1);
            dynamic right = layout.Cell(1, 2);
            left.VerticalAlignment = WdCellAlignVerticalCenter;
            right.VerticalAlignment = WdCellAlignVerticalCenter;

            // Heading area.
            stage = "write heading";
            dynamic leftRange = left.Range.Duplicate;
            leftRange.End -= 1;
            leftRange.Text = "SigCore UC\r\nen Z em Product\r";

            dynamic title = left.Range.Paragraphs.Item(1).Range;
            title.Font.Name = "Arial";
            title.Font.Size = 16;
            title.Font.Bold = 1;
            title.ParagraphFormat.Alignment = WdAlignParagraphCenter;
            title.ParagraphFormat.SpaceAfter = 0;

            dynamic brand = left.Range.Paragraphs.Item(2).Range;
            brand.Font.Name = "Segoe UI Light";
            brand.Font.Size = 8.5;
            brand.Font.Bold = 0;
            brand.ParagraphFormat.Alignment = WdAlignParagraphCenter;
            brand.ParagraphFormat.SpaceAfter = 2;

            // Add a true 2-column detail table after the heading. This is what keeps
            // every inner edge aligned regardless of the length of the values.
            dynamic detailsAnchor = left.Range.Duplicate;
            detailsAnchor.End -= 1;
            detailsAnchor.Collapse(0); // end
            stage = "create detail table";
            dynamic details = document.Tables.Add(detailsAnchor, 5, 2);
            details.AllowAutoFit = false;
            details.Borders.Enable = 0;
            details.TopPadding = 0;
            details.BottomPadding = 0;
            details.LeftPadding = 0;
            details.RightPadding = 0;

            // Use the available width instead of wasting space on a separate colon column.
            // The colon is part of the right-aligned label, so it sits directly on the
            // inner edge between the two columns.
            stage = "size detail columns";
            details.Columns.Item(1).Width = InchesToPoints(1.00);
            details.Columns.Item(2).Width = InchesToPoints(1.90);

            stage = "populate detail rows";
            SetDetailRow(details, 1, "Getting Started", GettingStartedUrl);
            SetDetailRow(details, 2, "Local", label.LocalUrl);
            SetDetailRow(details, 3, "MAC Address", label.MacAddress);
            SetDetailRow(details, 4, "Serial Number", label.SerialNumber);
            SetDetailRow(details, 5, "Revision", label.Revision);

            stage = "add QR code";
            dynamic qrRange = right.Range.Duplicate;
            qrRange.End -= 1;
            qrRange.ParagraphFormat.Alignment = WdAlignParagraphCenter;
            qrRange.InlineShapes.AddPicture(qrPath, false, true);
            dynamic qr = right.Range.InlineShapes.Item(1);
            qr.LockAspectRatio = true;
            qr.Width = InchesToPoints(0.72);
            } catch (COMException ex) {
                throw new InvalidOperationException(
                    $"Word label generation failed during {stage}: {ex.Message}", ex);
            }
        }

        private static void SetDetailRow(dynamic table, int row, string label, string value) {
            dynamic labelCell = table.Cell(row, 1);
            dynamic valueCell = table.Cell(row, 2);

            labelCell.Range.Text = label + ":";
            valueCell.Range.Text = value ?? "";

            labelCell.Range.Font.Name = "Arial";
            valueCell.Range.Font.Name = "Arial";
            labelCell.Range.Font.Size = 6.5;
            valueCell.Range.Font.Size = 6.5;
            labelCell.Range.Font.Bold = 1;
            valueCell.Range.Font.Bold = 0;

            labelCell.LeftPadding = 0;
            labelCell.RightPadding = InchesToPoints(0.01);
            valueCell.LeftPadding = InchesToPoints(0.01);
            valueCell.RightPadding = 0;

            labelCell.Range.ParagraphFormat.Alignment = WdAlignParagraphRight;
            valueCell.Range.ParagraphFormat.Alignment = WdAlignParagraphLeft;
            labelCell.Range.ParagraphFormat.SpaceAfter = 0;
            valueCell.Range.ParagraphFormat.SpaceAfter = 0;
        }

        private static float InchesToPoints(double inches) => (float)(inches * 72.0);
    }
}
