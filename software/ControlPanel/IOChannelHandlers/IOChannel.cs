using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace ControlPanel.IOChannelHandlers {
    public class IOChannel<T> : ViewModelBase {
        private T _value;
        private bool _isServerUpdate;
        private string _userID;
        private string _units;
        protected readonly IDimmer dimmer;


        public uint Index { get; }
        public string Label { get { return Prefix + (Index + 1).ToString(); } }
        public string Prefix { get; }

        public virtual T Value {
            get { return _value; }
            set {
                if (!Equals(_value, value)) {
                    _value = value;
                    OnPropertyChanged();

                    if (!_isServerUpdate) {
                        OnUIValueChanged(value);
                    }
                }
            }
        }

        public bool IsServerUpdate {
            get => _isServerUpdate;
            set => _isServerUpdate = value;
            
        }
        protected virtual void OnUIValueChanged(T newValue) {
            // Default does nothing, subclasses can override.
        }

        // Safe method for updates coming from the server
        public virtual void SetValueFromServer(T newValue) {
            try {
                _isServerUpdate = true;
                Value = newValue;   // update silently
            } finally {
                _isServerUpdate = false;
            }
        }

        public string UserID {
            get { return _userID; }
            set { _userID = value; OnPropertyChanged(); }
        }

        public string Units {
            get { return _units; }
            set { _units = value; OnPropertyChanged(); }
        }

        // ------------------------------------------------------------
        // Command bound to HamburgerButton in XAML
        // ------------------------------------------------------------
        private ICommand _editCommand;
        public ICommand EditCommand {
            get { return _editCommand; }
            set { 
                _editCommand = value; 
                OnPropertyChanged(); 
            }
        }

        public IOChannel(string prefix, uint index, string userID = "", string units = "", IDimmer dimmer = null) {
            Prefix = prefix;
            Index = index;
            _userID = userID;
            _units = units;

            // Assign default command to handle edit action
            EditCommand = new RelayCommand(OnEditRequested);
            this.dimmer = dimmer;
        }

        // Fired when the hamburger button is clicked
        public event Action<IOChannel<T>> EditRequested;

        protected virtual void OnEditRequested() {
            if (EditRequested != null)
                EditRequested(this);
        }
        public virtual void Initialize() {

        }
    }
}
