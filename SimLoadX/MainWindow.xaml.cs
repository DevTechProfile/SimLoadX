using System.Reactive.Subjects;
using System.Windows;

namespace SimLoadX
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
       private ISubject<Size> _sizeStream;

        public MainWindow()
        {
            InitializeComponent();
            _sizeStream = new Subject<Size>();
            DataContext = new MainViewModel(_sizeStream);
        }

        private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _sizeStream.OnNext(e.NewSize);
        }
    }
}
