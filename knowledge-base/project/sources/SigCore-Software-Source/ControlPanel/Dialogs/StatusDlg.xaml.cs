using Newtonsoft.Json.Linq;
using System.Windows;
using System.Windows.Documents;

namespace ControlPanel.Dialogs {
    public partial class StatusDlg : Window {

        public StatusDlg(JObject json) {
            InitializeComponent();
            DisplayStatus(json);
        }

        private void DisplayStatus(JObject obj) {
            FlowDocument doc = new FlowDocument();

            JArray sections = (JArray)obj["sections"];

            foreach (JObject section in sections) {

                string title = (string)section["section"];

                // Section header: larger, stronger
                doc.Blocks.Add(new Paragraph(new Bold(new Run(title))) {
                    FontSize = 16,
                    Margin = new Thickness(0, 15, 0, 6)
                });

                Table table = new Table {
                    CellSpacing = 2,
                    Margin = new Thickness(10, 0, 0, 10)
                };

                table.Columns.Add(new TableColumn { Width = new GridLength(160) });
                table.Columns.Add(new TableColumn());

                TableRowGroup group = new TableRowGroup();
                table.RowGroups.Add(group);

                JArray items = (JArray)section["items"];

                foreach (JObject item in items) {

                    string label = (string)item["label"];
                    string data = (string)item["data"];

                    TableRow row = new TableRow();

                    // Label
                    row.Cells.Add(new TableCell(
                        new Paragraph(new Bold(new Run(label + ":"))) {
                            FontSize = 14,
                            Margin = new Thickness(5, 2, 5, 2)
                        }));

                    // Data
                    row.Cells.Add(new TableCell(
                        new Paragraph(new Run(data)) {
                            FontSize = 14,
                            Margin = new Thickness(5, 2, 5, 2)
                        }));

                    group.Rows.Add(row);
                }

                doc.Blocks.Add(table);
            }

            rtb.Document = doc;
        }

        private void Close_Click(object sender, RoutedEventArgs e) {
            Close();
        }
    }
}
