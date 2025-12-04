using SigCoreCommon;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ControlPanel.Dialogs {
    public class LoggerConfigDlgVM : ViewModelBase {
        private readonly MYSQL_LOGGER.DataLoggerConfig _config;

        public LoggerConfigDlgVM(MYSQL_LOGGER.DataLoggerConfig config) {
            _config = config;
        }

        public bool Enabled {
            get { return _config.Enabled; }
            set {
                if (_config.Enabled != value) {
                    _config.Enabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public double IntervalSec {
            get { return _config.IntervalSec; }
            set {
                if (_config.IntervalSec != value) {
                    _config.IntervalSec = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Server {
            get { return _config.Server; }
            set {
                if (_config.Server != value) {
                    _config.Server = value;
                    OnPropertyChanged();
                }
            }
        }

        public int Port {
            get { return _config.Port; }
            set {
                if (_config.Port != value) {
                    _config.Port = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Database {
            get { return _config.Database; }
            set {
                if (_config.Database != value) {
                    _config.Database = value;
                    OnPropertyChanged();
                }
            }
        }

        public string User {
            get { return _config.User; }
            set {
                if (_config.User != value) {
                    _config.User = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Password {
            get { return _config.Password; }
            set {
                if (_config.Password != value) {
                    _config.Password = value;
                    OnPropertyChanged();
                }
            }
        }

        public string TableName {
            get { return _config.TableName; }
            set {
                if (_config.TableName != value) {
                    _config.TableName = value;
                    OnPropertyChanged();
                }
            }
        }

        public int ConnectionTimeoutSec {
            get { return _config.ConnectionTimeoutSec; }
            set {
                if (_config.ConnectionTimeoutSec != value) {
                    _config.ConnectionTimeoutSec = value;
                    OnPropertyChanged();
                }
            }
        }

        public int CommandTimeoutSec {
            get { return _config.CommandTimeoutSec; }
            set {
                if (_config.CommandTimeoutSec != value) {
                    _config.CommandTimeoutSec = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Status {
            get { return _config.Status; }
            set {
                if (_config.Status != value) {
                    _config.Status = value;
                    OnPropertyChanged();
                }
            }
        }

        public MYSQL_LOGGER.DataLoggerConfig Config => _config;
    }
}
