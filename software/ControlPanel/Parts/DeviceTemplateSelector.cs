using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;

namespace ControlPanel.Parts {
    public class DeviceTemplateSelector : DataTemplateSelector {
        public DataTemplate PartD88A42Template { get; set; }
        public DataTemplate SystemVMTemplate { get; set; }
        public DataTemplate DefaultTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container) {

            if (item is SystemVM)
                return SystemVMTemplate;

            return DefaultTemplate; // Fallback if an unknown device is added
        }
    }
}
