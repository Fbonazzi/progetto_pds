using KnightElfLibrary;
using System.ComponentModel;

namespace KnightElfServer
{
    internal class DataModel : INotifyPropertyChanged
    {
        private ConnectionParams _connectionParams;
        public ConnectionParams ConnParams {
            get { return _connectionParams; }
            set
            {
                if (value == _connectionParams)
                    return;

                _connectionParams = value;
                OnPropertyChanged("ConnParams");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}