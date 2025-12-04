using System.Windows.Input;
using System;
using System.Windows.Threading;

namespace ControlPanel {
    public class RelayCommand : ICommand {
        private readonly Action execute;
        private readonly Func<bool> canExecute;
        private readonly Dispatcher dispatcher;

        public RelayCommand(Action execute)
            : this(execute, null) {
        }

        public RelayCommand(Action execute, Func<bool> canExecute) {
            if (execute == null)
                throw new ArgumentNullException("execute");

            this.execute = execute;
            this.canExecute = canExecute;

            // Get the Dispatcher from the application's main UI thread
            dispatcher = Dispatcher.CurrentDispatcher;

            // Subscribe to the CommandManager.RequerySuggested event on the UI thread
            CommandManager.RequerySuggested += CommandManager_RequerySuggested;
        }

        public bool CanExecute(object parameter) {
            if (dispatcher.CheckAccess()) {
                return canExecute == null ? true : canExecute();
            } else {
                return (bool)dispatcher.Invoke(new Func<bool>(() => canExecute == null ? true : canExecute()));
            }
        }

        public void Execute(object parameter) {
            execute();
        }

        // This method will be executed on the UI thread
        private void CommandManager_RequerySuggested(object sender, EventArgs e) {
            if (dispatcher.CheckAccess()) {
                // If we are already on the UI thread, execute directly
                RaiseCanExecuteChanged();
            } else {
                // If not on the UI thread, use the Dispatcher to execute on the UI thread
                dispatcher.Invoke(RaiseCanExecuteChanged);
            }
        }

        private EventHandler _canExecuteChanged;
        public event EventHandler CanExecuteChanged {
            add {
                if (dispatcher.CheckAccess()) {
                    _canExecuteChanged += value;
                } else {
                    dispatcher.Invoke(() => _canExecuteChanged += value);
                }
            }
            remove {
                if (dispatcher.CheckAccess()) {
                    _canExecuteChanged -= value;
                } else {
                    dispatcher.Invoke(() => _canExecuteChanged -= value);
                }
            }
        }

        public void RaiseCanExecuteChanged() {
            if (dispatcher.CheckAccess()) {
                var handler = _canExecuteChanged;
                handler?.Invoke(this, EventArgs.Empty);
            } else {
                dispatcher.Invoke(() => {
                    var handler = _canExecuteChanged;
                    handler?.Invoke(this, EventArgs.Empty);
                });
            }
        }
    }
    public class RelayCommandParam<T> : ICommand {
        private readonly Action<T> execute;
        private readonly Func<T, bool> canExecute;

        public RelayCommandParam(Action<T> execute, Func<T, bool> canExecute = null) {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.canExecute = canExecute;
        }

        public bool CanExecute(object parameter) {
            return canExecute == null || (parameter is T param && canExecute(param));
        }

        public void Execute(object parameter) {
            if (parameter is T param) {
                execute(param);
            }
        }

        public event EventHandler CanExecuteChanged {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public void RaiseCanExecuteChanged() {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}