using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ControlPanel {
    public class ViewModelBase : INotifyPropertyChanged {
        public ViewModelBase() {
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// The Attribute that the view model is based on
        /// Derived view models are expected to create a property that
        /// references this property and casts the result appropriately
        /// </summary>

        /// <summary>
        /// Called each time a property value changes. 
        /// Forces the view to update
        /// </summary>
        /// <param name="name">The name of the property value that changed</param>
        public virtual void OnPropertyChanged([CallerMemberName] string name = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

    }
}
